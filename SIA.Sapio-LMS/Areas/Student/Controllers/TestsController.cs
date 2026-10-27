using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio_LMS.Areas.Student.Models;

namespace SIA.Sapio_LMS.Areas.Student.Controllers
{
    [Area("Student")]
    public class TestsController : BaseStudentController
    {
        private readonly ApplicationDbContext _dbContext;

        public TestsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }




        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;

            var korisnik = await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x => x.USER_ID == CurrentUserId);

            if (korisnik == null)
            {
                return View(new List<StudentAssignedTestVM>());
            }

            // Dohvat svih aktivnih dodjela korisnika.
            // Ne filtriramo istekle rokove jer ih želimo prikazati studentu.
            var dodijeljeniTestovi = await _dbContext.DODIJELJENI_TESTOVI
                .AsNoTracking()
                .Where(x =>
                    x.KORISNIK_ID == korisnik.ID &&
                    x.AKTIVNO == "Y")
                .Select(x => new StudentAssignedTestVM
                {
                    // ID konkretne dodjele
                    DodjelaId = x.ID,

                    TestId = x.TEST_ID,
                    NazivTesta = x.TEST.NAZIV,
                    Opis = x.TEST.OPIS,

                    Predmet = x.TEST.Predmet != null
                        ? x.TEST.Predmet.NAZIV
                        : null,

                    DatumOd = x.TEST.DATUM_OD,
                    DatumDo = x.TEST.DATUM_DO,

                    RokZaPolaganje = x.ROK_ZA_POLAGANJE,

                    VrijemeTrajanjaMin = x.TEST.VRIJEME_TRAJANJA_MIN,
                    BrojPokusajaDozvoljeno = x.TEST.BROJ_POKUSAJA,

                    // Brojimo pokušaje samo ove dodjele
                    BrojPokusajaIskoristeno = _dbContext.POLAGANJE_TESTA
                        .Count(p =>
                            p.DODIJELJENI_TEST_ID == x.ID &&
                            p.KORISNIK_ID == korisnik.ID &&
                            p.STATUS != "CANCELLED"),

                    MozePolagati = false
                })
                .ToListAsync();

            // Dohvat svih polaganja za ovog korisnika.
            var polaganja = await _dbContext.POLAGANJE_TESTA
                .AsNoTracking()
                .Where(x =>
                    x.KORISNIK_ID == korisnik.ID)
                .Select(x => new
                {
                    x.DODIJELJENI_TEST_ID,
                    x.TEST_ID,
                    x.STATUS,
                    x.IS_PASSED,
                    x.VRIJEME_ISTEKA
                })
                .ToListAsync();

            foreach (var test in dodijeljeniTestovi)
            {
                // Pokušaji koji pripadaju konkretnoj dodjeli
                var polaganjaDodjele = polaganja
                    .Where(x => x.DODIJELJENI_TEST_ID == test.DodjelaId)
                    .ToList();

                var imaAktivnoPolaganje = polaganjaDodjele.Any(x =>
                    x.STATUS == "IN_PROGRESS" &&
                    x.VRIJEME_ISTEKA >= now);

                var vecPolozio = polaganjaDodjele.Any(x =>
                    x.IS_PASSED == "Y");

                var naOcjenjivanju = polaganjaDodjele.Any(x =>
                    x.STATUS == "PENDING_REVIEW");

                var individualniRokIstekao =
                    test.RokZaPolaganje.HasValue &&
                    test.RokZaPolaganje.Value < now;

                var periodTestaIstekao =
                    test.DatumDo.HasValue &&
                    test.DatumDo.Value < now;

                var testJosNijeDostupan =
                    test.DatumOd.HasValue &&
                    test.DatumOd.Value > now;

                var nemaPokusaja =
                    test.BrojPokusajaPreostalo <= 0;

                // Uspješno završena dodjela
                if (vecPolozio)
                {
                    test.MozePolagati = false;
                    test.StatusDostupnosti = "Položen";
                    test.RazlogNedostupnosti =
                        "Test je već uspješno položen.";
                }
                // Polaganje koje je već započeto
                else if (imaAktivnoPolaganje)
                {
                    test.MozePolagati = true;
                    test.StatusDostupnosti = "U toku";
                    test.RazlogNedostupnosti = null;
                }
                // Čekanje ocjenjivanja eseja
                else if (naOcjenjivanju)
                {
                    test.MozePolagati = false;
                    test.StatusDostupnosti = "Na ocjenjivanju";
                    test.RazlogNedostupnosti =
                        "Rezultat testa još nije konačan.";
                }
                else if (individualniRokIstekao)
                {
                    test.MozePolagati = false;
                    test.StatusDostupnosti = "Rok istekao";
                    test.RazlogNedostupnosti =
                        "Istekao je individualni rok za polaganje testa.";
                }
                else if (periodTestaIstekao)
                {
                    test.MozePolagati = false;
                    test.StatusDostupnosti = "Test nije dostupan";
                    test.RazlogNedostupnosti =
                        "Period dostupnosti ovog testa je završen.";
                }
                else if (testJosNijeDostupan)
                {
                    test.MozePolagati = false;
                    test.StatusDostupnosti = "Test nije dostupan";
                    test.RazlogNedostupnosti =
                        "Test trenutno nije dostupan za polaganje.";
                }
                else if (nemaPokusaja)
                {
                    test.MozePolagati = false;
                    test.StatusDostupnosti = "Nema pokušaja";
                    test.RazlogNedostupnosti =
                        "Iskorišteni su svi dozvoljeni pokušaji.";
                }
                else
                {
                    test.MozePolagati = true;
                    test.StatusDostupnosti = "Dostupno";
                    test.RazlogNedostupnosti = null;
                }
            }

            return View(dodijeljeniTestovi
                .OrderBy(x => x.RokZaPolaganje ?? DateTime.MaxValue)
                .ThenBy(x => x.NazivTesta)
                .ToList());
        }






        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int id)
        {
            var now = DateTime.Now;

            var korisnik = await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x => x.USER_ID == CurrentUserId);

            if (korisnik == null)
            {
                TempData["ErrorMessage"] =
                    "Nije pronađen LMS korisnik za trenutno prijavljenog studenta.";

                return RedirectToAction(nameof(Index));
            }

            // Parametar id predstavlja DODIJELJENI_TESTOVI.ID.
            // Pronalazimo samo aktivnu dodjelu trenutnog korisnika.
            var dodjela = await _dbContext.DODIJELJENI_TESTOVI
                .Include(x => x.TEST)
                .FirstOrDefaultAsync(x =>
                    x.ID == id &&
                    x.KORISNIK_ID == korisnik.ID &&
                    x.AKTIVNO == "Y");

            if (dodjela == null || dodjela.TEST == null)
            {
                TempData["ErrorMessage"] =
                    "Dodjela testa nije pronađena ili više nije aktivna.";

                return RedirectToAction(nameof(Index));
            }

            var test = dodjela.TEST;

            // Provjera da li je test aktivan.
            if (test.AKTIVNO != "Y" || test.STATUS != "ACTIVE")
            {
                TempData["ErrorMessage"] =
                    "Test trenutno nije aktivan.";

                return RedirectToAction(nameof(Index));
            }

            // Sva polaganja koja pripadaju ovoj konkretnoj dodjeli.
            var polaganjaDodjele = await _dbContext.POLAGANJE_TESTA
                .Where(x =>
                    x.DODIJELJENI_TEST_ID == dodjela.ID &&
                    x.KORISNIK_ID == korisnik.ID)
                .OrderByDescending(x => x.DATUM_POCETKA)
                .ToListAsync();

            // Ako je test položen, ne dozvoljavamo novo polaganje
            // u okviru iste dodjele.
            if (polaganjaDodjele.Any(x => x.IS_PASSED == "Y"))
            {
                TempData["ErrorMessage"] =
                    "Ovaj test ste već uspješno položili.";

                return RedirectToAction(nameof(Index));
            }

            // Ako se čeka ocjenjivanje eseja, novi pokušaj nije dozvoljen.
            if (polaganjaDodjele.Any(x => x.STATUS == "PENDING_REVIEW"))
            {
                TempData["ErrorMessage"] =
                    "Vaš prethodni pokušaj još čeka ocjenjivanje.";

                return RedirectToAction(nameof(Index));
            }

            // Provjera postojećeg započetog polaganja ove dodjele.
            var aktivnoPolaganje = polaganjaDodjele
                .FirstOrDefault(x => x.STATUS == "IN_PROGRESS");

            if (aktivnoPolaganje != null)
            {
                // Ako je vrijeme pokušaja isteklo, završavamo ga.
                if (now > aktivnoPolaganje.VRIJEME_ISTEKA)
                {
                    aktivnoPolaganje.STATUS = "TIME_EXPIRED";
                    aktivnoPolaganje.DATUM_ZAVRSETKA = now;
                    aktivnoPolaganje.AUTO_SUBMITTED = "Y";
                    aktivnoPolaganje.DATUM_IZMJENE = now;

                    await _dbContext.SaveChangesAsync();

                    TempData["ErrorMessage"] =
                        "Vrijeme za prethodno započeto polaganje je isteklo.";

                    return RedirectToAction(nameof(Index));
                }

                // Nastavak prethodno započetog pokušaja.
                // Individualni rok dodjele ne prekida pokušaj
                // koji je započet prije isteka tog roka.
                var imaPitanja = await _dbContext.POLAGANJE_PITANJA
                    .AnyAsync(x =>
                        x.POLAGANJE_TESTA_ID == aktivnoPolaganje.ID);

                if (!imaPitanja)
                {
                    await GeneratePolaganjeQuestionsAsync(
                        aktivnoPolaganje.ID,
                        test.ID,
                        test.RANDOM_PITANJA,
                        test.RANDOM_ODGOVORI
                    );
                }

                return RedirectToAction(
                    nameof(Take),
                    new { id = aktivnoPolaganje.ID });
            }

            // Provjera individualnog roka dodjele.
            if (dodjela.ROK_ZA_POLAGANJE.HasValue &&
                now > dodjela.ROK_ZA_POLAGANJE.Value)
            {
                TempData["ErrorMessage"] =
                    "Rok za polaganje ovog testa istekao je " +
                    dodjela.ROK_ZA_POLAGANJE.Value.ToString("dd.MM.yyyy HH:mm") +
                    ".";

                return RedirectToAction(nameof(Index));
            }

            // Provjera perioda dostupnosti testa.
            if (test.DATUM_OD > now || test.DATUM_DO < now)
            {
                TempData["ErrorMessage"] =
                    "Test nije u aktivnom periodu za polaganje.";

                return RedirectToAction(nameof(Index));
            }

            // Broj pokušaja računamo isključivo za ovu dodjelu.
            var brojPokusaja = polaganjaDodjele
                .Count(x => x.STATUS != "CANCELLED");

            if (brojPokusaja >= test.BROJ_POKUSAJA)
            {
                TempData["ErrorMessage"] =
                    "Iskoristili ste maksimalan broj pokušaja za ovu dodjelu testa.";

                return RedirectToAction(nameof(Index));
            }

            // Kreiranje novog pokušaja povezanog s dodjelom.
            var polaganje = new PolaganjeTesta
            {
                DODIJELJENI_TEST_ID = dodjela.ID,
                TEST_ID = test.ID,
                KORISNIK_ID = korisnik.ID,

                DATUM_POCETKA = now,
                VRIJEME_ISTEKA = now.AddMinutes(test.VRIJEME_TRAJANJA_MIN),

                STATUS = "IN_PROGRESS",
                REDNI_BROJ_POKUSAJA = brojPokusaja + 1,

                DATUM_KREIRANJA = now
            };

            _dbContext.POLAGANJE_TESTA.Add(polaganje);

            await _dbContext.SaveChangesAsync();

            // Generisanje pitanja za novi pokušaj.
            await GeneratePolaganjeQuestionsAsync(
                polaganje.ID,
                test.ID,
                test.RANDOM_PITANJA,
                test.RANDOM_ODGOVORI
            );

            return RedirectToAction(
                nameof(Take),
                new { id = polaganje.ID });
        }



        public async Task<IActionResult> Take(int id)
        {
            var now = DateTime.Now;

            var korisnik = await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x => x.USER_ID == CurrentUserId);

            if (korisnik == null)
                return RedirectToAction(nameof(Index));

            var polaganje = await _dbContext.POLAGANJE_TESTA
                .Include(x => x.Test)
                .ThenInclude(x => x.Predmet)
                .FirstOrDefaultAsync(x =>
                    x.ID == id &&
                    x.KORISNIK_ID == korisnik.ID);

            if (polaganje == null)
                return NotFound();

            if (polaganje.STATUS != "IN_PROGRESS")
            {
                //TempData["ErrorMessage"] = "Ovo polaganje više nije aktivno.";
                return RedirectToAction(nameof(Index));
            }

            if (now > polaganje.VRIJEME_ISTEKA)
            {
                polaganje.STATUS = "TIME_EXPIRED";
                polaganje.DATUM_ZAVRSETKA = now;
                polaganje.AUTO_SUBMITTED = "Y";

                await _dbContext.SaveChangesAsync();

                //TempData["ErrorMessage"] = "Vrijeme za polaganje je isteklo.";
                return RedirectToAction(nameof(Index));
            }

            var polaganjePitanja = await _dbContext.POLAGANJE_PITANJA
                .Where(x => x.POLAGANJE_TESTA_ID == polaganje.ID)
                .OrderBy(x => x.REDOSLIJED)
                .ToListAsync();

            var testPitanja = await _dbContext.TEST_PITANJA
                .Include(x => x.TipPitanja)
                .Where(x => x.TEST_ID == polaganje.TEST_ID)
                .ToListAsync();

            var polaganjeOdgovori = await (
                from pp in _dbContext.POLAGANJE_PITANJA
                join po in _dbContext.POLAGANJE_ODGOVORI
                    on pp.ID equals po.POLAGANJE_PITANJE_ID
                where pp.POLAGANJE_TESTA_ID == polaganje.ID
                orderby po.POLAGANJE_PITANJE_ID, po.REDOSLIJED
                select po
            ).ToListAsync();

            var testOdgovori = await (
                from tp in _dbContext.TEST_PITANJA
                join to in _dbContext.TEST_PITANJE_ODGOVORI
                    on tp.ID equals to.TEST_PITANJE_ID
                where tp.TEST_ID == polaganje.TEST_ID
                select to
            ).ToListAsync();

            var testPitanjaDict = testPitanja.ToDictionary(x => x.ID);
            var testOdgovoriDict = testOdgovori.ToDictionary(x => x.ID);

            var pitanjaVm = new List<TakeTestQuestionVM>();

            foreach (var pp in polaganjePitanja)
            {
                testPitanjaDict.TryGetValue(pp.TEST_PITANJE_ID, out var testPitanje);

                var pitanjeVm = new TakeTestQuestionVM
                {
                    PolaganjePitanjeId = pp.ID,
                    TestPitanjeId = pp.TEST_PITANJE_ID,
                    TekstPitanja = testPitanje?.TEKST_PITANJA ?? "",

                    SlikaPutanja = testPitanje?.SLIKA_PUTANJA,
                    SlikaNaziv = testPitanje?.SLIKA_NAZIV,

                    TipPitanjaSifra = testPitanje?.TipPitanja?.SIFRA ?? "",
                    Redoslijed = pp.REDOSLIJED,
                    Odgovori = new List<TakeTestAnswerVM>()
                };

                var odgovoriZaPitanje = polaganjeOdgovori
                    .Where(o => o.POLAGANJE_PITANJE_ID == pp.ID)
                    .OrderBy(o => o.REDOSLIJED)
                    .ToList();

                foreach (var po in odgovoriZaPitanje)
                {
                    testOdgovoriDict.TryGetValue(po.TEST_PITANJE_ODGOVOR_ID, out var testOdgovor);

                    pitanjeVm.Odgovori.Add(new TakeTestAnswerVM
                    {
                        PolaganjeOdgovorId = po.ID,
                        TestPitanjeOdgovorId = po.TEST_PITANJE_ODGOVOR_ID,
                        TekstOdgovora = testOdgovor?.TEKST_ODGOVORA ?? "",
                        Redoslijed = po.REDOSLIJED
                    });
                }

                pitanjaVm.Add(pitanjeVm);
            }

            var model = new TakeTestVM
            {
                PolaganjeId = polaganje.ID,
                TestId = polaganje.TEST_ID,
                NazivTesta = polaganje.Test?.NAZIV ?? "Test",
                Opis = polaganje.Test?.OPIS,
                VrijemeIsteka = polaganje.VRIJEME_ISTEKA,
                Pitanja = pitanjaVm,
                PredmetNaziv = polaganje.Test?.Predmet?.NAZIV ?? "",
            };

            return View(model);
        }

        private async Task GeneratePolaganjeQuestionsAsync(
            int polaganjeTestaId,
            int testId,
            string? randomPitanja,
            string? randomOdgovori)
        {
            var testPitanja = await _dbContext.TEST_PITANJA
                .Where(x => x.TEST_ID == testId)
                .ToListAsync();

            testPitanja = randomPitanja == "Y"
                ? testPitanja.OrderBy(x => Guid.NewGuid()).ToList()
                : testPitanja.OrderBy(x => x.REDOSLIJED).ToList();

            var polaganjePitanja = new List<PolaganjePitanja>();

            var redoslijedPitanja = 1;

            foreach (var testPitanje in testPitanja)
            {
                polaganjePitanja.Add(new PolaganjePitanja
                {
                    POLAGANJE_TESTA_ID = polaganjeTestaId,
                    TEST_PITANJE_ID = testPitanje.ID,
                    REDOSLIJED = redoslijedPitanja++,
                    DATUM_KREIRANJA = DateTime.Now
                });
            }

            _dbContext.POLAGANJE_PITANJA.AddRange(polaganjePitanja);
            await _dbContext.SaveChangesAsync();

            foreach (var polaganjePitanje in polaganjePitanja)
            {
                var testOdgovori = await _dbContext.TEST_PITANJE_ODGOVORI
                    .Where(x => x.TEST_PITANJE_ID == polaganjePitanje.TEST_PITANJE_ID)
                    .ToListAsync();

                testOdgovori = randomOdgovori == "Y"
                    ? testOdgovori.OrderBy(x => Guid.NewGuid()).ToList()
                    : testOdgovori.OrderBy(x => x.REDOSLIJED).ToList();

                var redoslijedOdgovora = 1;

                foreach (var testOdgovor in testOdgovori)
                {
                    _dbContext.POLAGANJE_ODGOVORI.Add(new PolaganjeOdgovori
                    {
                        POLAGANJE_PITANJE_ID = polaganjePitanje.ID,
                        TEST_PITANJE_ODGOVOR_ID = testOdgovor.ID,
                        REDOSLIJED = redoslijedOdgovora++
                    });
                }
            }

            await _dbContext.SaveChangesAsync();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int id)
        {
            var now = DateTime.Now;

            var korisnik = await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x => x.USER_ID == CurrentUserId);

            if (korisnik == null)
                return RedirectToAction(nameof(Index));

            var polaganje = await _dbContext.POLAGANJE_TESTA
                .Include(x => x.Test)
                .FirstOrDefaultAsync(x =>
                    x.ID == id &&
                    x.KORISNIK_ID == korisnik.ID);

            if (polaganje == null)
                return NotFound();

            if (polaganje.STATUS != "IN_PROGRESS")
                return RedirectToAction(nameof(Result), new { id = polaganje.ID });

            var autoSubmitted = now > polaganje.VRIJEME_ISTEKA;

            var pitanja = await _dbContext.POLAGANJE_PITANJA
                .Include(x => x.TestPitanje)
                    .ThenInclude(x => x.TipPitanja)
                .Where(x => x.POLAGANJE_TESTA_ID == polaganje.ID)
                .OrderBy(x => x.REDOSLIJED)
                .ToListAsync();

            var imaEsejskaPitanja = pitanja.Any(x =>
                x.TestPitanje != null &&
                x.TestPitanje.TipPitanja != null &&
                (x.TestPitanje.TipPitanja.SIFRA ?? "")
                    .Trim()
                    .ToUpper() == "ESSAY");

            var polaganjeOdgovori = await (
                from pp in _dbContext.POLAGANJE_PITANJA
                join po in _dbContext.POLAGANJE_ODGOVORI
                    on pp.ID equals po.POLAGANJE_PITANJE_ID
                where pp.POLAGANJE_TESTA_ID == polaganje.ID
                select po
            ).ToListAsync();

            var testOdgovori = await (
                from tp in _dbContext.TEST_PITANJA
                join odg in _dbContext.TEST_PITANJE_ODGOVORI
                    on tp.ID equals odg.TEST_PITANJE_ID
                where tp.TEST_ID == polaganje.TEST_ID
                select odg
            ).ToListAsync();

            var stariOdgovori = await _dbContext.STUDENT_ODGOVORI
                .Where(x => x.POLAGANJE_TESTA_ID == polaganje.ID)
                .ToListAsync();

            if (stariOdgovori.Any())
                _dbContext.STUDENT_ODGOVORI.RemoveRange(stariOdgovori);

            decimal ostvareniBodovi = 0;
            decimal maxBodovi = 0;

            foreach (var pitanje in pitanja)
            {
                var testPitanje = pitanje.TestPitanje;

                if (testPitanje == null)
                    continue;

                maxBodovi += testPitanje.BODOVI;

                var tip = testPitanje.TipPitanja?.SIFRA?.Trim().ToUpper() ?? "";
                var parcijalnoBodovanje = (testPitanje.PARCIJALNO_BODOVANJE ?? "N").Trim().ToUpper() == "Y";

                var selectedKey = $"answers[{pitanje.ID}]";
                var essayKey = $"essayAnswers[{pitanje.ID}]";

                var odabraniOdgovori = Request.Form[selectedKey]
                    .Select(x => int.TryParse(x, out var value) ? value : 0)
                    .Where(x => x > 0)
                    .Distinct()
                    .ToList();

                var essayOdgovor = Request.Form[essayKey].FirstOrDefault();

                var dozvoljeniOdgovoriZaPitanje = polaganjeOdgovori
                    .Where(x => x.POLAGANJE_PITANJE_ID == pitanje.ID)
                    .Select(x => x.TEST_PITANJE_ODGOVOR_ID)
                    .ToList();

                var odgovoriZaPitanje = testOdgovori
                    .Where(x => dozvoljeniOdgovoriZaPitanje.Contains(x.ID))
                    .ToList();

                var tacniOdgovori = odgovoriZaPitanje
                    .Where(x => x.IS_TACAN == "Y")
                    .Select(x => x.ID)
                    .ToList();

                var validniOdabraniOdgovori = odabraniOdgovori
                    .Where(x => dozvoljeniOdgovoriZaPitanje.Contains(x))
                    .ToList();

                decimal bodoviZaPitanje = 0;

                if (tip == "ESSAY")
                {
                    _dbContext.STUDENT_ODGOVORI.Add(new StudentOdgovor
                    {
                        POLAGANJE_TESTA_ID = polaganje.ID,
                        POLAGANJE_PITANJE_ID = pitanje.ID,
                        TEST_PITANJE_ODGOVOR_ID = null,
                        ESSAY_ODGOVOR = essayOdgovor,
                        IS_TACAN = null,
                        OSTVARENI_BODOVI = null,
                        DATUM_ODGOVORA = now
                    });

                    continue;
                }

                if (tip == "SINGLE_CHOICE" || tip == "SINGLE")
                {
                    var isCorrect =
                        validniOdabraniOdgovori.Count == 1 &&
                        tacniOdgovori.Count == 1 &&
                        validniOdabraniOdgovori.First() == tacniOdgovori.First();

                    if (isCorrect)
                        bodoviZaPitanje = testPitanje.BODOVI;
                }
                else if (tip == "MULTIPLE_CHOICE" || tip == "MULTIPLE")
                {
                    if (!parcijalnoBodovanje)
                    {
                        var sveTacno = validniOdabraniOdgovori
                            .OrderBy(x => x)
                            .SequenceEqual(tacniOdgovori.OrderBy(x => x));

                        if (sveTacno)
                            bodoviZaPitanje = testPitanje.BODOVI;
                    }
                    else
                    {
                        foreach (var odgovorId in validniOdabraniOdgovori)
                        {
                            var odgovor = odgovoriZaPitanje.FirstOrDefault(x => x.ID == odgovorId);

                            if (odgovor == null)
                                continue;

                            if (odgovor.IS_TACAN == "Y")
                            {
                                bodoviZaPitanje += odgovor.BODOVI ?? 0;
                            }
                            else
                            {
                                bodoviZaPitanje -= odgovor.NEGATIVNI_BODOVI ?? 0;
                            }
                        }

                        if (bodoviZaPitanje < 0)
                            bodoviZaPitanje = 0;

                        if (bodoviZaPitanje > testPitanje.BODOVI)
                            bodoviZaPitanje = testPitanje.BODOVI;
                    }
                }

                foreach (var odgovorId in validniOdabraniOdgovori)
                {
                    var odgovor = odgovoriZaPitanje.FirstOrDefault(x => x.ID == odgovorId);
                    var isTacan = tacniOdgovori.Contains(odgovorId);

                    decimal? bodoviZaOdgovor = null;

                    if (tip == "MULTIPLE_CHOICE" || tip == "MULTIPLE")
                    {
                        if (parcijalnoBodovanje && odgovor != null)
                        {
                            bodoviZaOdgovor = odgovor.IS_TACAN == "Y"
                                ? odgovor.BODOVI
                                : -(odgovor.NEGATIVNI_BODOVI ?? 0);
                        }
                    }
                    else if (tip == "SINGLE_CHOICE" || tip == "SINGLE")
                    {
                        bodoviZaOdgovor = isTacan ? testPitanje.BODOVI : 0;
                    }

                    _dbContext.STUDENT_ODGOVORI.Add(new StudentOdgovor
                    {
                        POLAGANJE_TESTA_ID = polaganje.ID,
                        POLAGANJE_PITANJE_ID = pitanje.ID,
                        TEST_PITANJE_ODGOVOR_ID = odgovorId,
                        ESSAY_ODGOVOR = null,
                        IS_TACAN = isTacan ? "Y" : "N",
                        OSTVARENI_BODOVI = bodoviZaOdgovor,
                        DATUM_ODGOVORA = now
                    });
                }

                ostvareniBodovi += bodoviZaPitanje;
            }

            var procenat = maxBodovi > 0
                ? Math.Round((ostvareniBodovi / maxBodovi) * 100, 2)
                : 0;

            polaganje.DATUM_ZAVRSETKA = now;
            polaganje.OSTVARENI_BODOVI = ostvareniBodovi;
            polaganje.MAX_BODOVI = maxBodovi;
            polaganje.AUTO_SUBMITTED = autoSubmitted ? "Y" : "N";

            if (imaEsejskaPitanja)
            {
                polaganje.STATUS = "PENDING_REVIEW";

                polaganje.PROCENAT = null;
                polaganje.IS_PASSED = null;
            }
            else
            {
                polaganje.STATUS = autoSubmitted
                    ? "TIME_EXPIRED"
                    : "SUBMITTED";

                polaganje.PROCENAT = procenat;

                polaganje.IS_PASSED =
                    procenat >= polaganje.Test.MIN_PROCENAT_PROLAZA
                        ? "Y"
                        : "N";
            }

            await _dbContext.SaveChangesAsync();

            return RedirectToAction(nameof(Result), new { id = polaganje.ID });
        }

        public async Task<IActionResult> Result(int id)
        {
            var korisnik = await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x => x.USER_ID == CurrentUserId);

            if (korisnik == null)
                return RedirectToAction(nameof(Index));

            var polaganje = await _dbContext.POLAGANJE_TESTA
                .Include(x => x.Test)
                    .ThenInclude(x => x.Predmet)
                .FirstOrDefaultAsync(x =>
                    x.ID == id &&
                    x.KORISNIK_ID == korisnik.ID);

            if (polaganje == null)
                return NotFound();

            return View(polaganje);
        }
    }
}