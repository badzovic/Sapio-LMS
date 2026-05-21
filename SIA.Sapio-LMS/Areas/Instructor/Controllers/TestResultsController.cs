using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio.LMS.Web.Areas.Instructor.Models.TestResults;
//using SIA.Sapio_LMS.Areas.Instructor.Models.TestResults;

namespace SIA.Sapio_LMS.Areas.Instructor.Controllers
{
    [Area("Instructor")]
    public class TestResultsController : BaseInstructorController
    {
        private readonly ApplicationDbContext _dbContext;

        public TestResultsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index(int predmetId)
        {
            var lokalniKorisnik = await GetCurrentLokalniKorisnikAsync();

            if (lokalniKorisnik == null)
                return Forbid();

            var predmet = await _dbContext.PREDMET_INSTRUKTORI
                .Include(x => x.Predmet)
                .Where(x =>
                    x.PREDMET_ID == predmetId &&
                    x.KORISNIK_ID == lokalniKorisnik.ID &&
                    x.AKTIVNO == "Y" &&
                    x.Predmet != null &&
                    x.Predmet.AKTIVNO == "Y")
                .Select(x => x.Predmet)
                .FirstOrDefaultAsync();

            if (predmet == null)
                return NotFound();

            var results = await _dbContext.POLAGANJE_TESTA
                .Include(x => x.Test)
                .Include(x => x.Korisnik)
                .Include(x => x.StudentOdgovori)
                .Where(x =>
                    x.Test != null &&
                    x.Test.PREDMET_ID == predmetId)
                .OrderByDescending(x => x.DATUM_ZAVRSETKA ?? x.DATUM_POCETKA)
                .Select(x => new InstructorTestResultListItemVM
                {
                    PolaganjeId = x.ID,

                    StudentImePrezime =
                        x.Korisnik != null
                            ? x.Korisnik.IME + " " + x.Korisnik.PREZIME
                            : "",

                    TestNaziv =
                        x.Test != null
                            ? x.Test.NAZIV
                            : "",

                    DatumPocetka = x.DATUM_POCETKA,
                    DatumZavrsetka = x.DATUM_ZAVRSETKA,
                    Status = x.STATUS,
                    RedniBrojPokusaja = x.REDNI_BROJ_POKUSAJA,

                    OstvareniBodovi = x.OSTVARENI_BODOVI ?? 0,
                    MaxBodovi = x.MAX_BODOVI ?? 0,
                    Procenat = x.PROCENAT ?? 0,

                    IsPassed = x.IS_PASSED == "Y",

                    HasPendingEssayReview = x.StudentOdgovori.Any(a =>
                        a.ESSAY_ODGOVOR != null &&
                        a.OSTVARENI_BODOVI == null)
                })
                .ToListAsync();

            var model = new InstructorTestResultsIndexVM
            {
                PredmetId = predmet.ID,
                PredmetNaziv = predmet.NAZIV,
                Results = results
            };

            return View(model);
        }



