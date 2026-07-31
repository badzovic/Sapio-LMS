using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio_LMS.Areas.Instructor.Models;

namespace SIA.Sapio_LMS.Areas.Instructor.Controllers
{
    [Area("Instructor")]
    public class SubjectTestsController : BaseInstructorController
    {
        private readonly ApplicationDbContext _dbContext;

        public SubjectTestsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index(
            int predmetId,
            string status = "all",
            DateTime? datumOd = null,
            DateTime? datumDo = null)
        {
            var predmet = await GetInstructorPredmetAsync(predmetId);

            if (predmet == null)
                return NotFound();

            status = string.IsNullOrWhiteSpace(status)
                ? "all"
                : status.ToLower();

            var query = _dbContext.TESTOVI
                .Include(x => x.TEST_KLASA)
                .Where(x => x.PREDMET_ID == predmetId && x.AKTIVNO == "Y");

            if (status != "all")
            {
                var dbStatus = status.ToUpper();

                query = query.Where(x => x.STATUS == dbStatus);
            }

            if (datumOd.HasValue)
            {
                query = query.Where(x => x.DATUM_DO >= datumOd.Value);
            }

            if (datumDo.HasValue)
            {
                var datumDoEnd = datumDo.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(x => x.DATUM_OD <= datumDoEnd);
            }

            var tests = await query
                .OrderByDescending(x => x.DATUM_KREIRANJA)
                .Select(x => new SubjectTestVM
                {
                    ID = x.ID,
                    PredmetId = x.PREDMET_ID,
                    PredmetNaziv = predmet.NAZIV,

                    TestKlasaId = x.TEST_KLASA_ID,
                    TestKlasaNaziv = x.TEST_KLASA != null ? x.TEST_KLASA.NAZIV : "-",

                    Naziv = x.NAZIV,
                    Opis = x.OPIS,
                    DatumOd = x.DATUM_OD,
                    DatumDo = x.DATUM_DO,
                    VrijemeTrajanjaMin = x.VRIJEME_TRAJANJA_MIN,
                    BrojPitanja = x.BROJ_PITANJA,
                    BrojLakih = x.BROJ_LAKIH,
                    BrojSrednjih = x.BROJ_SREDNJIH,
                    BrojTeskih = x.BROJ_TESKIH,
                    MinProcenatProlaza = x.MIN_PROCENAT_PROLAZA,
                    BrojPokusaja = x.BROJ_POKUSAJA,
                    Status = x.STATUS,
                    Aktivno = x.AKTIVNO,
                    DatumKreiranja = x.DATUM_KREIRANJA
                })
                .ToListAsync();

            var model = new SubjectTestIndexVM
            {
                PredmetId = predmet.ID,
                PredmetNaziv = predmet.NAZIV,
                Status = status,
                DatumOd = datumOd,
                DatumDo = datumDo,
                Tests = tests
            };

            return View(model);
        }

