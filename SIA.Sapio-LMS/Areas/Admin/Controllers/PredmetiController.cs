using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio_LMS.Areas.Admin.Models;
using SIA.Sapio_LMS.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Identity;
using SIA.Sapio.LMS.Data.Identity;

namespace SIA.Sapio_LMS.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PredmetiController : BaseAdminController
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly V3ProgramsApiService _v3ProgramsApiService;
        private readonly ILogger<PredmetiController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private const string InstructorRoleName = "Instruktor";

        private const int ProgramVrstaProgramaId = 1;

        public PredmetiController(
        ApplicationDbContext dbContext,
        V3ProgramsApiService v3ProgramsApiService,
        ILogger<PredmetiController> logger,
        UserManager<ApplicationUser> userManager)
        {
            _dbContext = dbContext;
            _v3ProgramsApiService = v3ProgramsApiService;
            _logger = logger;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? status)
        {
            status = string.IsNullOrWhiteSpace(status) ? "active" : status.ToLower();

            var query = _dbContext.PREDMETI
                .Where(x => x.VRSTA_PROGRAMA_ID == ProgramVrstaProgramaId)
                .AsQueryable();

            if (status == "active")
                query = query.Where(x => x.AKTIVNO == "Y");
            else if (status == "inactive")
                query = query.Where(x => x.AKTIVNO == "N");

            var model = await query
                .OrderBy(x => x.NAZIV)
                .Select(x => new PredmetVM
                {
                    ID = x.ID,
                    Naziv = x.NAZIV,
                    NazivEng = x.NAZIV_ENG,
                    PointSifra = x.POINT_SIFRA,
                    Aktivno = x.AKTIVNO
                })
                .ToListAsync();

            ViewBag.Status = status;
            return View(model);
        }

        public async Task<IActionResult> Details(int id, string? returnUrl = null)
        {
            var entity = await _dbContext.PREDMETI
                .FirstOrDefaultAsync(x => x.ID == id && x.VRSTA_PROGRAMA_ID == ProgramVrstaProgramaId);

            if (entity == null)
                return NotFound();

            var aktivniInstruktori = await _dbContext.PREDMET_INSTRUKTORI
                .Include(x => x.Korisnik)
                .Where(x => x.PREDMET_ID == id && x.AKTIVNO == "Y")
                .OrderBy(x => x.Korisnik!.PREZIME)
                .ThenBy(x => x.Korisnik!.IME)
                .Select(x => new PredmetInstruktorVM
                {
                    ID = x.ID,
                    KorisnikId = x.KORISNIK_ID,
                    ImePrezime = ((x.Korisnik!.IME ?? "") + " " + (x.Korisnik.PREZIME ?? "")).Trim(),
                    DatumOd = x.DATUM_OD
                })
                .ToListAsync();

            var aktivniKorisnikIds = aktivniInstruktori
                .Select(x => x.KorisnikId)
                .ToHashSet();

            var sviAktivniKorisnici = await _dbContext.KORISNICI
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

            var historijaInstruktora = await _dbContext.PREDMET_INSTRUKTORI
                .Include(x => x.Korisnik)
                .Where(x => x.PREDMET_ID == id)
                .OrderByDescending(x => x.DATUM_OD)
                .Select(x => new PredmetInstruktorVM
                {
                    ID = x.ID,
                    KorisnikId = x.KORISNIK_ID,
                    ImePrezime = ((x.Korisnik!.IME ?? "") + " " + (x.Korisnik.PREZIME ?? "")).Trim(),
                    DatumOd = x.DATUM_OD,
                    DatumDo = x.DATUM_DO,
                    Aktivno = x.AKTIVNO
                })
                .ToListAsync();

            var dostupniInstruktori = sviAktivniKorisnici
                .Where(x => !aktivniKorisnikIds.Contains(x.ID))
                .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = x.ID.ToString(),
                    Text = ((x.IME ?? "") + " " + (x.PREZIME ?? "")).Trim()
                })
                .ToList();

            var model = new PredmetVM
            {
                ID = entity.ID,
                Naziv = entity.NAZIV,
                NazivEng = entity.NAZIV_ENG,
                PrakticnaObuka = entity.PRAKTICNA_OBUKA,
                PointSifra = entity.POINT_SIFRA,
                Aktivno = entity.AKTIVNO,
                Instruktori = aktivniInstruktori,
                DostupniInstruktori = dostupniInstruktori,
                HistorijaInstruktora = historijaInstruktora
            };

            ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                ? Url.Action("Index", "Predmeti", new { area = "Admin", status = "active" })
                : returnUrl;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Details(PredmetVM model, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(model.Naziv))
                ModelState.AddModelError(nameof(model.Naziv), "Naziv je obavezan.");

            if (!ModelState.IsValid)
            {
                ViewBag.ReturnUrl = returnUrl;
                return View(model);
            }

            var entity = await _dbContext.PREDMETI
                .FirstOrDefaultAsync(x => x.ID == model.ID && x.VRSTA_PROGRAMA_ID == ProgramVrstaProgramaId);

            if (entity == null)
                return NotFound();

            entity.NAZIV = model.Naziv.Trim();
            entity.NAZIV_ENG = string.IsNullOrWhiteSpace(model.NazivEng) ? null : model.NazivEng.Trim();
            entity.POINT_SIFRA = model.PointSifra;
            entity.DATUM_IZMJENE = DateTime.Now;
            entity.PRAKTICNA_OBUKA = model.PrakticnaObuka;

            await _dbContext.SaveChangesAsync();

            TempData["ImportMessage"] = "Predmet je uspješno izmijenjen.";

            if (!string.IsNullOrWhiteSpace(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Details), new { id = model.ID });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id, string? returnUrl = null)
        {
            var entity = await _dbContext.PREDMETI
                .FirstOrDefaultAsync(x => x.ID == id && x.VRSTA_PROGRAMA_ID == ProgramVrstaProgramaId);

            if (entity == null)
            {
                TempData["ImportMessage"] = "Predmet nije pronađen.";
                return RedirectToAction(nameof(Index));
            }

            var now = DateTime.Now;
            var isActive = entity.AKTIVNO == "Y";

            if (isActive)
            {
                entity.AKTIVNO = "N";
                entity.DATUM_IZMJENE = now;

                var aktivneVeze = await _dbContext.PREDMET_INSTRUKTORI
                    .Where(x => x.PREDMET_ID == id && x.AKTIVNO == "Y")
                    .ToListAsync();

                foreach (var veza in aktivneVeze)
                {
                    veza.AKTIVNO = "N";
                    veza.DATUM_DO = now;
                    veza.DATUM_IZMJENE = now;
                }

                await _dbContext.SaveChangesAsync();

                foreach (var veza in aktivneVeze)
                {
                    var imaJosAktivnihPredmeta = await _dbContext.PREDMET_INSTRUKTORI
                        .AnyAsync(x => x.KORISNIK_ID == veza.KORISNIK_ID && x.AKTIVNO == "Y");

                    if (!imaJosAktivnihPredmeta)
                    {
                        var korisnik = await _dbContext.KORISNICI
                            .FirstOrDefaultAsync(x => x.ID == veza.KORISNIK_ID);

                        if (korisnik != null && !string.IsNullOrWhiteSpace(korisnik.USER_ID))
                        {
                            var identityUser = await _userManager.FindByIdAsync(korisnik.USER_ID);

                            if (identityUser != null && await _userManager.IsInRoleAsync(identityUser, InstructorRoleName))
                            {
                                await _userManager.RemoveFromRoleAsync(identityUser, InstructorRoleName);
                            }
                        }
                    }
                }

                TempData["ImportMessage"] = "Predmet je deaktiviran i svi aktivni instruktori su skinuti sa predmeta.";
            }
            else
            {
                entity.AKTIVNO = "Y";
                entity.DATUM_IZMJENE = now;

                await _dbContext.SaveChangesAsync();

                TempData["ImportMessage"] = "Predmet je aktiviran.";
            }

            return RedirectToAction(nameof(Details), new { id, returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportFromV3(int vrstaId)
        {
            if (vrstaId <= 0)
            {
                TempData["ImportMessage"] = "Vrsta programa nije ispravna.";
                return RedirectToAction(nameof(Index));
            }

            var vrstaPrograma = await _dbContext.VRSTE_PROGRAMA
                .FirstOrDefaultAsync(x => x.V3_ID_VRSTA == vrstaId && x.AKTIVNO == "Y");

            if (vrstaPrograma == null)
            {
                TempData["ImportMessage"] = $"Vrsta programa sa V3 ID = {vrstaId} ne postoji ili nije aktivna.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var v3Programs = await _v3ProgramsApiService.GetProgramsByVrstaAsync(vrstaId);

                if (v3Programs == null || !v3Programs.Any())
                {
                    TempData["ImportMessage"] = $"API nije vratio nijedan program za V3 vrstu {vrstaId}.";
                    return RedirectToAction(nameof(Index));
                }

                var existingV3Ids = await _dbContext.PREDMETI
                    .Where(x => x.V3_ID_OBUKE.HasValue)
                    .Select(x => x.V3_ID_OBUKE!.Value)
                    .ToListAsync();

                var existingSet = existingV3Ids.ToHashSet();
                var now = DateTime.Now;

                var newPredmeti = v3Programs
                    .Where(x => !existingSet.Contains(x.IdObuke))
                    .Select(x => new Predmet
                    {
                        V3_ID_OBUKE = x.IdObuke,
                        OZNAKA = string.IsNullOrWhiteSpace(x.Oznaka) ? null : x.Oznaka.Trim(),
                        NAZIV = string.IsNullOrWhiteSpace(x.Naziv) ? "(bez naziva)" : x.Naziv.Trim(),
                        NAZIV_ENG = string.IsNullOrWhiteSpace(x.NazivEng) ? null : x.NazivEng.Trim(),
                        V3_ID_VRSTA = x.IdVrsta,
                        VRSTA_PROGRAMA_ID = vrstaPrograma.ID,
                        XRAY_ALLOW = x.XrayAllow,
                        POINT_SIFRA = x.PointSifra,
                        AKTIVNO = "Y",
                        DATUM_KREIRANJA = now,
                        DATUM_IZMJENE = null,
                        DATUM_ZADNJEG_SYNCA = now
                    })
                    .ToList();

                if (newPredmeti.Any())
                {
                    await _dbContext.PREDMETI.AddRangeAsync(newPredmeti);
                    await _dbContext.SaveChangesAsync();
                }

                TempData["ImportMessage"] =
                    $"Import završen za V3 vrstu {vrstaId}. Ubačeno: {newPredmeti.Count}, preskočeno: {v3Programs.Count - newPredmeti.Count}.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri importu predmeta iz V3 za vrstaId={VrstaId}", vrstaId);

                TempData["ImportMessage"] = "Greška pri importu predmeta iz V3.";
                TempData["ImportErrors"] = ex.Message;

                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddInstructor(int predmetId, int korisnikId, string? returnUrl = null)
        {
            var predmet = await _dbContext.PREDMETI
    .FirstOrDefaultAsync(x => x.ID == predmetId && x.VRSTA_PROGRAMA_ID == ProgramVrstaProgramaId);

            if (predmet == null)
            {
                TempData["ImportMessage"] = "Predmet nije pronađen.";
                return RedirectToAction(nameof(Index));
            }

            if (predmet.AKTIVNO != "Y")
            {
                TempData["ImportMessage"] = "Nije moguće dodati instruktora na neaktivan predmet.";
                return RedirectToAction(nameof(Details), new { id = predmetId, returnUrl });
            }

            var korisnik = await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x => x.ID == korisnikId && x.AKTIVAN == "Y");

            if (korisnik == null)
            {
                TempData["ImportMessage"] = "Instruktor nije pronađen ili nije aktivan.";
                return RedirectToAction(nameof(Details), new { id = predmetId, returnUrl });
            }

            var alreadyActive = await _dbContext.PREDMET_INSTRUKTORI
                .AnyAsync(x => x.PREDMET_ID == predmetId
                            && x.KORISNIK_ID == korisnikId
                            && x.AKTIVNO == "Y");

            if (alreadyActive)
            {
                TempData["ImportMessage"] = "Instruktor je već dodijeljen ovom predmetu.";
                return RedirectToAction(nameof(Details), new { id = predmetId, returnUrl });
            }

            var now = DateTime.Now;

            var veza = new PredmetInstruktor
            {
                PREDMET_ID = predmetId,
                KORISNIK_ID = korisnikId,
                AKTIVNO = "Y",
                DATUM_OD = now,
                DATUM_DO = null,
                DATUM_KREIRANJA = now,
                DATUM_IZMJENE = null
            };

            _dbContext.PREDMET_INSTRUKTORI.Add(veza);
            await _dbContext.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(korisnik.USER_ID))
            {
                var identityUser = await _userManager.FindByIdAsync(korisnik.USER_ID);

                if (identityUser != null && !await _userManager.IsInRoleAsync(identityUser, InstructorRoleName))
                {
                    var roleResult = await _userManager.AddToRoleAsync(identityUser, InstructorRoleName);

                    if (!roleResult.Succeeded)
                    {
                        TempData["ImportMessage"] = "Instruktor je dodijeljen predmetu, ali dodjela Instruktor role nije uspjela.";
                        TempData["ImportErrors"] = string.Join("\n", roleResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
                        return RedirectToAction(nameof(Details), new { id = predmetId, returnUrl });
                    }
                }
            }

            TempData["ImportMessage"] = "Instruktor je dodijeljen predmetu i rola je usklađena.";
            return RedirectToAction(nameof(Details), new { id = predmetId, returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveInstructor(int id, int predmetId, string? returnUrl = null)
        {
            var veza = await _dbContext.PREDMET_INSTRUKTORI
                .FirstOrDefaultAsync(x => x.ID == id && x.PREDMET_ID == predmetId && x.AKTIVNO == "Y");

            if (veza == null)
            {
                TempData["ImportMessage"] = "Aktivna veza instruktora nije pronađena.";
                return RedirectToAction(nameof(Details), new { id = predmetId, returnUrl });
            }

            var korisnikId = veza.KORISNIK_ID;
            var now = DateTime.Now;

            veza.AKTIVNO = "N";
            veza.DATUM_DO = now;
            veza.DATUM_IZMJENE = now;

            await _dbContext.SaveChangesAsync();

            var imaJosAktivnihPredmeta = await _dbContext.PREDMET_INSTRUKTORI
                .AnyAsync(x => x.KORISNIK_ID == korisnikId && x.AKTIVNO == "Y");

            if (!imaJosAktivnihPredmeta)
            {
                var korisnik = await _dbContext.KORISNICI
                    .FirstOrDefaultAsync(x => x.ID == korisnikId);

                if (korisnik != null && !string.IsNullOrWhiteSpace(korisnik.USER_ID))
                {
                    var identityUser = await _userManager.FindByIdAsync(korisnik.USER_ID);

                    if (identityUser != null && await _userManager.IsInRoleAsync(identityUser, InstructorRoleName))
                    {
                        var roleResult = await _userManager.RemoveFromRoleAsync(identityUser, InstructorRoleName);

                        if (!roleResult.Succeeded)
                        {
                            TempData["ImportMessage"] = "Instruktor je skinut sa predmeta, ali uklanjanje Instructor role nije uspjelo.";
                            TempData["ImportErrors"] = string.Join("\n", roleResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
                            return RedirectToAction(nameof(Details), new { id = predmetId, returnUrl });
                        }
                    }
                }
            }

            TempData["ImportMessage"] = imaJosAktivnihPredmeta
                ? "Instruktor je skinut sa ovog predmeta. Rola Instructor ostaje jer predaje drugi predmet."
                : "Instruktor je skinut sa predmeta i rola Instructor je uklonjena.";

            return RedirectToAction(nameof(Details), new { id = predmetId, returnUrl });
        }

    }
}