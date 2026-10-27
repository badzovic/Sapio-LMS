
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio_LMS.Areas.Admin.Models;

namespace SIA.Sapio_LMS.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class CustomTrainingMatrixController : BaseAdminController
    {
        private readonly ApplicationDbContext _dbContext;

        public CustomTrainingMatrixController(
            ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }


        [HttpGet]
        public async Task<IActionResult> Index(
            int? korisnikId,
            string? search)
        {
            var model = new CustomTrainingMatrixIndexVM
            {
                KorisnikId = korisnikId,
                Search = search
            };

            // Svi aktivni LMS korisnici, bez obzira na tip.
            var korisnici = await _dbContext.KORISNICI
                .AsNoTracking()
                .Where(x => x.AKTIVAN == "Y")
                .OrderBy(x => x.PREZIME)
                .ThenBy(x => x.IME)
                .Select(x => new
                {
                    x.ID,
                    x.IME,
                    x.PREZIME
                })
                .ToListAsync();

            ViewBag.Korisnici = korisnici
                .Select(x => new SelectListItem
                {
                    Value = x.ID.ToString(),
                    Text = $"{x.IME} {x.PREZIME}".Trim(),
                    Selected = korisnikId == x.ID
                })
                .ToList();

            // Aktivni predmeti dostupni za dodavanje

            var predmeti = await _dbContext.PREDMETI
                .AsNoTracking()
                .Where(x => x.AKTIVNO == "Y" && x.VRSTA_PROGRAMA_ID == 1)
                .OrderBy(x => x.NAZIV)
                .Select(x => new
                {
                    x.ID,
                    x.NAZIV
                })
                .ToListAsync();


            ViewBag.Predmeti = predmeti
                .Select(x => new SelectListItem
                {
                    Value = x.ID.ToString(),
                    Text = x.NAZIV
                })
                .ToList();

            if (!korisnikId.HasValue)
            {
                return View(model);
            }

            // Provjera odabranog korisnika
            var korisnik = await _dbContext.KORISNICI
                .AsNoTracking()
                .Where(x =>
                    x.ID == korisnikId.Value &&
                    x.AKTIVAN == "Y")
                .Select(x => new
                {
                    x.ID,
                    x.IME,
                    x.PREZIME
                })
                .FirstOrDefaultAsync();

            if (korisnik == null)
            {
                TempData["ErrorMessage"] =
                    "Odabrani korisnik nije pronađen ili nije aktivan.";

                return RedirectToAction(nameof(Index));
            }

            model.ImePrezime =
                $"{korisnik.IME} {korisnik.PREZIME}".Trim();

            // Aktivne i deaktivirane veze korisnik-predmet

            model.Predmeti = await _dbContext.KORISNIK_PREDMETI
                .AsNoTracking()
                .Where(x => x.KORISNIK_ID == korisnikId.Value)
                .OrderByDescending(x => x.AKTIVNO)
                .ThenByDescending(x => x.DATUM_DODJELE)
                .Select(x => new CustomTrainingMatrixItemVM
                {
                    ID = x.ID,

                    KorisnikId = x.KORISNIK_ID,
                    PredmetId = x.PREDMET_ID,

                    NazivPredmeta = x.PREDMET != null
                        ? x.PREDMET.NAZIV
                        : "-",

                    DatumDodjele = x.DATUM_DODJELE,

                    DodijelioUserId = x.DODIJELIO_USER_ID,

                    DodijelioImePrezime = _dbContext.Users
                        .Where(u => u.Id == x.DODIJELIO_USER_ID)
                        .Select(u => (u.FirstName + " " + u.LastName).Trim())
                        .FirstOrDefault() ?? "-",

                    DeaktiviraoImePrezime = _dbContext.Users
                        .Where(u => u.Id == x.DEAKTIVIRAO_USER_ID)
                        .Select(u => (u.FirstName + " " + u.LastName).Trim())
                        .FirstOrDefault() ?? "-",

                    Aktivno = x.AKTIVNO == "Y",

                    DatumDeaktivacije = x.DATUM_DEAKTIVACIJE,

                    Napomena = x.NAPOMENA
                })
                .ToListAsync();


            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSubject(
            int korisnikId,
            int predmetId,
            string? napomena)
        {
            // Provjera korisnika
            var korisnik = await _dbContext.KORISNICI
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ID == korisnikId &&
                    x.AKTIVAN == "Y");

            if (korisnik == null)
            {
                TempData["ErrorMessage"] =
                    "Korisnik nije pronađen ili nije aktivan.";

                return RedirectToAction(nameof(Index));
            }

            // Provjera predmeta
            var predmet = await _dbContext.PREDMETI
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ID == predmetId &&
                    x.AKTIVNO == "Y");

            if (predmet == null)
            {
                TempData["ErrorMessage"] =
                    "Predmet nije pronađen ili nije aktivan.";

                return RedirectToAction(
                    nameof(Index),
                    new { korisnikId });
            }

            // Provjera postoji li već veza korisnika i predmeta
            var postojecaVeza = await _dbContext.KORISNIK_PREDMETI
                .FirstOrDefaultAsync(x =>
                    x.KORISNIK_ID == korisnikId &&
                    x.PREDMET_ID == predmetId);

            if (postojecaVeza != null)
            {
                if (postojecaVeza.AKTIVNO == "Y")
                {
                    TempData["ErrorMessage"] =
                        "Ovaj predmet je već aktivan u Custom matrici korisnika.";
                }
                else
                {
                    TempData["ErrorMessage"] =
                        "Predmet je ranije deaktiviran. Koristite opciju Reaktiviraj.";
                }

                return RedirectToAction(
                    nameof(Index),
                    new { korisnikId });
            }

            // Nova veza korisnika i predmeta
            var novaVeza = new KorisnikPredmet
            {
                KORISNIK_ID = korisnikId,
                PREDMET_ID = predmetId,

                DATUM_DODJELE = DateTime.Now,
                DODIJELIO_USER_ID = CurrentUserId,

                AKTIVNO = "Y",

                NAPOMENA = string.IsNullOrWhiteSpace(napomena)
                    ? null
                    : napomena.Trim()
            };

            _dbContext.KORISNIK_PREDMETI.Add(novaVeza);

            await _dbContext.SaveChangesAsync();

            TempData["ImportMessage"] =
                $"Predmet '{predmet.NAZIV}' uspješno je dodan u Custom matricu korisnika.";

            return RedirectToAction(
                nameof(Index),
                new { korisnikId });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id, int korisnikId)
        {
            var veza = await _dbContext.KORISNIK_PREDMETI
                .FirstOrDefaultAsync(x =>
                    x.ID == id &&
                    x.KORISNIK_ID == korisnikId);

            if (veza == null)
            {
                TempData["ErrorMessage"] =
                    "Veza korisnika i predmeta nije pronađena.";

                return RedirectToAction(
                    nameof(Index),
                    new { korisnikId });
            }

            if (veza.AKTIVNO != "Y")
            {
                TempData["ErrorMessage"] =
                    "Ovaj predmet je već deaktiviran.";

                return RedirectToAction(
                    nameof(Index),
                    new { korisnikId });
            }

            // Deaktiviramo obavezu bez brisanja zapisa
            veza.AKTIVNO = "N";
            veza.DATUM_DEAKTIVACIJE = DateTime.Now;
            veza.DEAKTIVIRAO_USER_ID = CurrentUserId;

            await _dbContext.SaveChangesAsync();

            TempData["ImportMessage"] =
                "Predmet je uspješno deaktiviran u Custom matrici korisnika.";

            return RedirectToAction(
                nameof(Index),
                new { korisnikId });
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate(int id, int korisnikId)
        {
            var veza = await _dbContext.KORISNIK_PREDMETI
                .FirstOrDefaultAsync(x =>
                    x.ID == id &&
                    x.KORISNIK_ID == korisnikId);

            if (veza == null)
            {
                TempData["ErrorMessage"] =
                    "Veza korisnika i predmeta nije pronađena.";

                return RedirectToAction(
                    nameof(Index),
                    new { korisnikId });
            }

            if (veza.AKTIVNO == "Y")
            {
                TempData["ErrorMessage"] =
                    "Ovaj predmet je već aktivan u Custom matrici.";

                return RedirectToAction(
                    nameof(Index),
                    new { korisnikId });
            }

            // Provjera da predmet i dalje postoji i da je aktivan
            var predmetAktivan = await _dbContext.PREDMETI
                .AsNoTracking()
                .AnyAsync(x =>
                    x.ID == veza.PREDMET_ID &&
                    x.AKTIVNO == "Y" &&
                    x.VRSTA_PROGRAMA_ID == 1);

            if (!predmetAktivan)
            {
                TempData["ErrorMessage"] =
                    "Predmet više nije aktivan ili ne pripada dozvoljenoj vrsti programa.";

                return RedirectToAction(
                    nameof(Index),
                    new { korisnikId });
            }

            // Ponovo aktiviramo postojeću vezu
            veza.AKTIVNO = "Y";
            veza.DATUM_DODJELE = DateTime.Now;
            veza.DODIJELIO_USER_ID = CurrentUserId;

            veza.DATUM_DEAKTIVACIJE = null;
            veza.DEAKTIVIRAO_USER_ID = null;

            await _dbContext.SaveChangesAsync();

            TempData["ImportMessage"] =
                "Predmet je uspješno reaktiviran u Custom matrici korisnika.";

            return RedirectToAction(
                nameof(Index),
                new { korisnikId });
        }

    }
}
