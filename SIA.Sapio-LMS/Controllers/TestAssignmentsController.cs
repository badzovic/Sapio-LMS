using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio_LMS.Areas.Models;
using SIA.Sapio_LMS.Models;
using SIA.Sapio_LMS.Services;
using System.Security.Claims;
using SIA.Sapio_LMS.Areas.Instructor.Models;

namespace SIA.Sapio_LMS.Controllers
{
    [Authorize(Roles = "Admin,Instruktor")]
    public class TestAssignmentsController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly V3MatrixApiService _v3MatrixApiService;

        public TestAssignmentsController(
            ApplicationDbContext dbContext,
            V3MatrixApiService v3MatrixApiService)
        {
            _dbContext = dbContext;
            _v3MatrixApiService = v3MatrixApiService;
        }



        [HttpGet]
        public async Task<IActionResult> Index(string mode = "instructor")
        {
            var identityUserId =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var isAdminMode = mode == "admin";
            var isInstructorMode = mode == "instructor";

            if (!isAdminMode && !isInstructorMode)
            {
                return BadRequest("Nepoznat način prikaza.");
            }

            if (isAdminMode && !User.IsInRole("Admin"))
            {
                TempData["ErrorMessage"] =
                    "Nemate pravo pristupa admin prikazu.";

                return RedirectToAction(nameof(Index),
                    new { mode = "instructor" });
            }

            if (isInstructorMode && !User.IsInRole("Instruktor"))
            {
                TempData["ErrorMessage"] =
                    "Nemate pravo pristupa instruktor prikazu.";

                return RedirectToAction(nameof(Index),
                    new { mode = "admin" });
            }

            int? trenutniKorisnikId = null;

            if (isInstructorMode)
            {
                trenutniKorisnikId = await _dbContext.KORISNICI
                    .Where(x => x.USER_ID == identityUserId)
                    .Select(x => (int?)x.ID)
                    .FirstOrDefaultAsync();

                if (trenutniKorisnikId == null)
                {
                    TempData["ErrorMessage"] =
                        "Nije pronađen povezani korisnik za instruktora.";

                    ViewBag.Mode = mode;

                    return View(
                        "~/Views/Shared/TestAssignment/Index.cshtml",
                        new List<TestAssignmentListItemVM>());
                }
            }

            var now = DateTime.Now;

            var query = _dbContext.TESTOVI
                .AsNoTracking()
                .AsQueryable();

            // Instruktor vidi samo dostupne testove
            // iz predmeta koje aktivno predaje.
            if (isInstructorMode)
            {
                query = query.Where(x =>
                    x.AKTIVNO == "Y" &&
                    x.STATUS == "ACTIVE" &&
                    x.DATUM_OD <= now &&
                    x.DATUM_DO >= now &&
                    _dbContext.PREDMET_INSTRUKTORI.Any(pi =>
                        pi.PREDMET_ID == x.PREDMET_ID &&
                        pi.KORISNIK_ID == trenutniKorisnikId &&
                        pi.AKTIVNO == "Y"));
            }

            // Admin dobija sve testove, uključujući
            // neaktivne i testove kojima je istekao period.
            var model = await query
                .OrderBy(x => x.Predmet.NAZIV)
                .ThenBy(x => x.NAZIV)
                .Select(x => new TestAssignmentListItemVM
                {
                    TestId = x.ID,
                    TestNaziv = x.NAZIV,
                    PredmetNaziv = x.Predmet.NAZIV,

                    TestKlasaNaziv = x.TEST_KLASA != null
                        ? x.TEST_KLASA.NAZIV
                        : "-",

                    DatumOd = x.DATUM_OD,
                    DatumDo = x.DATUM_DO,

                    BrojPitanja = x.BROJ_PITANJA,

                    BrojDodijeljenihStudenata =
                        _dbContext.DODIJELJENI_TESTOVI.Count(d =>
                            d.TEST_ID == x.ID &&
                            d.AKTIVNO == "Y"),

                    MozeDodijeliti =
                        x.AKTIVNO == "Y" &&
                        x.STATUS == "ACTIVE" &&
                        x.DATUM_OD <= now &&
                        x.DATUM_DO >= now,

                    MozePregledati = isAdminMode,

                    StatusPrikaza =
                        x.AKTIVNO != "Y" || x.STATUS != "ACTIVE"
                            ? "Neaktivan"
                            : x.DATUM_DO < now
                                ? "Istekao"
                                : x.DATUM_OD > now
                                    ? "Zakazan"
                                    : "Aktivan"
                })
                .ToListAsync();

            ViewBag.Mode = mode;

            return View(
                "~/Views/Shared/TestAssignment/Index.cshtml",
                model);
        }





