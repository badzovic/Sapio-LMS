using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio_LMS.Areas.Instructor.Models;

namespace SIA.Sapio_LMS.Areas.Instructor.Controllers
{
    [Area("Instructor")]
    public class SubjectQuestionsController : BaseInstructorController
    {
        private readonly ApplicationDbContext _dbContext;

        public SubjectQuestionsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index(int predmetId)
        {
            var predmet = await GetInstructorPredmetAsync(predmetId);

            if (predmet == null)
                return NotFound();

            var model = await _dbContext.PREDMET_PITANJA
                .Include(x => x.TipPitanja)
                .Include(x => x.TipTezinePitanja)
                .Where(x => x.PREDMET_ID == predmetId /*&& x.AKTIVNO == "Y"*/)
                .OrderByDescending(x => x.DATUM_KREIRANJA)
                .Select(x => new SubjectQuestionVM
                {
                    ID = x.ID,
                    PredmetId = x.PREDMET_ID,
                    PredmetNaziv = predmet.NAZIV,
                    TekstPitanja = x.TEKST_PITANJA,
                    TipPitanja = x.TipPitanja != null ? x.TipPitanja.NAZIV : "-",
                    Tezina = x.TipTezinePitanja != null ? x.TipTezinePitanja.NAZIV : "-",
                    Bodovi = x.BODOVI,
                    NegativniBodovi = x.NEGATIVNI_BODOVI,
                    ParcijalnoBodovanje = x.PARCIJALNO_BODOVANJE,
                    Aktivno = x.AKTIVNO,
                    DatumKreiranja = x.DATUM_KREIRANJA
                })
                .ToListAsync();

            ViewBag.PredmetId = predmet.ID;
            ViewBag.PredmetNaziv = predmet.NAZIV;

            return View(model);
        }

