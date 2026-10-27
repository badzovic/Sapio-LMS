
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio_LMS.Areas.Admin.Models;
using SIA.Sapio.LMS.Data.Entities;

namespace SIA.Sapio_LMS.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AssignedTestsController : BaseAdminController
    {
        private readonly ApplicationDbContext _dbContext;

        public AssignedTestsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }


        [HttpGet]
        public async Task<IActionResult> Index(
            string? search,
            string? status,
            string? nacinDodjele,
            DateTime? datumOd,
            DateTime? datumDo,
            int page = 1,
            int pageSize = 25,
            bool sporni = false)
        {
            var now = DateTime.Now;

            if (pageSize != 10 &&
                pageSize != 25 &&
                pageSize != 50 &&
                pageSize != 100)
            {
                pageSize = 25;
            }

            if (page < 1)
                page = 1;

            search = search?.Trim();

            // Sve dodjele, uključujući historijske.
            var query = _dbContext.DODIJELJENI_TESTOVI
                .AsNoTracking()
                .AsQueryable();

            // POSTOJEĆI FILTER: Pretraga
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    (x.KORISNIK.IME + " " +
                     x.KORISNIK.PREZIME).Contains(search) ||
                    x.TEST.NAZIV.Contains(search) ||
                    x.TEST.Predmet.NAZIV.Contains(search));
            }

            // POSTOJEĆI FILTER: Način dodjele
            if (nacinDodjele == "auto")
            {
                query = query.Where(x =>
                    x.AUTO_DODJELA == "Y");
            }
            else if (nacinDodjele == "manual")
            {
                query = query.Where(x =>
                    x.AUTO_DODJELA != "Y");
            }

            // POSTOJEĆI FILTER: Datum od
            if (datumOd.HasValue)
            {
                var od = datumOd.Value.Date;

                query = query.Where(x =>
                    x.DATUM_DODJELE >= od);
            }

            // POSTOJEĆI FILTER: Datum do
            if (datumDo.HasValue)
            {
                var doSljedecegDana =
                    datumDo.Value.Date.AddDays(1);

                query = query.Where(x =>
                    x.DATUM_DODJELE < doSljedecegDana);
            }

            // Podaci o pokušajima.
            // VAŽNO: Pokušaje povezujemo preko ID-a dodjele,
            // a ne preko TEST_ID i datuma.
            var evidencijaQuery = query
                .Select(x => new
                {
                    ID = x.ID,

                    TestId = x.TEST_ID,
                    KorisnikId = x.KORISNIK_ID,

                    Ime = x.KORISNIK.IME,
                    Prezime = x.KORISNIK.PREZIME,

                    PredmetNaziv = x.TEST.Predmet.NAZIV,
                    TestNaziv = x.TEST.NAZIV,

                    TestKlasaNaziv =
                        x.TEST_KLASA != null
                            ? x.TEST_KLASA.NAZIV
                            : x.TEST.TEST_KLASA != null
                                ? x.TEST.TEST_KLASA.NAZIV
                                : "-",

                    DatumDodjele = x.DATUM_DODJELE,
                    RokZaPolaganje = x.ROK_ZA_POLAGANJE,

                    AutoDodjela = x.AUTO_DODJELA,
                    DodijelioUserId = x.DODIJELIO_USER_ID,

                    StatusDodjele = x.STATUS,
                    Aktivno = x.AKTIVNO,

                    BrojDozvoljenihPokusaja =
                        x.TEST.BROJ_POKUSAJA,


                    // Postoji li novija dodjela bilo kojeg testa
                    // iz istog predmeta ovom korisniku?
                    ImaNovijuDodjelu =
                        _dbContext.DODIJELJENI_TESTOVI.Any(d =>
                            d.KORISNIK_ID == x.KORISNIK_ID &&
                            d.TEST.PREDMET_ID == x.TEST.PREDMET_ID &&
                            (
                                d.DATUM_DODJELE > x.DATUM_DODJELE ||
                                (
                                    d.DATUM_DODJELE == x.DATUM_DODJELE &&
                                    d.ID > x.ID
                                )
                            )),


                    BrojPokusaja = _dbContext.POLAGANJE_TESTA
                        .Count(p =>
                            p.DODIJELJENI_TEST_ID == x.ID &&
                            p.STATUS != "CANCELLED"),

                    ImaPolozen = _dbContext.POLAGANJE_TESTA
                        .Any(p =>
                            p.DODIJELJENI_TEST_ID == x.ID &&
                            p.IS_PASSED == "Y"),

                    ImaUToku = _dbContext.POLAGANJE_TESTA
                        .Any(p =>
                            p.DODIJELJENI_TEST_ID == x.ID &&
                            p.STATUS == "IN_PROGRESS" &&
                            p.VRIJEME_ISTEKA >= now),

                    ImaNaOcjenjivanju = _dbContext.POLAGANJE_TESTA
                        .Any(p =>
                            p.DODIJELJENI_TEST_ID == x.ID &&
                            p.STATUS == "PENDING_REVIEW"),

                    ImaNepolozen = _dbContext.POLAGANJE_TESTA
                        .Any(p =>
                            p.DODIJELJENI_TEST_ID == x.ID &&
                            p.IS_PASSED == "N"),

                    NajboljiProcenat = _dbContext.POLAGANJE_TESTA
                        .Where(p =>
                            p.DODIJELJENI_TEST_ID == x.ID &&
                            p.STATUS != "CANCELLED")
                        .Max(p => (decimal?)p.PROCENAT)
                });

            // POSTOJEĆA STATISTIKA
            // Računa se prije filtera statusa i paginacije.
            var ukupnoDodjela =
                await evidencijaQuery.CountAsync();

            var polozeno = await evidencijaQuery
                .CountAsync(x => x.ImaPolozen);

            var nijePolozeno = await evidencijaQuery
                .CountAsync(x =>
                    !x.ImaPolozen &&
                    !x.ImaUToku &&
                    !x.ImaNaOcjenjivanju &&
                    x.ImaNepolozen &&
                    (!x.RokZaPolaganje.HasValue ||
                     x.RokZaPolaganje.Value >= now));

            var rokIstekao = await evidencijaQuery
                .CountAsync(x =>
                    !x.ImaPolozen &&
                    !x.ImaUToku &&
                    !x.ImaNaOcjenjivanju &&
                    x.RokZaPolaganje.HasValue &&
                    x.RokZaPolaganje.Value < now);

            var cekaPolaganje = await evidencijaQuery
                .CountAsync(x =>
                    !x.ImaPolozen &&
                    !x.ImaUToku &&
                    !x.ImaNaOcjenjivanju &&
                    !x.ImaNepolozen &&
                    (!x.RokZaPolaganje.HasValue ||
                     x.RokZaPolaganje.Value >= now));

            // NOVO: FILTER "VIDI SPORNE"
            //
            // Uključujemo samo najnoviju dodjelu za isti
            // test i korisnika, ako nije položio i:
            //
            // 1. Rok je istekao; ili
            // 2. Iskoristio je sve dozvoljene pokušaje.
            //
            // Ne prikazujemo aktivno polaganje ni eseje
            // koji čekaju ocjenjivanje.
            if (sporni)
            {
                evidencijaQuery = evidencijaQuery
                    .Where(x =>
                        !x.ImaNovijuDodjelu &&
                        !x.ImaPolozen &&
                        !x.ImaUToku &&
                        !x.ImaNaOcjenjivanju &&
                        (
                            (
                                x.RokZaPolaganje.HasValue &&
                                x.RokZaPolaganje.Value < now
                            )
                            ||
                            (
                                x.BrojPokusaja >=
                                x.BrojDozvoljenihPokusaja
                            )
                        ));
            }

            // POSTOJEĆI FILTER STATUSA
            if (status == "passed")
            {
                evidencijaQuery = evidencijaQuery
                    .Where(x => x.ImaPolozen);
            }
            else if (status == "in_progress")
            {
                evidencijaQuery = evidencijaQuery
                    .Where(x =>
                        !x.ImaPolozen &&
                        x.ImaUToku);
            }
            else if (status == "pending_review")
            {
                evidencijaQuery = evidencijaQuery
                    .Where(x =>
                        !x.ImaPolozen &&
                        !x.ImaUToku &&
                        x.ImaNaOcjenjivanju);
            }
            else if (status == "expired")
            {
                evidencijaQuery = evidencijaQuery
                    .Where(x =>
                        !x.ImaPolozen &&
                        !x.ImaUToku &&
                        !x.ImaNaOcjenjivanju &&
                        x.RokZaPolaganje.HasValue &&
                        x.RokZaPolaganje.Value < now);
            }
            else if (status == "failed")
            {
                evidencijaQuery = evidencijaQuery
                    .Where(x =>
                        !x.ImaPolozen &&
                        !x.ImaUToku &&
                        !x.ImaNaOcjenjivanju &&
                        x.ImaNepolozen &&
                        (!x.RokZaPolaganje.HasValue ||
                         x.RokZaPolaganje.Value >= now));
            }
            else if (status == "waiting")
            {
                evidencijaQuery = evidencijaQuery
                    .Where(x =>
                        !x.ImaPolozen &&
                        !x.ImaUToku &&
                        !x.ImaNaOcjenjivanju &&
                        !x.ImaNepolozen &&
                        (!x.RokZaPolaganje.HasValue ||
                         x.RokZaPolaganje.Value >= now));
            }

            // PAGINACIJA
            var totalCount =
                await evidencijaQuery.CountAsync();

            var totalPages = (int)Math.Ceiling(
                (double)totalCount / pageSize);

            if (totalPages > 0 && page > totalPages)
                page = totalPages;

            var zapisi = await evidencijaQuery
                .OrderByDescending(x => x.DatumDodjele)
                .ThenByDescending(x => x.ID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Priprema postojećeg ViewModela
            var dodjele = zapisi
                .Select(x =>
                {
                    var statusPolaganja = "Čeka polaganje";

                    if (x.ImaPolozen)
                    {
                        statusPolaganja = "Položen";
                    }
                    else if (x.ImaUToku)
                    {
                        statusPolaganja = "U toku";
                    }
                    else if (x.ImaNaOcjenjivanju)
                    {
                        statusPolaganja = "Na ocjenjivanju";
                    }
                    else if (
                        x.RokZaPolaganje.HasValue &&
                        x.RokZaPolaganje.Value < now)
                    {
                        statusPolaganja = "Rok istekao";
                    }
                    else if (
                        x.BrojPokusaja >=
                        x.BrojDozvoljenihPokusaja &&
                        !x.ImaPolozen)
                    {
                        statusPolaganja = "Nije položen";
                    }
                    else if (x.ImaNepolozen)
                    {
                        statusPolaganja = "Nije položen";
                    }

                    return new AssignedTestItemVM
                    {
                        ID = x.ID,

                        TestId = x.TestId,
                        KorisnikId = x.KorisnikId,

                        ImePrezime =
                            (x.Ime + " " + x.Prezime).Trim(),

                        PredmetNaziv =
                            x.PredmetNaziv ?? "-",

                        TestNaziv =
                            x.TestNaziv ?? "-",

                        TestKlasaNaziv =
                            x.TestKlasaNaziv ?? "-",

                        DatumDodjele = x.DatumDodjele,
                        RokZaPolaganje = x.RokZaPolaganje,

                        NacinDodjele =
                            x.AutoDodjela == "Y"
                                ? "Automatska"
                                : "Ručna",

                        DodijelioUserId = x.DodijelioUserId,

                        StatusDodjele =
                            x.StatusDodjele ?? "-",

                        StatusPolaganja = statusPolaganja,

                        BrojPokusaja = x.BrojPokusaja,
                        NajboljiProcenat =
                            x.NajboljiProcenat,

                        Aktivno = x.Aktivno == "Y"
                    };
                })
                .ToList();

            var model = new AssignedTestsIndexVM
            {
                Dodjele = dodjele,

                Search = search,
                Status = status,
                NacinDodjele = nacinDodjele,
                DatumOd = datumOd,
                DatumDo = datumDo,

                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,

                UkupnoDodjela = ukupnoDodjela,
                CekaPolaganje = cekaPolaganje,
                RokIstekao = rokIstekao,
                Polozeno = polozeno,
                NijePolozeno = nijePolozeno
            };

            ViewBag.Sporni = sporni;

            return View(model);
        }


        [HttpGet]
        public async Task<IActionResult> Polaganja(
            string? search,
            string? rezultat,
            DateTime? datumOd,
            DateTime? datumDo,
            int page = 1,
            int pageSize = 25)
        {
            if (pageSize != 10 &&
                pageSize != 25 &&
                pageSize != 50 &&
                pageSize != 100)
            {
                pageSize = 25;
            }

            if (page < 1)
                page = 1;

            search = search?.Trim();

            // Sva polaganja, uključujući historijska.
            // Jedan red predstavlja jedan konkretan pokušaj.

            var query = _dbContext.POLAGANJE_TESTA
                .AsNoTracking()
                .AsQueryable();

            // PRETRAGA
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    (x.Korisnik!.IME + " " +
                     x.Korisnik.PREZIME).Contains(search) ||
                    x.Test!.NAZIV.Contains(search) ||
                    x.Test.Predmet!.NAZIV.Contains(search));
            }

            // REZULTAT POLAGANJA
            if (rezultat == "passed")
            {
                query = query.Where(x => x.IS_PASSED == "Y");
            }
            else if (rezultat == "failed")
            {
                query = query.Where(x => x.IS_PASSED == "N");
            }
            else if (rezultat == "pending_review")
            {
                query = query.Where(x => x.STATUS == "PENDING_REVIEW");
            }
            else if (rezultat == "in_progress")
            {
                query = query.Where(x => x.STATUS == "IN_PROGRESS");
            }

            // DATUM ZAVRŠETKA OD
            if (datumOd.HasValue)
            {
                var od = datumOd.Value.Date;

                query = query.Where(x =>
                    x.DATUM_ZAVRSETKA.HasValue &&
                    x.DATUM_ZAVRSETKA.Value >= od);
            }

            // DATUM ZAVRŠETKA DO
            if (datumDo.HasValue)
            {
                var doSljedecegDana = datumDo.Value.Date.AddDays(1);

                query = query.Where(x =>
                    x.DATUM_ZAVRSETKA.HasValue &&
                    x.DATUM_ZAVRSETKA.Value < doSljedecegDana);
            }

            // PAGINACIJA
            var totalCount = await query.CountAsync();

            var totalPages = (int)Math.Ceiling(
                (double)totalCount / pageSize);

            if (totalPages > 0 && page > totalPages)
                page = totalPages;

            // DOHVAT POLAGANJA
            var polaganja = await query
                .OrderByDescending(x =>
                    x.DATUM_ZAVRSETKA ?? x.DATUM_POCETKA)
                .ThenByDescending(x => x.ID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AssignedTestAttemptItemVM
                {
                    PolaganjeId = x.ID,
                    DodjelaId = x.DODIJELJENI_TEST_ID,

                    KorisnikId = x.KORISNIK_ID,
                    TestId = x.TEST_ID,

                    ImePrezime =
                        (x.Korisnik!.IME + " " +
                         x.Korisnik.PREZIME).Trim(),

                    PredmetNaziv = x.Test!.Predmet != null
                        ? x.Test.Predmet.NAZIV
                        : "-",

                    TestNaziv = x.Test.NAZIV,

                    RedniBrojPokusaja = x.REDNI_BROJ_POKUSAJA,

                    DatumPocetka = x.DATUM_POCETKA,
                    DatumZavrsetka = x.DATUM_ZAVRSETKA,

                    Status = x.STATUS,
                    IsPassed = x.IS_PASSED,

                    Procenat = x.PROCENAT,
                    OstvareniBodovi = x.OSTVARENI_BODOVI,
                    MaxBodovi = x.MAX_BODOVI,

                    AutoSubmitted = x.AUTO_SUBMITTED == "Y"
                })
                .ToListAsync();

            var model = new AssignedTestAttemptsIndexVM
            {
                Polaganja = polaganja,

                Search = search,
                Rezultat = rezultat,
                DatumOd = datumOd,
                DatumDo = datumDo,

                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return View(model);
        }


        [HttpGet]
        public async Task<IActionResult> Answers(int id, int? polaganjeId)
        {
            // 1. Dohvatanje konkretne dodjele
            var dodjela = await _dbContext.DODIJELJENI_TESTOVI
                .AsNoTracking()
                .Include(x => x.KORISNIK)
                .Include(x => x.TEST)
                    .ThenInclude(x => x.Predmet)
                .Include(x => x.TEST)
                    .ThenInclude(x => x.TEST_KLASA)
                .Include(x => x.TEST_KLASA)
                .FirstOrDefaultAsync(x => x.ID == id);

            if (dodjela == null)
            {
                TempData["ErrorMessage"] = "Dodjela testa nije pronađena.";
                return RedirectToAction(nameof(Index));
            }


            // 2. Dohvat pokušaja konkretne dodjele testa.
            // Povezivanje isključivo preko DODIJELJENI_TEST_ID,
            // kako se pokušaji različitih dodjela ne bi miješali.

            var pokusajiQuery = _dbContext.POLAGANJE_TESTA
                .AsNoTracking()
                .Where(x =>
                    x.DODIJELJENI_TEST_ID == dodjela.ID &&
                    x.KORISNIK_ID == dodjela.KORISNIK_ID);


            var pokusaji = await pokusajiQuery
                .OrderByDescending(x => x.DATUM_POCETKA)
                .ThenByDescending(x => x.ID)
                .Select(x => new AssignedTestAttemptVM
                {
                    ID = x.ID,
                    RedniBrojPokusaja = x.REDNI_BROJ_POKUSAJA,

                    DatumPocetka = x.DATUM_POCETKA,
                    DatumZavrsetka = x.DATUM_ZAVRSETKA,

                    Status = x.STATUS,

                    OstvareniBodovi = x.OSTVARENI_BODOVI,
                    MaxBodovi = x.MAX_BODOVI,
                    Procenat = x.PROCENAT,

                    IsPassed = x.IS_PASSED,
                    AutoSubmitted = x.AUTO_SUBMITTED == "Y"
                })
                .ToListAsync();

            // 4. Glavni ViewModel
            var model = new AssignedTestAnswersVM
            {
                DodjelaId = dodjela.ID,
                TestId = dodjela.TEST_ID,
                KorisnikId = dodjela.KORISNIK_ID,

                ImePrezime =
                    $"{dodjela.KORISNIK.IME} {dodjela.KORISNIK.PREZIME}".Trim(),

                PredmetNaziv = dodjela.TEST.Predmet.NAZIV,
                TestNaziv = dodjela.TEST.NAZIV,

                TestKlasaNaziv =
                    dodjela.TEST_KLASA?.NAZIV
                    ?? dodjela.TEST.TEST_KLASA?.NAZIV
                    ?? "-",

                DatumDodjele = dodjela.DATUM_DODJELE,
                RokZaPolaganje = dodjela.ROK_ZA_POLAGANJE,

                Pokusaji = pokusaji
            };

            // Nema pokušaja - View će prikazati obavještenje.
            if (!pokusaji.Any())
            {
                return View(model);
            }

            // 5. Izbor pokušaja
            var odabraniPokusaj = polaganjeId.HasValue
                ? pokusaji.FirstOrDefault(x => x.ID == polaganjeId.Value)
                : pokusaji.FirstOrDefault();

            if (odabraniPokusaj == null)
            {
                return NotFound(
                    "Odabrano polaganje ne pripada ovoj dodjeli testa.");
            }

            model.OdabranoPolaganjeId = odabraniPokusaj.ID;

            // 6. Dohvatanje pitanja konkretnog pokušaja
            var pitanja = await _dbContext.Set<PolaganjePitanja>()
                .AsNoTracking()
                .Where(x =>
                    x.POLAGANJE_TESTA_ID == odabraniPokusaj.ID)
                .Include(x => x.TestPitanje)
                    .ThenInclude(x => x.TipPitanja)
                .Include(x => x.TestPitanje)
                    .ThenInclude(x => x.TipTezinePitanja)
                .OrderBy(x => x.REDOSLIJED)
                .ToListAsync();

            foreach (var pitanje in pitanja)
            {
                var testPitanje = pitanje.TestPitanje;

                if (testPitanje == null)
                    continue;

                // 7. Ponuđene opcije u redoslijedu
                // konkretnog pokušaja.
                var ponudjeneOpcije = await _dbContext.Set<PolaganjeOdgovori>()
                    .AsNoTracking()
                    .Where(x =>
                        x.POLAGANJE_PITANJE_ID == pitanje.ID)
                    .Include(x => x.TestPitanjeOdgovor)
                    .OrderBy(x => x.REDOSLIJED)
                    .ToListAsync();

                // 8. Stvarni odgovori studenta
                var studentskiOdgovori = await _dbContext.Set<StudentOdgovor>()
                    .AsNoTracking()
                    .Where(x =>
                        x.POLAGANJE_TESTA_ID == odabraniPokusaj.ID &&
                        x.POLAGANJE_PITANJE_ID == pitanje.ID)
                    .OrderBy(x => x.DATUM_ODGOVORA)
                    .ToListAsync();

                var odgovorSaBodovima = studentskiOdgovori
                    .FirstOrDefault(x => x.OSTVARENI_BODOVI.HasValue);

                var odgovorSaOcjenom = studentskiOdgovori
                    .FirstOrDefault(x => x.IS_TACAN != null);

                var essayOdgovor = studentskiOdgovori
                    .FirstOrDefault(x =>
                        !string.IsNullOrWhiteSpace(x.ESSAY_ODGOVOR));

                var pitanjeVM = new AssignedTestQuestionVM
                {
                    ID = pitanje.ID,
                    TestPitanjeId = testPitanje.ID,

                    Redoslijed = pitanje.REDOSLIJED,

                    TekstPitanja = testPitanje.TEKST_PITANJA,

                    TipPitanja =
                        testPitanje.TipPitanja?.NAZIV ?? "-",

                    TezinaPitanja =
                        testPitanje.TipTezinePitanja?.NAZIV ?? "-",

                    Bodovi = testPitanje.BODOVI,
                    NegativniBodovi = testPitanje.NEGATIVNI_BODOVI,

                    ParcijalnoBodovanje =
                        testPitanje.PARCIJALNO_BODOVANJE == "Y",

                    Obavezno = testPitanje.IS_OBAVEZNO == "Y",

                    SlikaPutanja = testPitanje.SLIKA_PUTANJA,
                    SlikaNaziv = testPitanje.SLIKA_NAZIV,

                    EsejskiOdgovor = essayOdgovor?.ESSAY_ODGOVOR,

                    OstvareniBodovi =
                        odgovorSaBodovima?.OSTVARENI_BODOVI,

                    IsTacan = odgovorSaOcjenom?.IS_TACAN == "Y"
                        ? true
                        : odgovorSaOcjenom?.IS_TACAN == "N"
                            ? false
                            : null
                };

                // 9. Priprema ponuđenih i odabranih opcija
                foreach (var opcija in ponudjeneOpcije)
                {
                    var odgovor = opcija.TestPitanjeOdgovor;

                    if (odgovor == null)
                        continue;

                    var odabran = studentskiOdgovori
                        .Any(x =>
                            x.TEST_PITANJE_ODGOVOR_ID ==
                            opcija.TEST_PITANJE_ODGOVOR_ID);

                    pitanjeVM.Odgovori.Add(new AssignedTestOptionVM
                    {
                        ID = odgovor.ID,
                        Redoslijed = opcija.REDOSLIJED,

                        TekstOdgovora = odgovor.TEKST_ODGOVORA,

                        IsTacan = odgovor.IS_TACAN == "Y",
                        IsSelected = odabran,

                        Bodovi = odgovor.BODOVI,
                        NegativniBodovi = odgovor.NEGATIVNI_BODOVI
                    });
                }

                model.Pitanja.Add(pitanjeVM);
            }

            return View(model);
        }

    }
}