        [HttpGet]
        public async Task<IActionResult> Assign(
            int id,
            string mode = "instructor",
            string studentFilter = "dueSoon")
        {
            var isAdminMode = mode == "admin";
            var isInstructorMode = mode == "instructor";

            // Provjera načina pristupa
            if (!isAdminMode && !isInstructorMode)
            {
                return BadRequest("Nepoznat način pristupa.");
            }

            if (isAdminMode && !User.IsInRole("Admin"))
            {
                TempData["ErrorMessage"] =
                    "Nemate pravo pristupa admin prikazu.";

                return RedirectToAction(
                    nameof(Index),
                    new { mode = "instructor" });
            }

            if (isInstructorMode && !User.IsInRole("Instruktor"))
            {
                TempData["ErrorMessage"] =
                    "Nemate pravo pristupa instruktor prikazu.";

                return RedirectToAction(
                    nameof(Index),
                    new { mode = "admin" });
            }

            var identityUserId =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var now = DateTime.Now;

            // 1. Dohvat testa
            var test = await _dbContext.TESTOVI
                .AsNoTracking()
                .Include(x => x.Predmet)
                .FirstOrDefaultAsync(x => x.ID == id);

            if (test == null)
            {
                TempData["ErrorMessage"] = "Test nije pronađen.";

                return RedirectToAction(nameof(Index), new { mode });
            }

            // 2. Provjera dostupnosti testa
            if (test.AKTIVNO != "Y" || test.STATUS != "ACTIVE")
            {
                TempData["ErrorMessage"] =
                    "Test nije aktivan i nije ga moguće dodijeliti studentima.";

                return RedirectToAction(nameof(Index), new { mode });
            }

            if (test.DATUM_OD > now)
            {
                TempData["ErrorMessage"] =
                    "Test još nije dostupan za dodjelu. Period važenja testa nije počeo.";

                return RedirectToAction(nameof(Index), new { mode });
            }

            if (test.DATUM_DO < now)
            {
                TempData["ErrorMessage"] =
                    "Period važenja testa je istekao. Test više nije moguće dodijeliti studentima.";

                return RedirectToAction(nameof(Index), new { mode });
            }

            // 3. Provjera prava instruktora
            int? trenutniKorisnikId = null;

            if (isInstructorMode)
            {
                trenutniKorisnikId = await _dbContext.KORISNICI
                    .Where(x => x.USER_ID == identityUserId)
                    .Select(x => (int?)x.ID)
                    .FirstOrDefaultAsync();

                if (!trenutniKorisnikId.HasValue)
                {
                    TempData["ErrorMessage"] =
                        "Nije pronađen povezani korisnik za trenutno prijavljenog instruktora.";

                    return RedirectToAction(nameof(Index), new { mode });
                }

                var instruktorImaPravo = await _dbContext.PREDMET_INSTRUKTORI
                    .AnyAsync(pi =>
                        pi.PREDMET_ID == test.PREDMET_ID &&
                        pi.KORISNIK_ID == trenutniKorisnikId.Value &&
                        pi.AKTIVNO == "Y");

                if (!instruktorImaPravo)
                {
                    TempData["ErrorMessage"] =
                        "Nemate pravo dodjeljivati ovaj test.";

                    return RedirectToAction(nameof(Index), new { mode });
                }
            }

            // 4. Sve historijske dodjele konkretnog testa
            var postojeceDodjele = await _dbContext.DODIJELJENI_TESTOVI
                .AsNoTracking()
                .Where(x => x.TEST_ID == id)
                .OrderByDescending(x => x.DATUM_DODJELE)
                .ThenByDescending(x => x.ID)
                .Select(x => new
                {
                    x.ID,
                    x.KORISNIK_ID,
                    x.DATUM_DODJELE,
                    x.ROK_ZA_POLAGANJE,
                    x.STATUS,
                    x.AKTIVNO
                })
                .ToListAsync();

            var dodjeleByKorisnik = postojeceDodjele
                .GroupBy(x => x.KORISNIK_ID)
                .ToDictionary(x => x.Key, x => x.ToList());

            // 5. Polaganja konkretnog testa
            var polaganja = await _dbContext.POLAGANJE_TESTA
                .AsNoTracking()
                .Where(x => x.TEST_ID == id)
                .Select(x => new
                {
                    x.ID,
                    x.KORISNIK_ID,
                    x.DODIJELJENI_TEST_ID,
                    x.STATUS,
                    x.IS_PASSED,
                    x.VRIJEME_ISTEKA,
                    x.DATUM_POCETKA
                })
                .ToListAsync();

            var polaganjaByDodjela = polaganja
                .Where(x => x.DODIJELJENI_TEST_ID.HasValue)
                .GroupBy(x => x.DODIJELJENI_TEST_ID!.Value)
                .ToDictionary(x => x.Key, x => x.ToList());

            // 6. Aktivni LMS korisnici i Identity email adrese
            var korisniciRaw = await _dbContext.KORISNICI
                .AsNoTracking()
                .Where(k => k.AKTIVAN == "Y")
                .GroupJoin(
                    _dbContext.Users.AsNoTracking(),
                    k => k.USER_ID,
                    u => u.Id,
                    (k, users) => new
                    {
                        Korisnik = k,
                        IdentityUsers = users
                    })
                .SelectMany(
                    x => x.IdentityUsers.DefaultIfEmpty(),
                    (x, u) => new
                    {
                        x.Korisnik.ID,
                        x.Korisnik.IME,
                        x.Korisnik.PREZIME,

                        EMAIL = u != null ? u.Email : null,

                        x.Korisnik.KOMPANIJA,
                        x.Korisnik.ACCESS_KEY
                    })
                .OrderBy(x => x.PREZIME)
                .ThenBy(x => x.IME)
                .ToListAsync();

            // 7. V3 statusi obuke
            List<V3RadnikProgramStatusDto> v3Statusi = new();

            if (studentFilter != "all")
            {
                if (test.Predmet == null ||
                    !test.Predmet.V3_ID_OBUKE.HasValue)
                {
                    TempData["ErrorMessage"] =
                        "Predmet nije povezan sa V3 obukom.";

                    return RedirectToAction(nameof(Index), new { mode });
                }

                var apiResponse = await _v3MatrixApiService
                    .GetRadniciProgramStatusAsync(
                        test.Predmet.V3_ID_OBUKE.Value);

                v3Statusi = apiResponse?.Users
                    ?? new List<V3RadnikProgramStatusDto>();
            }

            var v3StatusByRadnikId = v3Statusi
                .GroupBy(x => x.RadnikId)
                .ToDictionary(x => x.Key, x => x.First());

            // 8. Priprema korisnika i utvrđivanje mogućnosti dodjele
            var studenti = korisniciRaw
                .Select(x =>
                {
                    V3RadnikProgramStatusDto? status = null;

                    if (x.ACCESS_KEY.HasValue)
                    {
                        v3StatusByRadnikId.TryGetValue(
                            x.ACCESS_KEY.Value,
                            out status);
                    }

                    var statusObuke = "Van matrice";
                    DateTime? datumVaziDo = null;
                    int? danaDoIsteka = null;

                    if (status != null)
                    {
                        datumVaziDo = status.DatumVaziDo;
                        danaDoIsteka = status.DanaDoIsteka;

                        if (!status.DatumVaziDo.HasValue)
                        {
                            statusObuke = "Nema evidenciju";
                        }
                        else if (status.Isteklo)
                        {
                            statusObuke = "Isteklo";
                        }
                        else if (status.DanaDoIsteka.HasValue &&
                                 status.DanaDoIsteka.Value <= 90)
                        {
                            statusObuke = "Ističe u 3 mjeseca";
                        }
                        else
                        {
                            statusObuke = "Važeće";
                        }
                    }

                    var item = new AssignTestStudentItemVM
                    {
                        KorisnikId = x.ID,
                        ImePrezime = $"{x.IME} {x.PREZIME}".Trim(),
                        Email = x.EMAIL,
                        Kompanija = x.KOMPANIJA,

                        DatumVaziDo = datumVaziDo,
                        DanaDoIsteka = danaDoIsteka,
                        StatusObuke = statusObuke,

                        BrojDozvoljenihPokusaja = test.BROJ_POKUSAJA,

                        // Niko nije automatski označen za novu dodjelu.
                        Odabran = false,
                        MozeNovaDodjela = false
                    };

                    // Pronalazimo historiju dodjela ovog korisnika
                    if (!dodjeleByKorisnik.TryGetValue(
                            x.ID,
                            out var dodjeleKorisnika) ||
                        dodjeleKorisnika.Count == 0)
                    {
                        // Nikada nije imao ovaj test
                        item.VecDodijeljen = false;
                        item.MozeNovaDodjela = true;
                        item.StatusPosljednjeDodjele = "Nije dodijeljen";

                        return item;
                    }

                    // Posljednja dodjela po datumu i ID-u
                    var posljednja = dodjeleKorisnika.First();

                    item.VecDodijeljen = dodjeleKorisnika
                        .Any(d => d.AKTIVNO == "Y");

                    item.PosljednjaDodjelaId = posljednja.ID;
                    item.DatumPosljednjeDodjele =
                        posljednja.DATUM_DODJELE;
                    item.RokPosljednjeDodjele =
                        posljednja.ROK_ZA_POLAGANJE;

                    item.StatusPosljednjeDodjele =
                        posljednja.STATUS ?? "DODIJELJEN";

                    // Pokušaji posljednje dodjele
                    if (!polaganjaByDodjela.TryGetValue(
                            posljednja.ID,
                            out var pokusajiDodjele))
                    {
                        item.BrojIskoristenihPokusaja = 0;
                    }
                    else
                    {
                        item.BrojIskoristenihPokusaja =
                            pokusajiDodjele.Count(p =>
                                p.STATUS != "CANCELLED");
                    }

                    var imaPolozenTest = polaganjaByDodjela
                        .Where(g => dodjeleKorisnika.Any(d =>
                            d.ID == g.Key))
                        .SelectMany(g => g.Value)
                        .Any(p => p.IS_PASSED == "Y");

                    // Provjera svih neriješenih pokušaja
                    var imaPolaganjeUToku = polaganjaByDodjela
                        .Where(g => dodjeleKorisnika.Any(d =>
                            d.ID == g.Key))
                        .SelectMany(g => g.Value)
                        .Any(p => p.STATUS == "IN_PROGRESS");

                    var cekaOcjenjivanje = polaganjaByDodjela
                        .Where(g => dodjeleKorisnika.Any(d =>
                            d.ID == g.Key))
                        .SelectMany(g => g.Value)
                        .Any(p => p.STATUS == "PENDING_REVIEW");

                    item.ImaPolozenTest = imaPolozenTest;
                    item.CekaOcjenjivanje = cekaOcjenjivanje;

                    // Provjera neuspješnih ili isteklih dodjela
                    var rokIstekao =
                        posljednja.ROK_ZA_POLAGANJE.HasValue &&
                        posljednja.ROK_ZA_POLAGANJE.Value < now;

                    var nemaPokusaja =
                        item.BrojIskoristenihPokusaja >=
                        test.BROJ_POKUSAJA;

                    // Provjeravamo postoji li druga aktivna dodjela
                    // koja još uvijek traje.
                    var drugaAktivnaDodjela = dodjeleKorisnika.Any(d =>
                        d.ID != posljednja.ID &&
                        d.AKTIVNO == "Y" &&
                        d.STATUS != "ISTEKAO" &&
                        (!d.ROK_ZA_POLAGANJE.HasValue ||
                         d.ROK_ZA_POLAGANJE.Value >= now));

                    if (imaPolozenTest)
                    {
                        item.StatusPosljednjeDodjele = "POLOZEN";
                        item.RazlogNemoguceDodjele =
                            "Korisnik je već položio ovaj test.";
                    }
                    else if (cekaOcjenjivanje)
                    {
                        item.StatusPosljednjeDodjele =
                            "NA_OCJENJIVANJU";

                        item.RazlogNemoguceDodjele =
                            "Postoji pokušaj koji čeka ocjenjivanje.";
                    }
                    else if (imaPolaganjeUToku)
                    {
                        item.StatusPosljednjeDodjele = "U_TOKU";

                        item.RazlogNemoguceDodjele =
                            "Korisnik ima započeto polaganje.";
                    }
                    else if (drugaAktivnaDodjela)
                    {
                        item.RazlogNemoguceDodjele =
                            "Korisnik ima drugu aktivnu dodjelu ovog testa.";
                    }
                    else if (rokIstekao)
                    {
                        item.StatusPosljednjeDodjele = "ISTEKAO";
                        item.PotrebnaIntervencijaAdmina = true;

                        item.MozeNovaDodjela = isAdminMode;

                        item.RazlogNemoguceDodjele = isAdminMode
                            ? ""
                            : "Istekao je rok. Ponovnu dodjelu može izvršiti administrator.";
                    }
                    else if (nemaPokusaja)
                    {
                        item.StatusPosljednjeDodjele =
                            "ISCRPLJENI_POKUSAJI";

                        item.PotrebnaIntervencijaAdmina = true;

                        item.MozeNovaDodjela = isAdminMode;

                        item.RazlogNemoguceDodjele = isAdminMode
                            ? ""
                            : "Iskorišteni su pokušaji. Ponovnu dodjelu može izvršiti administrator.";
                    }
                    else
                    {
                        item.RazlogNemoguceDodjele =
                            "Korisnik već ima prethodnu dodjelu ovog testa.";
                    }

                    return item;
                })
                .Where(x =>
                    studentFilter == "all"
                    || studentFilter == "allMatrix" &&
                       x.StatusObuke != "Van matrice"
                    || studentFilter == "dueSoon" &&
                       (
                           x.StatusObuke == "Nema evidenciju"
                           || x.StatusObuke == "Isteklo"
                           || x.StatusObuke == "Ističe u 3 mjeseca"
                       ))
                .ToList();

            // 9. ViewModel
            var model = new AssignTestStudentsVM
            {
                TestId = test.ID,
                TestNaziv = test.NAZIV,
                PredmetNaziv = test.Predmet != null
                    ? test.Predmet.NAZIV
                    : "",
                Studenti = studenti
            };

            ViewBag.Mode = mode;
            ViewBag.StudentFilter = studentFilter;

            return View(
                "~/Views/Shared/TestAssignment/Assign.cshtml",
                model);
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(
            AssignTestStudentsVM model,
            string mode = "instructor")
        {
            var isAdminMode = mode == "admin";
            var isInstructorMode = mode == "instructor";

            // 1. Provjera načina pristupa
            if (!isAdminMode && !isInstructorMode)
            {
                return BadRequest("Nepoznat način pristupa.");
            }

            if (isAdminMode && !User.IsInRole("Admin"))
            {
                TempData["ErrorMessage"] =
                    "Nemate pravo pristupa admin prikazu.";

                return RedirectToAction(
                    nameof(Index),
                    new { mode = "instructor" });
            }

            if (isInstructorMode && !User.IsInRole("Instruktor"))
            {
                TempData["ErrorMessage"] =
                    "Nemate pravo pristupa instruktor prikazu.";

                return RedirectToAction(
                    nameof(Index),
                    new { mode = "admin" });
            }

            var identityUserId =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var now = DateTime.Now;

            // 2. Dohvat testa
            var test = await _dbContext.TESTOVI
                .FirstOrDefaultAsync(x => x.ID == model.TestId);

            if (test == null)
            {
                TempData["ErrorMessage"] =
                    "Test nije pronađen.";

                return RedirectToAction(
                    nameof(Index),
                    new { mode });
            }

            // 3. Provjera dostupnosti testa
            if (test.AKTIVNO != "Y" || test.STATUS != "ACTIVE")
            {
                TempData["ErrorMessage"] =
                    "Test nije aktivan i nije ga moguće dodijeliti.";

                return RedirectToAction(
                    nameof(Index),
                    new { mode });
            }

            if (test.DATUM_OD > now)
            {
                TempData["ErrorMessage"] =
                    "Period dostupnosti testa još nije počeo.";

                return RedirectToAction(
                    nameof(Index),
                    new { mode });
            }

            if (test.DATUM_DO < now)
            {
                TempData["ErrorMessage"] =
                    "Period dostupnosti testa je istekao.";

                return RedirectToAction(
                    nameof(Index),
                    new { mode });
            }

            // 4. Provjera prava instruktora
            if (isInstructorMode)
            {
                var trenutniKorisnikId = await _dbContext.KORISNICI
                    .Where(x => x.USER_ID == identityUserId)
                    .Select(x => (int?)x.ID)
                    .FirstOrDefaultAsync();

                if (!trenutniKorisnikId.HasValue)
                {
                    TempData["ErrorMessage"] =
                        "Nije pronađen povezani korisnik instruktora.";

                    return RedirectToAction(
                        nameof(Index),
                        new { mode });
                }

                var instruktorImaPravo =
                    await _dbContext.PREDMET_INSTRUKTORI
                        .AnyAsync(pi =>
                            pi.PREDMET_ID == test.PREDMET_ID &&
                            pi.KORISNIK_ID == trenutniKorisnikId.Value &&
                            pi.AKTIVNO == "Y");

                if (!instruktorImaPravo)
                {
                    TempData["ErrorMessage"] =
                        "Nemate pravo dodjeljivati ovaj test.";

                    return RedirectToAction(
                        nameof(Index),
                        new { mode });
                }
            }

            // 5. Odabrani korisnici, bez duplikata
            var selectedStudentIds =
                (model.SelectedStudentIds ?? new List<int>())
                    .Where(x => x > 0)
                    .Distinct()
                    .ToList();

            if (!selectedStudentIds.Any())
            {
                TempData["ErrorMessage"] =
                    "Niste odabrali nijednog korisnika.";

                return RedirectToAction(
                    nameof(Assign),
                    new
                    {
                        id = model.TestId,
                        mode,
                        studentFilter = "all"
                    });
            }

            // 6. Aktivni LMS korisnici
            var aktivniKorisnikIds = await _dbContext.KORISNICI
                .AsNoTracking()
                .Where(x => x.AKTIVAN == "Y")
                .Select(x => x.ID)
                .ToListAsync();

            var aktivniKorisniciSet =
                aktivniKorisnikIds.ToHashSet();

            // 7. Sve prethodne dodjele konkretnog testa
            var postojeceDodjele = await _dbContext.DODIJELJENI_TESTOVI
                .Where(x => x.TEST_ID == model.TestId)
                .OrderByDescending(x => x.DATUM_DODJELE)
                .ThenByDescending(x => x.ID)
                .ToListAsync();

            var dodjeleByKorisnik = postojeceDodjele
                .GroupBy(x => x.KORISNIK_ID)
                .ToDictionary(x => x.Key, x => x.ToList());

            // 8. Sva polaganja ovog testa
            var polaganja = await _dbContext.POLAGANJE_TESTA
                .AsNoTracking()
                .Where(x => x.TEST_ID == model.TestId)
                .Select(x => new
                {
                    x.KORISNIK_ID,
                    x.DODIJELJENI_TEST_ID,
                    x.STATUS,
                    x.IS_PASSED
                })
                .ToListAsync();

            var polaganjaByDodjela = polaganja
                .Where(x => x.DODIJELJENI_TEST_ID.HasValue)
                .GroupBy(x => x.DODIJELJENI_TEST_ID!.Value)
                .ToDictionary(x => x.Key, x => x.ToList());

            // 9. Datum i rok ručne dodjele
            var datumDodjele = now;
            var rokZaPolaganje = datumDodjele.AddMonths(2);

            int brojPrvihDodjela = 0;
            int brojPonovnihDodjela = 0;
            int brojPreskocenih = 0;

            // 10. Obrada odabranih korisnika
            foreach (var studentId in selectedStudentIds)
            {
                // Ne dodjeljujemo neaktivnom ili nepostojećem korisniku
                if (!aktivniKorisniciSet.Contains(studentId))
                {
                    brojPreskocenih++;
                    continue;
                }

                if (!dodjeleByKorisnik.TryGetValue(
                        studentId,
                        out var dodjeleKorisnika) ||
                    dodjeleKorisnika.Count == 0)
                {
                    // PRVA DODJELA
                    _dbContext.DODIJELJENI_TESTOVI.Add(
                        new DodijeljeniTest
                        {
                            TEST_ID = model.TestId,
                            KORISNIK_ID = studentId,
                            DODIJELIO_USER_ID = identityUserId,

                            DATUM_DODJELE = datumDodjele,
                            ROK_ZA_POLAGANJE = rokZaPolaganje,

                            STATUS = "DODIJELJEN",
                            AUTO_DODJELA = "N",
                            AKTIVNO = "Y",

                            NAPOMENA = "Ručna dodjela testa."
                        });

                    brojPrvihDodjela++;
                    continue;
                }

                // Postoji prethodna dodjela.
                // Instruktor ne može izvršiti ponovnu dodjelu.
                if (!isAdminMode)
                {
                    brojPreskocenih++;
                    continue;
                }

                // Posljednja dodjela korisnika za ovaj test
                var posljednja = dodjeleKorisnika.First();

                // Sva polaganja koja pripadaju historiji ovog testa
                var svaPolaganjaKorisnika = polaganjaByDodjela
                    .Where(g => dodjeleKorisnika.Any(d => d.ID == g.Key))
                    .SelectMany(g => g.Value)
                    .ToList();

                // Ako je test već položen, ne kreiramo novu dodjelu
                var imaPolozenTest = svaPolaganjaKorisnika
                    .Any(x => x.IS_PASSED == "Y");

                if (imaPolozenTest)
                {
                    brojPreskocenih++;
                    continue;
                }

                // Ne smije postojati polaganje u toku
                var imaPolaganjeUToku = svaPolaganjaKorisnika
                    .Any(x => x.STATUS == "IN_PROGRESS");

                if (imaPolaganjeUToku)
                {
                    brojPreskocenih++;
                    continue;
                }

                // Ne smije postojati polaganje koje čeka ocjenjivanje
                var cekaOcjenjivanje = svaPolaganjaKorisnika
                    .Any(x => x.STATUS == "PENDING_REVIEW");

                if (cekaOcjenjivanje)
                {
                    brojPreskocenih++;
                    continue;
                }

                // Pokušaji posljednje dodjele
                var brojPokusaja = 0;

                if (polaganjaByDodjela.TryGetValue(
                        posljednja.ID,
                        out var pokusajiPosljednjeDodjele))
                {
                    brojPokusaja = pokusajiPosljednjeDodjele
                        .Count(x => x.STATUS != "CANCELLED");
                }

                // Provjera isteka roka
                var rokIstekao =
                    posljednja.ROK_ZA_POLAGANJE.HasValue &&
                    posljednja.ROK_ZA_POLAGANJE.Value < now;

                // Provjera iskorištenih pokušaja
                var pokusajiIskoristeni =
                    brojPokusaja >= test.BROJ_POKUSAJA;

                // Ako postoji neka druga aktivna dodjela
                // čiji rok još traje, ne dodjeljujemo ponovo
                var drugaAktivnaDodjela = dodjeleKorisnika
                    .Any(d =>
                        d.ID != posljednja.ID &&
                        d.AKTIVNO == "Y" &&
                        d.STATUS != "ISTEKAO" &&
                        (!d.ROK_ZA_POLAGANJE.HasValue ||
                         d.ROK_ZA_POLAGANJE.Value >= now));

                if (drugaAktivnaDodjela)
                {
                    brojPreskocenih++;
                    continue;
                }

                // Ponovna dodjela dozvoljena je samo ako
                // je rok istekao ili su pokušaji iskorišteni
                if (!rokIstekao && !pokusajiIskoristeni)
                {
                    brojPreskocenih++;
                    continue;
                }

                // Deaktiviramo prethodne aktivne dodjele,
                // bez brisanja historije ili izmjene njihovih rokova.
                foreach (var staraDodjela in dodjeleKorisnika
                    .Where(x => x.AKTIVNO == "Y"))
                {
                    staraDodjela.AKTIVNO = "N";
                    staraDodjela.DATUM_IZMJENE = now;
                }

                // NOVA RUČNA DODJELA
                _dbContext.DODIJELJENI_TESTOVI.Add(
                    new DodijeljeniTest
                    {
                        TEST_ID = model.TestId,
                        KORISNIK_ID = studentId,
                        DODIJELIO_USER_ID = identityUserId,

                        DATUM_DODJELE = datumDodjele,
                        ROK_ZA_POLAGANJE = rokZaPolaganje,

                        STATUS = "DODIJELJEN",
                        AUTO_DODJELA = "N",
                        AKTIVNO = "Y",

                        RAZLOG_DODJELE = rokIstekao
                            ? "PONOVNA_DODJELA_ISTEKAO_ROK"
                            : "PONOVNA_DODJELA_POKUSAJI",

                        NAPOMENA =
                            "Ponovna ručna dodjela od strane administratora."
                    });

                brojPonovnihDodjela++;
            }

            // 11. Snimanje svih promjena
            await _dbContext.SaveChangesAsync();

            // 12. Povratna poruka
            if (brojPrvihDodjela == 0 &&
                brojPonovnihDodjela == 0)
            {
                TempData["ErrorMessage"] =
                    $"Nije izvršena nijedna dodjela. " +
                    $"Preskočeno korisnika: {brojPreskocenih}.";
            }
            else
            {
                TempData["SuccessMessage"] =
                    $"Dodjela završena. " +
                    $"Prvih dodjela: {brojPrvihDodjela}, " +
                    $"ponovnih dodjela: {brojPonovnihDodjela}, " +
                    $"preskočeno: {brojPreskocenih}.";
            }

            return RedirectToAction(
                nameof(Index),
                new { mode });
        }





        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Preview(int id)
        {
            var test = await _dbContext.TESTOVI
                .AsNoTracking()
                .Include(x => x.Predmet)
                .Include(x => x.TEST_KLASA)
                .FirstOrDefaultAsync(x => x.ID == id);

            if (test == null)
                return NotFound();

            // Dohvati pitanja koja pripadaju konkretnom testu
            var pitanja = await _dbContext.TEST_PITANJA
                .AsNoTracking()
                .Include(x => x.TipPitanja)
                .Include(x => x.TipTezinePitanja)
                .Where(x => x.TEST_ID == id)
                .OrderBy(x => x.REDOSLIJED)
                .ToListAsync();

            // Osnovne informacije o testu
            var model = new SubjectTestDetailsVM
            {
                ID = test.ID,
                PredmetId = test.PREDMET_ID,
                PredmetNaziv = test.Predmet?.NAZIV ?? "-",

                TestKlasaId = test.TEST_KLASA_ID,
                TestKlasaNaziv = test.TEST_KLASA?.NAZIV ?? "-",

                Naziv = test.NAZIV,
                Opis = test.OPIS,

                DatumOd = test.DATUM_OD,
                DatumDo = test.DATUM_DO,

                VrijemeTrajanjaMin = test.VRIJEME_TRAJANJA_MIN,
                BrojPitanja = test.BROJ_PITANJA,

                BrojLakih = test.BROJ_LAKIH,
                BrojSrednjih = test.BROJ_SREDNJIH,
                BrojTeskih = test.BROJ_TESKIH,

                MinProcenatProlaza = test.MIN_PROCENAT_PROLAZA,
                BrojPokusaja = test.BROJ_POKUSAJA,

                RandomPitanja = test.RANDOM_PITANJA,
                RandomOdgovori = test.RANDOM_ODGOVORI,

                Status = test.STATUS,
                Aktivno = test.AKTIVNO,

                DatumKreiranja = test.DATUM_KREIRANJA,
                DatumIzmjene = test.DATUM_IZMJENE
            };

            // Učitavanje pitanja i njihovih odgovora
            foreach (var pitanje in pitanja)
            {
                var odgovori = await _dbContext.TEST_PITANJE_ODGOVORI
                    .AsNoTracking()
                    .Where(x => x.TEST_PITANJE_ID == pitanje.ID)
                    .OrderBy(x => x.REDOSLIJED)
                    .Select(x => new SubjectTestAnswerDetailsVM
                    {
                        ID = x.ID,
                        TekstOdgovora = x.TEKST_ODGOVORA,
                        IsTacan = x.IS_TACAN,
                        Bodovi = x.BODOVI,
                        NegativniBodovi = x.NEGATIVNI_BODOVI,
                        Redoslijed = x.REDOSLIJED
                    })
                    .ToListAsync();

                model.Pitanja.Add(new SubjectTestQuestionDetailsVM
                {
                    ID = pitanje.ID,
                    OriginalPitanjeId = pitanje.ORIGINAL_PITANJE_ID,

                    TekstPitanja = pitanje.TEKST_PITANJA,

                    // Slika koja je sačuvana uz pitanje u testu
                    SlikaPutanja = pitanje.SLIKA_PUTANJA,
                    SlikaNaziv = pitanje.SLIKA_NAZIV,

                    TipPitanja = pitanje.TipPitanja?.NAZIV ?? "-",
                    Tezina = pitanje.TipTezinePitanja?.NAZIV ?? "-",

                    Bodovi = pitanje.BODOVI,
                    NegativniBodovi = pitanje.NEGATIVNI_BODOVI,
                    ParcijalnoBodovanje = pitanje.PARCIJALNO_BODOVANJE,

                    IsObavezno = pitanje.IS_OBAVEZNO,
                    Redoslijed = pitanje.REDOSLIJED,

                    Odgovori = odgovori
                });
            }

            // Administratorski pregled - bez mogućnosti izmjene
            return View(
                "~/Views/Shared/TestAssignment/Preview.cshtml",
                model
            );
        }
    }
}