        public async Task<IActionResult> Create(int predmetId)
        {
            var predmet = await GetInstructorPredmetAsync(predmetId);

            if (predmet == null)
                return NotFound();

            var model = new CreateSubjectQuestionVM
            {
                PredmetId = predmet.ID,
                PredmetNaziv = predmet.NAZIV,
                Bodovi = 1,
                ParcijalnoBodovanje = "N",
                Odgovori = new List<CreateSubjectQuestionAnswerVM>
                {
                    new CreateSubjectQuestionAnswerVM { Redoslijed = 1 },
                    new CreateSubjectQuestionAnswerVM { Redoslijed = 2 }
                }
            };

            await PopulateCreateListsAsync(model);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateSubjectQuestionVM model)
        {
            var predmet = await GetInstructorPredmetAsync(model.PredmetId);

            if (predmet == null)
                return NotFound();

            model.PredmetNaziv = predmet.NAZIV;

            var tipPitanja = await _dbContext.TIPOVI_PITANJA
                .FirstOrDefaultAsync(x => x.ID == model.TipPitanjaId && x.AKTIVNO == "Y");

            if (tipPitanja == null)
                ModelState.AddModelError(nameof(model.TipPitanjaId), "Tip pitanja je obavezan.");

            var tipTezine = await _dbContext.TIPOVI_TEZINE_PITANJA
                .FirstOrDefaultAsync(x => x.ID == model.TipTezineId && x.AKTIVNO == "Y");

            if (tipTezine == null)
                ModelState.AddModelError(nameof(model.TipTezineId), "Težina pitanja je obavezna.");

            if (string.IsNullOrWhiteSpace(model.TekstPitanja))
                ModelState.AddModelError(nameof(model.TekstPitanja), "Tekst pitanja je obavezno.");

            if (model.Bodovi <= 0)
                ModelState.AddModelError(nameof(model.Bodovi), "Ukupan broj bodova mora biti veći od 0.");

            var tipSifra = tipPitanja?.SIFRA;

            var aktivniOdgovori = (model.Odgovori ?? new List<CreateSubjectQuestionAnswerVM>())
                .Where(x => !string.IsNullOrWhiteSpace(x.TekstOdgovora))
                .ToList();

            if (tipSifra == "SINGLE_CHOICE")
            {
                if (aktivniOdgovori.Count < 2)
                {
                    ModelState.AddModelError("", "Pitanje sa jednim tačnim odgovorom mora imati najmanje 2 ponuđena odgovora.");
                }

                if (aktivniOdgovori.Count(x => x.IsTacan) != 1)
                {
                    ModelState.AddModelError("", "Pitanje sa jednim tačnim odgovorom mora imati tačno jedan označen tačan odgovor.");
                }
            }

            if (tipSifra == "MULTIPLE_CHOICE")
            {
                if (aktivniOdgovori.Count < 2)
                {
                    ModelState.AddModelError("", "Pitanje sa više tačnih odgovora mora imati najmanje 2 ponuđena odgovora.");
                }

                if (!aktivniOdgovori.Any(x => x.IsTacan))
                {
                    ModelState.AddModelError("", "Pitanje sa više tačnih odgovora mora imati barem jedan tačan odgovor.");
                }
            }

            if (tipSifra == "ESSAY")
            {
                aktivniOdgovori.Clear();
                model.ParcijalnoBodovanje = "N";
                model.NegativniBodovi = null;
            }

            if (tipSifra != "ESSAY")
            {
                var tacniOdgovori = aktivniOdgovori.Where(x => x.IsTacan).ToList();
                var netacniOdgovori = aktivniOdgovori.Where(x => !x.IsTacan).ToList();

                if (tipSifra == "SINGLE_CHOICE")
                {
                    foreach (var odgovor in aktivniOdgovori)
                    {
                        odgovor.Bodovi = odgovor.IsTacan
                            ? Math.Round(model.Bodovi, 2)
                            : 0;

                        odgovor.NegativniBodovi = 0;
                    }

                    model.NegativniBodovi = 0;
                    model.ParcijalnoBodovanje = "N";
                }

                if (tipSifra == "MULTIPLE_CHOICE")
                {
                    var brojTacnih = tacniOdgovori.Count;
                    var brojNetacnih = netacniOdgovori.Count;

                    var bodoviPoTacnom = brojTacnih > 0
                        ? Math.Round(model.Bodovi / brojTacnih, 2)
                        : 0;

                    var kazneniPoNetacnom = brojNetacnih > 0
                        ? Math.Round(model.Bodovi / brojNetacnih, 2)
                        : 0;

                    foreach (var odgovor in aktivniOdgovori)
                    {
                        odgovor.Bodovi = odgovor.IsTacan ? bodoviPoTacnom : 0;
                        odgovor.NegativniBodovi = odgovor.IsTacan ? 0 : kazneniPoNetacnom;
                    }

                    model.NegativniBodovi = kazneniPoNetacnom;
                }
            }

            if (!ModelState.IsValid)
            {
                if (model.Odgovori == null || !model.Odgovori.Any())
                {
                    model.Odgovori = new List<CreateSubjectQuestionAnswerVM>
            {
                new CreateSubjectQuestionAnswerVM { Redoslijed = 1 },
                new CreateSubjectQuestionAnswerVM { Redoslijed = 2 }
            };
                }

                await PopulateCreateListsAsync(model);
                return View(model);
            }

            var now = DateTime.Now;

            using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                var pitanje = new PredmetPitanje
                {
                    PREDMET_ID = model.PredmetId,
                    TIP_PITANJA_ID = model.TipPitanjaId,
                    TIP_TEZINE_ID = model.TipTezineId,
                    TEKST_PITANJA = model.TekstPitanja.Trim(),
                    BODOVI = model.Bodovi,
                    NEGATIVNI_BODOVI = model.NegativniBodovi,
                    PARCIJALNO_BODOVANJE = model.ParcijalnoBodovanje == "Y" ? "Y" : "N",
                    AKTIVNO = "Y",
                    CREATED_BY_USER_ID = CurrentUserId,
                    DATUM_KREIRANJA = now,
                    DATUM_IZMJENE = null
                };

                _dbContext.PREDMET_PITANJA.Add(pitanje);
                await _dbContext.SaveChangesAsync();

                if (tipSifra != "ESSAY")
                {
                    var redoslijed = 1;

                    foreach (var odgovor in aktivniOdgovori)
                    {
                        var odgovorEntity = new PredmetPitanjeOdgovor
                        {
                            PITANJE_ID = pitanje.ID,
                            TEKST_ODGOVORA = odgovor.TekstOdgovora.Trim(),
                            IS_TACAN = odgovor.IsTacan ? "Y" : "N",
                            BODOVI = odgovor.Bodovi,
                            NEGATIVNI_BODOVI = odgovor.NegativniBodovi,
                            REDOSLIJED = redoslijed,
                            AKTIVNO = "Y",
                            DATUM_KREIRANJA = now,
                            DATUM_IZMJENE = null
                        };

                        _dbContext.PREDMET_PITANJE_ODGOVORI.Add(odgovorEntity);
                        redoslijed++;
                    }

                    await _dbContext.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                TempData["ImportMessage"] = "Pitanje je uspješno kreirano.";

                return RedirectToAction(nameof(Index), new { predmetId = model.PredmetId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                TempData["ImportMessage"] = "Greška prilikom kreiranja pitanja.";
                TempData["ImportErrors"] = ex.Message;

                await PopulateCreateListsAsync(model);
                return View(model);
            }
        }

        private async Task PopulateCreateListsAsync(CreateSubjectQuestionVM model)
        {
            model.TipoviPitanja = await _dbContext.TIPOVI_PITANJA
                .Where(x => x.AKTIVNO == "Y")
                .OrderBy(x => x.ID)
                .Select(x => new SelectListItem
                {
                    Value = x.ID.ToString(),
                    Text = x.NAZIV
                })
                .ToListAsync();

            model.TipoviTezine = await _dbContext.TIPOVI_TEZINE_PITANJA
                .Where(x => x.AKTIVNO == "Y")
                .OrderBy(x => x.REDOSLIJED)
                .Select(x => new SelectListItem
                {
                    Value = x.ID.ToString(),
                    Text = x.NAZIV
                })
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

        public async Task<IActionResult> Details(int id)
        {
            var pitanje = await _dbContext.PREDMET_PITANJA
                .Include(x => x.Predmet)
                .Include(x => x.TipPitanja)
                .Include(x => x.TipTezinePitanja)
                .FirstOrDefaultAsync(x => x.ID == id);

            if (pitanje == null)
                return NotFound();

            var predmet = await GetInstructorPredmetAsync(pitanje.PREDMET_ID);

            if (predmet == null)
                return NotFound();

            var odgovori = await _dbContext.PREDMET_PITANJE_ODGOVORI
                .Where(x => x.PITANJE_ID == id)
                .OrderBy(x => x.REDOSLIJED)
                .Select(x => new SubjectQuestionAnswerDetailsVM
                {
                    ID = x.ID,
                    TekstOdgovora = x.TEKST_ODGOVORA,
                    IsTacan = x.IS_TACAN,
                    Redoslijed = x.REDOSLIJED,
                    Bodovi = x.BODOVI,
                    NegativniBodovi = x.NEGATIVNI_BODOVI,
                    Aktivno = x.AKTIVNO
                })
                .ToListAsync();

            var model = new SubjectQuestionDetailsVM
            {
                ID = pitanje.ID,
                PredmetId = pitanje.PREDMET_ID,
                PredmetNaziv = predmet.NAZIV,
                TekstPitanja = pitanje.TEKST_PITANJA,
                TipPitanja = pitanje.TipPitanja?.NAZIV ?? "-",
                TipPitanjaSifra = pitanje.TipPitanja?.SIFRA ?? "",
                Tezina = pitanje.TipTezinePitanja?.NAZIV ?? "-",
                Bodovi = pitanje.BODOVI,
                NegativniBodovi = pitanje.NEGATIVNI_BODOVI,
                ParcijalnoBodovanje = pitanje.PARCIJALNO_BODOVANJE,
                Aktivno = pitanje.AKTIVNO,
                DatumKreiranja = pitanje.DATUM_KREIRANJA,
                DatumIzmjene = pitanje.DATUM_IZMJENE,
                Odgovori = odgovori
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id, int predmetId)
        {
            var predmet = await GetInstructorPredmetAsync(predmetId);

            if (predmet == null)
                return NotFound();

            var pitanje = await _dbContext.PREDMET_PITANJA
                .FirstOrDefaultAsync(x => x.ID == id && x.PREDMET_ID == predmetId);

            if (pitanje == null)
                return NotFound();

            pitanje.AKTIVNO = pitanje.AKTIVNO == "Y" ? "N" : "Y";
            pitanje.DATUM_IZMJENE = DateTime.Now;

            await _dbContext.SaveChangesAsync();

            TempData["ImportMessage"] = pitanje.AKTIVNO == "Y"
                ? "Pitanje je aktivirano."
                : "Pitanje je deaktivirano.";

            return RedirectToAction(nameof(Details), new { id });
        }



    }
}