        public async Task<IActionResult> Details(int id)
        {
            var lokalniKorisnik = await GetCurrentLokalniKorisnikAsync();

            if (lokalniKorisnik == null)
                return Forbid();

            var polaganje = await _dbContext.POLAGANJE_TESTA
                .Include(x => x.Test)
                .Include(x => x.Korisnik)
                .FirstOrDefaultAsync(x => x.ID == id);

            if (polaganje == null || polaganje.Test == null)
                return NotFound();

            var predmetId = polaganje.Test.PREDMET_ID;

            var hasAccess = await _dbContext.PREDMET_INSTRUKTORI
                .AnyAsync(x =>
                    x.PREDMET_ID == predmetId &&
                    x.KORISNIK_ID == lokalniKorisnik.ID &&
                    x.AKTIVNO == "Y");

            if (!hasAccess)
                return Forbid();

            var polaganjePitanja = await _dbContext.POLAGANJE_PITANJA
                .Include(x => x.TestPitanje)
                    .ThenInclude(x => x.TipPitanja)
                .Where(x => x.POLAGANJE_TESTA_ID == id)
                .OrderBy(x => x.REDOSLIJED)
                .ToListAsync();

            var studentOdgovori = await _dbContext.STUDENT_ODGOVORI
                .Where(x => x.POLAGANJE_TESTA_ID == id)
                .ToListAsync();

            var ponudjeniOdgovori = await _dbContext.TEST_PITANJE_ODGOVORI
                .Where(o => _dbContext.POLAGANJE_PITANJA.Any(pp =>
                    pp.POLAGANJE_TESTA_ID == id &&
                    pp.TEST_PITANJE_ID == o.TEST_PITANJE_ID))
                .OrderBy(o => o.TEST_PITANJE_ID)
                .ThenBy(o => o.REDOSLIJED)
                .ToListAsync();

            var questions = polaganjePitanja
                .Where(p => p.TestPitanje != null)
                .Select(p =>
                {
                    var pitanje = p.TestPitanje!;

                    var odgovoriStudentaZaPitanje = studentOdgovori
                        .Where(x => x.POLAGANJE_PITANJE_ID == p.ID)
                        .ToList();

                    var essayOdgovor = odgovoriStudentaZaPitanje
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.ESSAY_ODGOVOR));

                    var odgovoriZaPitanje = ponudjeniOdgovori
                        .Where(x => x.TEST_PITANJE_ID == pitanje.ID)
                        .OrderBy(x => x.REDOSLIJED)
                        .ToList();

                    return new InstructorEssayAnswerGradeVM
                    {
                        PolaganjeId = id,
                        PolaganjePitanjeId = p.ID,
                        TestPitanjeId = pitanje.ID,

                        StudentOdgovorId = essayOdgovor?.ID ?? 0,

                        PitanjeTekst = pitanje.TEKST_PITANJA ?? "",

                        TipPitanjaSifra = pitanje.TipPitanja != null
                            ? pitanje.TipPitanja.SIFRA
                            : "",

                        Bodovi = pitanje.BODOVI,
                        MaxBodovi = pitanje.BODOVI,
                        NegativniBodovi = pitanje.NEGATIVNI_BODOVI ?? 0,

                        EssayOdgovor = essayOdgovor?.ESSAY_ODGOVOR,
                        DodijeljeniBodovi = essayOdgovor?.OSTVARENI_BODOVI,

                        Odgovori = odgovoriZaPitanje
                            .Select(o => new InstructorEssayAnswerOptionVM
                            {
                                TestPitanjeOdgovorId = o.ID,
                                TekstOdgovora = o.TEKST_ODGOVORA ?? "",

                                IsTacan = o.IS_TACAN == "Y",

                                IsSelectedByStudent = odgovoriStudentaZaPitanje.Any(so =>
                                    so.TEST_PITANJE_ODGOVOR_ID.HasValue &&
                                    so.TEST_PITANJE_ODGOVOR_ID.Value == o.ID),

                                Bodovi = o.BODOVI ?? 0,
                                NegativniBodovi = o.NEGATIVNI_BODOVI ?? 0
                            })
                            .ToList()
                    };
                })
                .ToList();