        public async Task<IActionResult> Create(int predmetId)
        {
            var predmet = await GetInstructorPredmetAsync(predmetId);

            if (predmet == null)
                return NotFound();

            var model = new CreateSubjectTestVM
            {
                PredmetId = predmet.ID,
                PredmetNaziv = predmet.NAZIV,

                TestKlasaId = await GetDefaultTestKlasaIdAsync(),

                DatumOd = DateTime.Now,
                DatumDo = DateTime.Now.AddDays(15),

                VrijemeTrajanjaMin = 30,
                MinProcenatProlaza = 70,
                BrojPokusaja = 1,

                RandomPitanja = "Y",
                RandomOdgovori = "Y"
            };

            await PopulateCreateTestModelAsync(model, predmet.ID, predmet.NAZIV);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateSubjectTestVM model)
        {
            var predmet = await GetInstructorPredmetAsync(model.PredmetId);

            if (predmet == null)
                return NotFound();

            model.PredmetNaziv = predmet.NAZIV;
            model.BrojPitanja = model.BrojLakih + model.BrojSrednjih + model.BrojTeskih;

            if (string.IsNullOrWhiteSpace(model.Naziv))
                ModelState.AddModelError(nameof(model.Naziv), "Naziv testa je obavezan.");

            if (model.TestKlasaId <= 0)
            {
                ModelState.AddModelError(nameof(model.TestKlasaId), "Klasa testa je obavezna.");
            }
            else
            {
                var klasaPostoji = await _dbContext.TEST_KLASE
                    .AnyAsync(x => x.ID == model.TestKlasaId && x.AKTIVNO == "Y");

                if (!klasaPostoji)
                    ModelState.AddModelError(nameof(model.TestKlasaId), "Odabrana klasa testa nije validna.");
            }

            if (model.DatumOd >= model.DatumDo)
                ModelState.AddModelError(nameof(model.DatumDo), "Datum do mora biti veći od datuma od.");

            if (model.VrijemeTrajanjaMin <= 0)
                ModelState.AddModelError(nameof(model.VrijemeTrajanjaMin), "Vrijeme trajanja mora biti veće od 0.");

            if (model.BrojPitanja <= 0)
                ModelState.AddModelError(nameof(model.BrojPitanja), "Test mora imati barem jedno pitanje.");

            if (model.MinProcenatProlaza < 0 || model.MinProcenatProlaza > 100)
                ModelState.AddModelError(nameof(model.MinProcenatProlaza), "Minimalni procenat prolaza mora biti između 0 i 100.");

            if (model.BrojPokusaja <= 0)
                ModelState.AddModelError(nameof(model.BrojPokusaja), "Broj pokušaja mora biti veći od 0.");

            var aktivnaPitanja = await _dbContext.PREDMET_PITANJA
                .Include(x => x.TipTezinePitanja)
                .Where(x => x.PREDMET_ID == model.PredmetId && x.AKTIVNO == "Y")
                .ToListAsync();

            var dostupnoLakih = aktivnaPitanja.Count(x => x.TipTezinePitanja != null && x.TipTezinePitanja.SIFRA == "LAKO");
            var dostupnoSrednjih = aktivnaPitanja.Count(x => x.TipTezinePitanja != null && x.TipTezinePitanja.SIFRA == "SREDNJE");
            var dostupnoTeskih = aktivnaPitanja.Count(x => x.TipTezinePitanja != null && x.TipTezinePitanja.SIFRA == "TESKO");

            if (model.BrojLakih > dostupnoLakih)
                ModelState.AddModelError(nameof(model.BrojLakih), $"Nema dovoljno lakih pitanja. Dostupno: {dostupnoLakih}.");

            if (model.BrojSrednjih > dostupnoSrednjih)
                ModelState.AddModelError(nameof(model.BrojSrednjih), $"Nema dovoljno srednjih pitanja. Dostupno: {dostupnoSrednjih}.");

            if (model.BrojTeskih > dostupnoTeskih)
                ModelState.AddModelError(nameof(model.BrojTeskih), $"Nema dovoljno teških pitanja. Dostupno: {dostupnoTeskih}.");

            var mandatoryIds = model.ObaveznaPitanjaIds?.Distinct().ToList() ?? new List<int>();

            var mandatoryQuestions = aktivnaPitanja
                .Where(x => mandatoryIds.Contains(x.ID))
                .ToList();

            if (mandatoryIds.Count != mandatoryQuestions.Count)
                ModelState.AddModelError("", "Neka od označenih obaveznih pitanja nisu validna ili više nisu aktivna.");

            var mandatoryEasy = mandatoryQuestions.Count(x => x.TipTezinePitanja != null && x.TipTezinePitanja.SIFRA == "LAKO");
            var mandatoryMedium = mandatoryQuestions.Count(x => x.TipTezinePitanja != null && x.TipTezinePitanja.SIFRA == "SREDNJE");
            var mandatoryHard = mandatoryQuestions.Count(x => x.TipTezinePitanja != null && x.TipTezinePitanja.SIFRA == "TESKO");

            if (mandatoryEasy > model.BrojLakih)
                ModelState.AddModelError("", "Broj obaveznih lakih pitanja je veći od traženog broja lakih pitanja.");

            if (mandatoryMedium > model.BrojSrednjih)
                ModelState.AddModelError("", "Broj obaveznih srednjih pitanja je veći od traženog broja srednjih pitanja.");

            if (mandatoryHard > model.BrojTeskih)
                ModelState.AddModelError("", "Broj obaveznih teških pitanja je veći od traženog broja teških pitanja.");

            if (!ModelState.IsValid)
            {
                await PopulateCreateTestModelAsync(model, predmet.ID, predmet.NAZIV);
                return View(model);
            }

            var now = DateTime.Now;

            using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                var test = new Test
                {
                    PREDMET_ID = model.PredmetId,
                    TEST_KLASA_ID = model.TestKlasaId,

                    NAZIV = model.Naziv.Trim(),
                    OPIS = model.Opis,

                    DATUM_OD = model.DatumOd,
                    DATUM_DO = model.DatumDo,

                    VRIJEME_TRAJANJA_MIN = model.VrijemeTrajanjaMin,

                    BROJ_PITANJA = model.BrojPitanja,
                    BROJ_LAKIH = model.BrojLakih,
                    BROJ_SREDNJIH = model.BrojSrednjih,
                    BROJ_TESKIH = model.BrojTeskih,

                    MIN_PROCENAT_PROLAZA = model.MinProcenatProlaza,
                    BROJ_POKUSAJA = model.BrojPokusaja,

                    RANDOM_PITANJA = model.RandomPitanja == "Y" ? "Y" : "N",
                    RANDOM_ODGOVORI = model.RandomOdgovori == "Y" ? "Y" : "N",

                    STATUS = "DRAFT",
                    AKTIVNO = "Y",

                    CREATED_BY_USER_ID = CurrentUserId,

                    DATUM_KREIRANJA = now,
                    DATUM_IZMJENE = null
                };

                _dbContext.TESTOVI.Add(test);
                await _dbContext.SaveChangesAsync();

                await GenerateTestSnapshotAsync(test, model);

                await transaction.CommitAsync();

                TempData["ImportMessage"] = "Test je uspješno generisan.";

                return RedirectToAction(nameof(Index), new { predmetId = model.PredmetId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                TempData["ImportMessage"] = "Greška prilikom generisanja testa.";
                TempData["ImportErrors"] = ex.Message;

                await PopulateCreateTestModelAsync(model, predmet.ID, predmet.NAZIV);
                return View(model);
            }
        }

        private async Task GenerateTestSnapshotAsync(Test test, CreateSubjectTestVM model)
        {
            var selectedQuestions = await SelectQuestionsForTestAsync(model);

            if (model.RandomPitanja == "Y")
                selectedQuestions = selectedQuestions.OrderBy(x => Guid.NewGuid()).ToList();

            var now = DateTime.Now;
            var redoslijed = 1;

            foreach (var question in selectedQuestions)
            {
                var testQuestion = new TestPitanje
                {
                    TEST_ID = test.ID,
                    ORIGINAL_PITANJE_ID = question.ID,

                    TIP_PITANJA_ID = question.TIP_PITANJA_ID,
                    TIP_TEZINE_ID = question.TIP_TEZINE_ID,

                    TEKST_PITANJA = question.TEKST_PITANJA,

                    BODOVI = question.BODOVI,
                    NEGATIVNI_BODOVI = question.NEGATIVNI_BODOVI,

                    PARCIJALNO_BODOVANJE = question.PARCIJALNO_BODOVANJE,

                    REDOSLIJED = redoslijed,

                    IS_OBAVEZNO = model.ObaveznaPitanjaIds != null && model.ObaveznaPitanjaIds.Contains(question.ID) ? "Y" : "N",

                    DATUM_KREIRANJA = now,

                    SLIKA_PUTANJA = question.SLIKA_PUTANJA,
                    SLIKA_NAZIV = question.SLIKA_NAZIV
                };

                _dbContext.TEST_PITANJA.Add(testQuestion);
                await _dbContext.SaveChangesAsync();

                var answers = await _dbContext.PREDMET_PITANJE_ODGOVORI
                    .Where(x =>
                        x.PITANJE_ID == question.ID &&
                        x.AKTIVNO == "Y")
                    .OrderBy(x => x.REDOSLIJED)
                    .ToListAsync();

                if (model.RandomOdgovori == "Y")
                    answers = answers.OrderBy(x => Guid.NewGuid()).ToList();

                var odgovorRedoslijed = 1;

                foreach (var answer in answers)
                {
                    var testAnswer = new TestPitanjeOdgovor
                    {
                        TEST_PITANJE_ID = testQuestion.ID,
                        ORIGINAL_ODGOVOR_ID = answer.ID,

                        TEKST_ODGOVORA = answer.TEKST_ODGOVORA,

                        IS_TACAN = answer.IS_TACAN,

                        BODOVI = answer.BODOVI,
                        NEGATIVNI_BODOVI = answer.NEGATIVNI_BODOVI,

                        REDOSLIJED = odgovorRedoslijed,

                        DATUM_KREIRANJA = now
                    };

                    _dbContext.TEST_PITANJE_ODGOVORI.Add(testAnswer);

                    odgovorRedoslijed++;
                }

                await _dbContext.SaveChangesAsync();

                redoslijed++;
            }
        }

        private async Task<List<PredmetPitanje>> SelectQuestionsForTestAsync(CreateSubjectTestVM model)
        {
            var mandatoryIds = model.ObaveznaPitanjaIds?.Distinct().ToList() ?? new List<int>();

            var allQuestions = await _dbContext.PREDMET_PITANJA
                .Include(x => x.TipTezinePitanja)
                .Where(x =>
                    x.PREDMET_ID == model.PredmetId &&
                    x.AKTIVNO == "Y")
                .ToListAsync();

            var mandatoryQuestions = allQuestions
                .Where(x => mandatoryIds.Contains(x.ID))
                .ToList();

            var selected = new List<PredmetPitanje>();
            selected.AddRange(mandatoryQuestions);

            AddRandomQuestionsByDifficulty(
                selected,
                allQuestions,
                mandatoryIds,
                "LAKO",
                model.BrojLakih);

            AddRandomQuestionsByDifficulty(
                selected,
                allQuestions,
                mandatoryIds,
                "SREDNJE",
                model.BrojSrednjih);

            AddRandomQuestionsByDifficulty(
                selected,
                allQuestions,
                mandatoryIds,
                "TESKO",
                model.BrojTeskih);

            return selected
                .GroupBy(x => x.ID)
                .Select(x => x.First())
                .ToList();
        }

        private void AddRandomQuestionsByDifficulty(
            List<PredmetPitanje> selected,
            List<PredmetPitanje> allQuestions,
            List<int> mandatoryIds,
            string tezinaSifra,
            int requestedCount)
        {
            var alreadySelectedForDifficulty = selected.Count(x =>
                x.TipTezinePitanja != null &&
                x.TipTezinePitanja.SIFRA == tezinaSifra);

            var remainingNeeded = requestedCount - alreadySelectedForDifficulty;

            if (remainingNeeded <= 0)
                return;

            var pool = allQuestions
                .Where(x =>
                    x.TipTezinePitanja != null &&
                    x.TipTezinePitanja.SIFRA == tezinaSifra &&
                    !mandatoryIds.Contains(x.ID) &&
                    !selected.Any(s => s.ID == x.ID))
                .OrderBy(x => Guid.NewGuid())
                .Take(remainingNeeded)
                .ToList();

            selected.AddRange(pool);
        }

        private async Task PopulateCreateTestModelAsync(CreateSubjectTestVM model, int predmetId, string predmetNaziv)
        {
            var dostupnaPitanja = await _dbContext.PREDMET_PITANJA
                .Include(x => x.TipPitanja)
                .Include(x => x.TipTezinePitanja)
                .Where(x =>
                    x.PREDMET_ID == predmetId &&
                    x.AKTIVNO == "Y")
                .OrderBy(x => x.TipTezinePitanja!.REDOSLIJED)
                .ThenBy(x => x.TEKST_PITANJA)
                .Select(x => new CreateSubjectTestQuestionOptionVM
                {
                    ID = x.ID,
                    TekstPitanja = x.TEKST_PITANJA,
                    TipPitanja = x.TipPitanja != null ? x.TipPitanja.NAZIV : "-",
                    Tezina = x.TipTezinePitanja != null ? x.TipTezinePitanja.NAZIV : "-",
                    TezinaSifra = x.TipTezinePitanja != null ? x.TipTezinePitanja.SIFRA : "",
                    Bodovi = x.BODOVI
                })
                .ToListAsync();

            model.PredmetId = predmetId;
            model.PredmetNaziv = predmetNaziv;

            model.DostupnaPitanja = dostupnaPitanja;

            model.DostupnoLakih = dostupnaPitanja.Count(x => x.TezinaSifra == "LAKO");
            model.DostupnoSrednjih = dostupnaPitanja.Count(x => x.TezinaSifra == "SREDNJE");
            model.DostupnoTeskih = dostupnaPitanja.Count(x => x.TezinaSifra == "TESKO");

            model.TestKlase = await _dbContext.TEST_KLASE
                .Where(x => x.AKTIVNO == "Y")
                .OrderBy(x => x.NAZIV)
                .ToListAsync();
        }

        private async Task<Predmet?> GetInstructorPredmetAsync(int predmetId)
        {
            if (string.IsNullOrWhiteSpace(CurrentUserId))
                return null;

            var lokalniKorisnik = await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x =>
                    x.USER_ID == CurrentUserId &&
                    x.AKTIVAN == "Y");

            if (lokalniKorisnik == null)
                return null;

            return await _dbContext.PREDMET_INSTRUKTORI
                .Include(x => x.Predmet)
                .Where(x =>
                    x.PREDMET_ID == predmetId &&
                    x.KORISNIK_ID == lokalniKorisnik.ID &&
                    x.AKTIVNO == "Y" &&
                    x.Predmet != null &&
                    x.Predmet.AKTIVNO == "Y")
                .Select(x => x.Predmet)
                .FirstOrDefaultAsync();
        }


