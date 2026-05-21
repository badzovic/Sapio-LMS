using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio_LMS.Areas.Models;
using SIA.Sapio_LMS.Models;
using SIA.Sapio_LMS.Services;
using System.Security.Claims;

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
            var identityUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var isAdminMode = mode == "admin";
            var isInstructorMode = mode == "instructor";

            if (isAdminMode && !User.IsInRole("Admin"))
            {
                TempData["ErrorMessage"] = "Nemate pravo pristupa admin prikazu.";
                return RedirectToAction(nameof(Index), new { mode = "instructor" });
            }

            if (isInstructorMode && !User.IsInRole("Instruktor"))
            {
                TempData["ErrorMessage"] = "Nemate pravo pristupa instruktor prikazu.";
                return RedirectToAction(nameof(Index), new { mode = "admin" });
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
                    TempData["ErrorMessage"] = "Nije pronađen povezani korisnik za trenutno prijavljenog instruktora.";
                    return View("~/Views/Shared/TestAssignment/Index.cshtml", new List<TestAssignmentListItemVM>());
                }
            }

            var query = _dbContext.TESTOVI
                .Include(x => x.Predmet)
                .Where(x =>
                    x.AKTIVNO == "Y" &&
                    x.STATUS == "ACTIVE");

            if (isInstructorMode)
            {
                query = query.Where(x =>
                    _dbContext.PREDMET_INSTRUKTORI.Any(pi =>
                        pi.PREDMET_ID == x.PREDMET_ID &&
                        pi.KORISNIK_ID == trenutniKorisnikId &&
                        pi.AKTIVNO == "Y"));
            }

            var model = await query
                .OrderBy(x => x.Predmet.NAZIV)
                .ThenBy(x => x.NAZIV)
                .Select(x => new TestAssignmentListItemVM
                {
                    TestId = x.ID,
                    TestNaziv = x.NAZIV,
                    PredmetNaziv = x.Predmet.NAZIV,
                    DatumOd = x.DATUM_OD,
                    DatumDo = x.DATUM_DO,
                    BrojPitanja = x.BROJ_PITANJA,
                    BrojDodijeljenihStudenata = _dbContext.DODIJELJENI_TESTOVI
                        .Count(d => d.TEST_ID == x.ID && d.AKTIVNO == "Y")
                })
                .ToListAsync();

            ViewBag.Mode = mode;

            return View("~/Views/Shared/TestAssignment/Index.cshtml", model);
        }

        [HttpGet]
        public async Task<IActionResult> Assign(
            int id,
            string mode = "instructor",
            string studentFilter = "dueSoon")
        {
            var test = await _dbContext.TESTOVI
                .Include(x => x.Predmet)
                .FirstOrDefaultAsync(x => x.ID == id);

            if (test == null)
            {
                TempData["ErrorMessage"] = "Test nije pronađen.";
                return RedirectToAction(nameof(Index), new { mode });
            }

            var isAdminMode = mode == "admin";
            var identityUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            int? trenutniKorisnikId = null;

            if (!isAdminMode)
            {
                trenutniKorisnikId = await _dbContext.KORISNICI
                    .Where(x => x.USER_ID == identityUserId)
                    .Select(x => (int?)x.ID)
                    .FirstOrDefaultAsync();

                if (trenutniKorisnikId == null)
                {
                    TempData["ErrorMessage"] = "Nije pronađen povezani korisnik za trenutno prijavljenog instruktora.";
                    return RedirectToAction(nameof(Index), new { mode });
                }

                var instruktorImaPravo = await _dbContext.PREDMET_INSTRUKTORI
                    .AnyAsync(pi =>
                        pi.PREDMET_ID == test.PREDMET_ID &&
                        pi.KORISNIK_ID == trenutniKorisnikId &&
                        pi.AKTIVNO == "Y");

                if (!instruktorImaPravo)
                {
                    TempData["ErrorMessage"] = "Nemate pravo dodjeljivati ovaj test.";
                    return RedirectToAction(nameof(Index), new { mode });
                }
            }

            var vecDodijeljeniIds = await _dbContext.DODIJELJENI_TESTOVI
                .Where(x => x.TEST_ID == id && x.AKTIVNO == "Y")
                .Select(x => x.KORISNIK_ID)
                .ToListAsync();

            var korisniciRaw = await _dbContext.KORISNICI
                .Where(x => x.AKTIVAN == "Y")
                .OrderBy(x => x.PREZIME)
                .ThenBy(x => x.IME)
                .Select(x => new
                {
                    x.ID,
                    x.IME,
                    x.PREZIME,
                    x.EMAIL,
                    x.KOMPANIJA,
                    x.ACCESS_KEY
                })
                .ToListAsync();

            List<V3RadnikProgramStatusDto> v3Statusi = new();

            if (studentFilter != "all")
            {
                if (test.Predmet == null || !test.Predmet.V3_ID_OBUKE.HasValue)
                {
                    TempData["ErrorMessage"] = "Predmet nije povezan sa V3 obukom.";
                    return RedirectToAction(nameof(Index), new { mode });
                }

                var apiResponse = await _v3MatrixApiService
                    .GetRadniciProgramStatusAsync(test.Predmet.V3_ID_OBUKE.Value);

                v3Statusi = apiResponse?.Users ?? new List<V3RadnikProgramStatusDto>();
            }

            var v3StatusByRadnikId = v3Statusi
                .GroupBy(x => x.RadnikId)
                .ToDictionary(x => x.Key, x => x.First());

            var studenti = korisniciRaw
                .Select(x =>
                {
                    V3RadnikProgramStatusDto? status = null;

                    if (x.ACCESS_KEY.HasValue)
                    {
                        v3StatusByRadnikId.TryGetValue(x.ACCESS_KEY.Value, out status);
                    }

                    var vecDodijeljen = vecDodijeljeniIds.Contains(x.ID);

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
                        else if (status.DanaDoIsteka.HasValue && status.DanaDoIsteka.Value <= 90)
                        {
                            statusObuke = "Ističe u 3 mjeseca";
                        }
                        else
                        {
                            statusObuke = "Važeće";
                        }
                    }

                    return new AssignTestStudentItemVM
                    {
                        KorisnikId = x.ID,
                        ImePrezime = x.IME + " " + x.PREZIME,
                        Email = x.EMAIL,
                        Kompanija = x.KOMPANIJA,
                        VecDodijeljen = vecDodijeljen,
                        Odabran = vecDodijeljen,

                        DatumVaziDo = datumVaziDo,
                        DanaDoIsteka = danaDoIsteka,
                        StatusObuke = statusObuke
                    };
                })
                .Where(x =>
                    studentFilter == "all"
                    || studentFilter == "allMatrix" && x.StatusObuke != "Van matrice"
                    || studentFilter == "dueSoon" && (
                        x.StatusObuke == "Nema evidenciju"
                        || x.StatusObuke == "Isteklo"
                        || x.StatusObuke == "Ističe u 3 mjeseca"
                    ))
                .ToList();

            var model = new AssignTestStudentsVM
            {
                TestId = test.ID,
                TestNaziv = test.NAZIV,
                PredmetNaziv = test.Predmet != null ? test.Predmet.NAZIV : "",
                Studenti = studenti
            };

            ViewBag.Mode = mode;
            ViewBag.StudentFilter = studentFilter;

            return View("~/Views/Shared/TestAssignment/Assign.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(AssignTestStudentsVM model, string mode = "instructor")
        {
            var test = await _dbContext.TESTOVI
                .FirstOrDefaultAsync(x => x.ID == model.TestId);

            if (test == null)
            {
                TempData["ErrorMessage"] = "Test nije pronađen.";
                return RedirectToAction(nameof(Index), new { mode });
            }

            var identityUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var isAdminMode = mode == "admin";

            if (isAdminMode && !User.IsInRole("Admin"))
            {
                TempData["ErrorMessage"] = "Nemate pravo pristupa admin prikazu.";
                return RedirectToAction(nameof(Index), new { mode = "instructor" });
            }

            if (!isAdminMode)
            {
                if (!User.IsInRole("Instruktor"))
                {
                    TempData["ErrorMessage"] = "Nemate pravo pristupa instruktor prikazu.";
                    return RedirectToAction(nameof(Index), new { mode = "admin" });
                }

                var trenutniKorisnikId = await _dbContext.KORISNICI
                    .Where(x => x.USER_ID == identityUserId)
                    .Select(x => (int?)x.ID)
                    .FirstOrDefaultAsync();

                if (trenutniKorisnikId == null)
                {
                    TempData["ErrorMessage"] = "Nije pronađen povezani korisnik za trenutno prijavljenog instruktora.";
                    return RedirectToAction(nameof(Index), new { mode });
                }

                var instruktorImaPravo = await _dbContext.PREDMET_INSTRUKTORI
                    .AnyAsync(pi =>
                        pi.PREDMET_ID == test.PREDMET_ID &&
                        pi.KORISNIK_ID == trenutniKorisnikId &&
                        pi.AKTIVNO == "Y");

                if (!instruktorImaPravo)
                {
                    TempData["ErrorMessage"] = "Nemate pravo dodjeljivati ovaj test.";
                    return RedirectToAction(nameof(Index), new { mode });
                }
            }

            var selectedStudentIds = model.SelectedStudentIds
                .Distinct()
                .ToList();

            var postojeceDodjele = await _dbContext.DODIJELJENI_TESTOVI
                .Where(x => x.TEST_ID == model.TestId)
                .ToListAsync();

            foreach (var studentId in selectedStudentIds)
            {
                var postojeca = postojeceDodjele
                    .FirstOrDefault(x => x.KORISNIK_ID == studentId);

                if (postojeca == null)
                {
                    _dbContext.DODIJELJENI_TESTOVI.Add(new DodijeljeniTest
                    {
                        TEST_ID = model.TestId,
                        KORISNIK_ID = studentId,
                        DODIJELIO_USER_ID = identityUserId,
                        DATUM_DODJELE = DateTime.Now,
                        AKTIVNO = "Y"
                    });
                }
                else if (postojeca.AKTIVNO != "Y")
                {
                    postojeca.AKTIVNO = "Y";
                    postojeca.DODIJELIO_USER_ID = identityUserId;
                    postojeca.DATUM_DODJELE = DateTime.Now;
                }
            }

            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = "Test je uspješno dodijeljen odabranim studentima.";

            return RedirectToAction(nameof(Index), new { mode });
        }
    }
}