            var model = new InstructorTestResultDetailsVM
            {
                PolaganjeId = polaganje.ID,
                PredmetId = predmetId,

                StudentImePrezime = polaganje.Korisnik != null
                    ? $"{polaganje.Korisnik.IME} {polaganje.Korisnik.PREZIME}"
                    : "",

                TestNaziv = polaganje.Test.NAZIV ?? "",
                Status = polaganje.STATUS ?? "",

                OstvareniBodovi = polaganje.OSTVARENI_BODOVI ?? 0,
                MaxBodovi = polaganje.MAX_BODOVI ?? 0,
                Procenat = polaganje.PROCENAT ?? 0,
                IsPassed = polaganje.IS_PASSED == "Y",

                Questions = questions
            };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GradeEssay(InstructorEssayAnswerGradeVM model)
        {
            var answer = await _dbContext.STUDENT_ODGOVORI
                .Include(x => x.PolaganjeTesta)
                    .ThenInclude(x => x.Test)
                .Include(x => x.PolaganjePitanje)
                    .ThenInclude(x => x.TestPitanje)
                .FirstOrDefaultAsync(x => x.ID == model.StudentOdgovorId);

            if (answer == null ||
                answer.PolaganjeTesta == null ||
                answer.PolaganjeTesta.Test == null ||
                answer.PolaganjePitanje == null ||
                answer.PolaganjePitanje.TestPitanje == null)
            {
                return NotFound();
            }

            var lokalniKorisnik = await GetCurrentLokalniKorisnikAsync();

            if (lokalniKorisnik == null)
                return Forbid();

            var predmetId = answer.PolaganjeTesta.Test.PREDMET_ID;

            var hasAccess = await _dbContext.PREDMET_INSTRUKTORI
                .AnyAsync(x =>
                    x.PREDMET_ID == predmetId &&
                    x.KORISNIK_ID == lokalniKorisnik.ID &&
                    x.AKTIVNO == "Y");

            if (!hasAccess)
                return Forbid();

            var maxBodovi = answer.PolaganjePitanje.TestPitanje.BODOVI;
            var uneseniBodovi = model.DodijeljeniBodovi ?? 0;

            if (uneseniBodovi < 0)
            {
                TempData["ErrorMessage"] = "Bodovi ne mogu biti manji od 0.";
                return RedirectToAction(nameof(Details), new { id = answer.POLAGANJE_TESTA_ID });
            }

            if (uneseniBodovi > maxBodovi)
            {
                TempData["ErrorMessage"] = $"Bodovi ne mogu biti veći od maksimalnih bodova pitanja ({maxBodovi}).";
                return RedirectToAction(nameof(Details), new { id = answer.POLAGANJE_TESTA_ID });
            }

            answer.OSTVARENI_BODOVI = uneseniBodovi;

            await _dbContext.SaveChangesAsync();

            await RecalculatePolaganjeResult(answer.POLAGANJE_TESTA_ID);

            TempData["SuccessMessage"] = "Bodovi su uspješno spašeni.";

            return RedirectToAction(nameof(Details), new { id = answer.POLAGANJE_TESTA_ID });
        }