        //ZA EDIT
        public async Task<IActionResult> Details(int id)
        {
            var test = await _dbContext.TESTOVI
                .Include(x => x.Predmet)
                .Include(x => x.TEST_KLASA)
                .FirstOrDefaultAsync(x => x.ID == id);

            if (test == null)
                return NotFound();

            var predmet = await GetInstructorPredmetAsync(test.PREDMET_ID);

            if (predmet == null)
                return NotFound();

            var pitanja = await _dbContext.TEST_PITANJA
                .Include(x => x.TipPitanja)
                .Include(x => x.TipTezinePitanja)
                .Where(x => x.TEST_ID == id)
                .OrderBy(x => x.REDOSLIJED)
                .ToListAsync();

            var model = new SubjectTestDetailsVM
            {
                ID = test.ID,
                PredmetId = test.PREDMET_ID,
                PredmetNaziv = predmet.NAZIV,
                TestKlasaId = test.TEST_KLASA_ID,
                TestKlasaNaziv = test.TEST_KLASA != null ? test.TEST_KLASA.NAZIV : "-",
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

            foreach (var pitanje in pitanja)
            {
                var odgovori = await _dbContext.TEST_PITANJE_ODGOVORI
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

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Activate(int id)
        {
            var test = await _dbContext.TESTOVI.FirstOrDefaultAsync(x => x.ID == id);

            if (test == null)
                return NotFound();

            var predmet = await GetInstructorPredmetAsync(test.PREDMET_ID);

            if (predmet == null)
                return NotFound();

            if (test.STATUS != "DRAFT")
            {
                TempData["ImportMessage"] = "Samo draft test se može aktivirati.";
                return RedirectToAction(nameof(Details), new { id });
            }

            test.STATUS = "ACTIVE";
            test.DATUM_IZMJENE = DateTime.Now;

            await _dbContext.SaveChangesAsync();

            TempData["ImportMessage"] = "Test je aktiviran.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            var test = await _dbContext.TESTOVI.FirstOrDefaultAsync(x => x.ID == id);

            if (test == null)
                return NotFound();

            var predmet = await GetInstructorPredmetAsync(test.PREDMET_ID);

            if (predmet == null)
                return NotFound();

            if (test.STATUS != "ACTIVE")
            {
                TempData["ImportMessage"] = "Samo aktivan test se može arhivirati.";
                return RedirectToAction(nameof(Details), new { id });
            }

            test.STATUS = "ARCHIVED";
            test.DATUM_IZMJENE = DateTime.Now;

            await _dbContext.SaveChangesAsync();

            TempData["ImportMessage"] = "Test je arhiviran.";

            return RedirectToAction(nameof(Details), new { id });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var test = await _dbContext.TESTOVI
                .FirstOrDefaultAsync(x => x.ID == id && x.AKTIVNO == "Y");

            if (test == null)
                return NotFound();

            var predmet = await GetInstructorPredmetAsync(test.PREDMET_ID);

            if (predmet == null)
                return NotFound();

            if (test.STATUS != "DRAFT")
            {
                TempData["ImportMessage"] = "Samo draft test se može uređivati.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var obaveznaPitanjaIds = await _dbContext.TEST_PITANJA
                .Where(x => x.TEST_ID == test.ID && x.IS_OBAVEZNO == "Y" && x.ORIGINAL_PITANJE_ID != null)
                .Select(x => x.ORIGINAL_PITANJE_ID!.Value)
                .ToListAsync();

            var model = new CreateSubjectTestVM
            {
                TestId = test.ID,
                PredmetId = test.PREDMET_ID,
                PredmetNaziv = predmet.NAZIV,

                TestKlasaId = test.TEST_KLASA_ID,

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

                ObaveznaPitanjaIds = obaveznaPitanjaIds
            };

            await PopulateCreateTestModelAsync(model, predmet.ID, predmet.NAZIV);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CreateSubjectTestVM model)
        {
            var test = await _dbContext.TESTOVI
                .FirstOrDefaultAsync(x => x.ID == model.TestId && x.AKTIVNO == "Y");

            if (test == null)
                return NotFound();

            var predmet = await GetInstructorPredmetAsync(test.PREDMET_ID);

            if (predmet == null)
                return NotFound();

            if (test.STATUS != "DRAFT")
            {
                TempData["ImportMessage"] = "Samo draft test se može uređivati.";
                return RedirectToAction(nameof(Details), new { id = test.ID });
            }

            model.PredmetId = test.PREDMET_ID;
            model.PredmetNaziv = predmet.NAZIV;
            model.BrojPitanja = model.BrojLakih + model.BrojSrednjih + model.BrojTeskih;

            if (string.IsNullOrWhiteSpace(model.Naziv))
                ModelState.AddModelError(nameof(model.Naziv), "Naziv testa je obavezan.");

            if (model.TestKlasaId <= 0)
            {
                ModelState.AddModelError(nameof(model.TestKlasaId), "Klasa testa je obavezna.");
            }
            else
            {
                var klasaPostoji = await _dbContext.TEST_KLASE
                    .AnyAsync(x => x.ID == model.TestKlasaId && x.AKTIVNO == "Y");

                if (!klasaPostoji)
                    ModelState.AddModelError(nameof(model.TestKlasaId), "Odabrana klasa testa nije validna.");
            }

            if (model.DatumOd >= model.DatumDo)
                ModelState.AddModelError(nameof(model.DatumDo), "Datum do mora biti veći od datuma od.");

            if (model.VrijemeTrajanjaMin <= 0)
                ModelState.AddModelError(nameof(model.VrijemeTrajanjaMin), "Vrijeme trajanja mora biti veće od 0.");

            if (model.BrojPitanja <= 0)
                ModelState.AddModelError(nameof(model.BrojPitanja), "Test mora imati barem jedno pitanje.");

            if (model.MinProcenatProlaza < 0 || model.MinProcenatProlaza > 100)
                ModelState.AddModelError(nameof(model.MinProcenatProlaza), "Minimalni procenat prolaza mora biti između 0 i 100.");

            if (model.BrojPokusaja <= 0)
                ModelState.AddModelError(nameof(model.BrojPokusaja), "Broj pokušaja mora biti veći od 0.");

            var aktivnaPitanja = await _dbContext.PREDMET_PITANJA
                .Include(x => x.TipTezinePitanja)
                .Where(x => x.PREDMET_ID == test.PREDMET_ID && x.AKTIVNO == "Y")
                .ToListAsync();

            var dostupnoLakih = aktivnaPitanja.Count(x => x.TipTezinePitanja != null && x.TipTezinePitanja.SIFRA == "LAKO");
            var dostupnoSrednjih = aktivnaPitanja.Count(x => x.TipTezinePitanja != null && x.TipTezinePitanja.SIFRA == "SREDNJE");
            var dostupnoTeskih = aktivnaPitanja.Count(x => x.TipTezinePitanja != null && x.TipTezinePitanja.SIFRA == "TESKO");

            if (model.BrojLakih > dostupnoLakih)
                ModelState.AddModelError(nameof(model.BrojLakih), $"Nema dovoljno lakih pitanja. Dostupno: {dostupnoLakih}.");

            if (model.BrojSrednjih > dostupnoSrednjih)
                ModelState.AddModelError(nameof(model.BrojSrednjih), $"Nema dovoljno srednjih pitanja. Dostupno: {dostupnoSrednjih}.");

            if (model.BrojTeskih > dostupnoTeskih)
                ModelState.AddModelError(nameof(model.BrojTeskih), $"Nema dovoljno teških pitanja. Dostupno: {dostupnoTeskih}.");

            var mandatoryIds = model.ObaveznaPitanjaIds?.Distinct().ToList() ?? new List<int>();

            var mandatoryQuestions = aktivnaPitanja
                .Where(x => mandatoryIds.Contains(x.ID))
                .ToList();

            if (mandatoryIds.Count != mandatoryQuestions.Count)
                ModelState.AddModelError("", "Neka od označenih obaveznih pitanja nisu validna ili više nisu aktivna.");

            var mandatoryEasy = mandatoryQuestions.Count(x => x.TipTezinePitanja != null && x.TipTezinePitanja.SIFRA == "LAKO");
            var mandatoryMedium = mandatoryQuestions.Count(x => x.TipTezinePitanja != null && x.TipTezinePitanja.SIFRA == "SREDNJE");
            var mandatoryHard = mandatoryQuestions.Count(x => x.TipTezinePitanja != null && x.TipTezinePitanja.SIFRA == "TESKO");

            if (mandatoryEasy > model.BrojLakih)
                ModelState.AddModelError("", "Broj obaveznih lakih pitanja je veći od traženog broja lakih pitanja.");

            if (mandatoryMedium > model.BrojSrednjih)
                ModelState.AddModelError("", "Broj obaveznih srednjih pitanja je veći od traženog broja srednjih pitanja.");

            if (mandatoryHard > model.BrojTeskih)
                ModelState.AddModelError("", "Broj obaveznih teških pitanja je veći od traženog broja teških pitanja.");

            if (!ModelState.IsValid)
            {
                await PopulateCreateTestModelAsync(model, predmet.ID, predmet.NAZIV);
                return View(model);
            }

            using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                test.NAZIV = model.Naziv.Trim();
                test.OPIS = model.Opis;

                test.TEST_KLASA_ID = model.TestKlasaId;

                test.DATUM_OD = model.DatumOd;
                test.DATUM_DO = model.DatumDo;

                test.VRIJEME_TRAJANJA_MIN = model.VrijemeTrajanjaMin;

                test.BROJ_PITANJA = model.BrojPitanja;
                test.BROJ_LAKIH = model.BrojLakih;
                test.BROJ_SREDNJIH = model.BrojSrednjih;
                test.BROJ_TESKIH = model.BrojTeskih;

                test.MIN_PROCENAT_PROLAZA = model.MinProcenatProlaza;
                test.BROJ_POKUSAJA = model.BrojPokusaja;

                test.RANDOM_PITANJA = model.RandomPitanja == "Y" ? "Y" : "N";
                test.RANDOM_ODGOVORI = model.RandomOdgovori == "Y" ? "Y" : "N";

                test.DATUM_IZMJENE = DateTime.Now;

                await DeleteTestSnapshotAsync(test.ID);
                await _dbContext.SaveChangesAsync();

                await GenerateTestSnapshotAsync(test, model);

                await transaction.CommitAsync();

                TempData["ImportMessage"] = "Draft test je uspješno ažuriran.";

                return RedirectToAction(nameof(Details), new { id = test.ID });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                TempData["ImportMessage"] = "Greška prilikom ažuriranja draft testa.";
                TempData["ImportErrors"] = ex.Message;

                await PopulateCreateTestModelAsync(model, predmet.ID, predmet.NAZIV);
                return View(model);
            }
        }

        private async Task DeleteTestSnapshotAsync(int testId)
        {
            var questions = await _dbContext.TEST_PITANJA
                .Where(x => x.TEST_ID == testId)
                .ToListAsync();

            if (!questions.Any())
                return;

            foreach (var question in questions)
            {
                var answers = await _dbContext.TEST_PITANJE_ODGOVORI
                    .Where(x => x.TEST_PITANJE_ID == question.ID)
                    .ToListAsync();

                if (answers.Any())
                {
                    _dbContext.TEST_PITANJE_ODGOVORI.RemoveRange(answers);
                }
            }

            _dbContext.TEST_PITANJA.RemoveRange(questions);
        }
        private async Task<int> GetDefaultTestKlasaIdAsync()
        {
            return await _dbContext.TEST_KLASE
                .Where(x => x.AKTIVNO == "Y" && x.SIFRA == "STICANJE")
                .Select(x => x.ID)
                .FirstOrDefaultAsync();
        }
    }
}