        private async Task RecalculatePolaganjeResult(int polaganjeId)
        {
            var polaganje = await _dbContext.POLAGANJE_TESTA
                .Include(x => x.Test)
                .FirstOrDefaultAsync(x => x.ID == polaganjeId);

            if (polaganje == null || polaganje.Test == null)
                return;

            var pitanja = await _dbContext.POLAGANJE_PITANJA
                .Include(x => x.TestPitanje)
                    .ThenInclude(x => x.TipPitanja)
                .Where(x => x.POLAGANJE_TESTA_ID == polaganjeId)
                .OrderBy(x => x.REDOSLIJED)
                .ToListAsync();

            var studentOdgovori = await _dbContext.STUDENT_ODGOVORI
                .Where(x => x.POLAGANJE_TESTA_ID == polaganjeId)
                .ToListAsync();

            var testOdgovori = await _dbContext.TEST_PITANJE_ODGOVORI
                .Where(o => _dbContext.POLAGANJE_PITANJA.Any(pp =>
                    pp.POLAGANJE_TESTA_ID == polaganjeId &&
                    pp.TEST_PITANJE_ID == o.TEST_PITANJE_ID))
                .ToListAsync();

            decimal ostvareniBodovi = 0;
            decimal maxBodovi = 0;

            foreach (var pitanjePolaganja in pitanja)
            {
                var testPitanje = pitanjePolaganja.TestPitanje;

                if (testPitanje == null)
                    continue;

                maxBodovi += testPitanje.BODOVI;

                var tip = testPitanje.TipPitanja?.SIFRA?.Trim().ToUpper() ?? "";
                var parcijalnoBodovanje = (testPitanje.PARCIJALNO_BODOVANJE ?? "N").Trim().ToUpper() == "Y";

                var odgovoriStudentaZaPitanje = studentOdgovori
                    .Where(x => x.POLAGANJE_PITANJE_ID == pitanjePolaganja.ID)
                    .ToList();

                decimal bodoviZaPitanje = 0;

                if (tip == "ESSAY")
                {
                    bodoviZaPitanje = odgovoriStudentaZaPitanje
                        .Where(x => x.TEST_PITANJE_ODGOVOR_ID == null)
                        .Select(x => x.OSTVARENI_BODOVI ?? 0)
                        .FirstOrDefault();

                    if (bodoviZaPitanje < 0)
                        bodoviZaPitanje = 0;

                    if (bodoviZaPitanje > testPitanje.BODOVI)
                        bodoviZaPitanje = testPitanje.BODOVI;

                    ostvareniBodovi += bodoviZaPitanje;
                    continue;
                }

                var odabraniOdgovori = odgovoriStudentaZaPitanje
                    .Where(x => x.TEST_PITANJE_ODGOVOR_ID.HasValue)
                    .Select(x => x.TEST_PITANJE_ODGOVOR_ID!.Value)
                    .Distinct()
                    .ToList();

                var odgovoriZaPitanje = testOdgovori
                    .Where(x => x.TEST_PITANJE_ID == testPitanje.ID)
                    .ToList();

                var tacniOdgovori = odgovoriZaPitanje
                    .Where(x => x.IS_TACAN == "Y")
                    .Select(x => x.ID)
                    .ToList();

                if (tip == "SINGLE_CHOICE" || tip == "SINGLE")
                {
                    var isCorrect =
                        odabraniOdgovori.Count == 1 &&
                        tacniOdgovori.Count == 1 &&
                        odabraniOdgovori.First() == tacniOdgovori.First();

                    if (isCorrect)
                        bodoviZaPitanje = testPitanje.BODOVI;
                }
                else if (tip == "MULTIPLE_CHOICE" || tip == "MULTIPLE")
                {
                    if (!parcijalnoBodovanje)
                    {
                        var sveTacno = odabraniOdgovori
                            .OrderBy(x => x)
                            .SequenceEqual(tacniOdgovori.OrderBy(x => x));

                        if (sveTacno)
                            bodoviZaPitanje = testPitanje.BODOVI;
                    }
                    else
                    {
                        foreach (var odgovorId in odabraniOdgovori)
                        {
                            var odgovor = odgovoriZaPitanje.FirstOrDefault(x => x.ID == odgovorId);

                            if (odgovor == null)
                                continue;

                            if (odgovor.IS_TACAN == "Y")
                                bodoviZaPitanje += odgovor.BODOVI ?? 0;
                            else
                                bodoviZaPitanje -= odgovor.NEGATIVNI_BODOVI ?? 0;
                        }

                        if (bodoviZaPitanje < 0)
                            bodoviZaPitanje = 0;

                        if (bodoviZaPitanje > testPitanje.BODOVI)
                            bodoviZaPitanje = testPitanje.BODOVI;
                    }
                }

                ostvareniBodovi += bodoviZaPitanje;
            }

            var procenat = maxBodovi > 0
                ? Math.Round((ostvareniBodovi / maxBodovi) * 100, 2)
                : 0;

            polaganje.OSTVARENI_BODOVI = ostvareniBodovi;
            polaganje.MAX_BODOVI = maxBodovi;
            polaganje.PROCENAT = procenat;
            polaganje.IS_PASSED = procenat >= polaganje.Test.MIN_PROCENAT_PROLAZA ? "Y" : "N";
            polaganje.DATUM_IZMJENE = DateTime.Now;

            await _dbContext.SaveChangesAsync();
        }

        private async Task<Korisnik?> GetCurrentLokalniKorisnikAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentUserId))
                return null;

            return await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x =>
                    x.USER_ID == CurrentUserId &&
                    x.AKTIVAN == "Y");
        }
    }
}