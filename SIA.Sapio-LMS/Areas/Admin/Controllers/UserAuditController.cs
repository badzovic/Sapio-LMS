using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Areas.Admin;
using SIA.Sapio.LMS.Data;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Identity;
using SIA.Sapio_LMS.Areas.Admin.Models;

namespace SIA.Sapio.LMS.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UserAuditController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public UserAuditController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string section = "import", string status = "all")
        {
            section = NormalizeSection(section);
            status = NormalizeStatus(status);

            var model = new UserAuditIndexVM
            {
                Section = section,

                ImportUnreadCount =
                    await _context.EVIDENCIJA_PROMJENA_TIPA_KORISNIKA
                        .AsNoTracking()
                        .CountAsync(x => !x.PREGLEDANO),

                SyncUnreadCount =
                    await _context.EVIDENCIJA_SINHRONIZACIJE_KORISNIKA
                        .AsNoTracking()
                        .CountAsync(x => !x.PREGLEDANO),

                DeactivationUnreadCount =
                    await _context.EVIDENCIJA_DEAKTIVACIJE_KORISNIKA
                        .AsNoTracking()
                        .CountAsync(x => !x.PREGLEDANO)
            };

            if (section == "import")
            {
                var query =
                    _context.EVIDENCIJA_PROMJENA_TIPA_KORISNIKA
                        .AsNoTracking()
                        .AsQueryable();

                if (status == "unreviewed")
                {
                    query = query.Where(x => !x.PREGLEDANO);
                }
                else if (status == "reviewed")
                {
                    query = query.Where(x => x.PREGLEDANO);
                }

                model.ImportItems = await query
                    .OrderByDescending(x => x.DATUM_KREIRANJA)
                    .ThenByDescending(x => x.ID)
                    .Select(x => new ImportUserAuditItemVM
                    {
                        Id = x.ID,
                        KorisnikId = x.KORISNIK_ID,
                        Ime = x.NOVO_IME,
                        Prezime = x.NOVO_PREZIME,
                        HrmId = x.HRM_ID,
                        StariTipKorisnika = GetTipKorisnikaNaziv(x.STARI_TIP_KORISNIKA_ID),
                        NoviTipKorisnika = GetTipKorisnikaNaziv(x.NOVI_TIP_KORISNIKA_ID),
                        DatumPromjene = x.DATUM_KREIRANJA,
                        PokrenuoUserId = null,
                        PregledaoUserId = x.PREGLEDAO_USER_ID,
                        Pregledao =
                            _context.Users
                                .Where(u =>
                                    u.Id == x.PREGLEDAO_USER_ID)
                                .Select(u =>
                                    ((u.FirstName ?? "") + " " +
                                     (u.LastName ?? "")).Trim())
                                .FirstOrDefault(),
                        DatumPregleda = x.DATUM_PREGLEDA
                    })
                    .ToListAsync();
            }
            else if (section == "sync")
            {
                var query =
                    _context.EVIDENCIJA_SINHRONIZACIJE_KORISNIKA
                        .AsNoTracking()
                        .AsQueryable();

                if (status == "unreviewed")
                {
                    query = query.Where(x => !x.PREGLEDANO);
                }
                else if (status == "reviewed")
                {
                    query = query.Where(x => x.PREGLEDANO);
                }

                model.SyncItems = await query
                    .OrderByDescending(x => x.DATUM_KREIRANJA)
                    .ThenByDescending(x => x.ID)
                    .Select(x => new SyncAuditItemVM
                    {
                        Id = x.ID,
                        KorisnikId = x.KORISNIK_ID,
                        UserId = x.USER_ID,
                        HrmId = x.HRM_ID,
                        StaroIme = x.STARO_IME,
                        NovoIme = x.NOVO_IME,
                        StaroPrezime = x.STARO_PREZIME,
                        NovoPrezime = x.NOVO_PREZIME,
                        StaroImeOca = x.STARO_IME_OCA,
                        NovoImeOca = x.NOVO_IME_OCA,
                        StariEmail = x.STARI_EMAIL,
                        NoviEmail = x.NOVI_EMAIL,
                        StariAccessKey = x.STARI_ACCESS_KEY,
                        NoviAccessKey = x.NOVI_ACCESS_KEY,
                        PromijenjenaPolja = x.PROMIJENJENA_POLJA,
                        Opis = x.OPIS,
                        DatumPromjene = x.DATUM_KREIRANJA,
                        Pregledano = x.PREGLEDANO,
                        PregledaoUserId = x.PREGLEDAO_USER_ID,
                        Pregledao =
                            _context.Users
                                .Where(u =>
                                    u.Id == x.PREGLEDAO_USER_ID)
                                .Select(u =>
                                    ((u.FirstName ?? "") + " " +
                                     (u.LastName ?? "")).Trim())
                                .FirstOrDefault(),
                        DatumPregleda =
                            x.DATUM_PREGLEDA
                    })
                    .ToListAsync();
            }
            else if (section == "deactivation")
            {
                var query =
                    _context.EVIDENCIJA_DEAKTIVACIJE_KORISNIKA
                        .AsNoTracking()
                        .AsQueryable();

                if (status == "unreviewed")
                {
                    query = query.Where(x => !x.PREGLEDANO);
                }
                else if (status == "reviewed")
                {
                    query = query.Where(x => x.PREGLEDANO);
                }

                model.DeactivationItems = await query
                    .OrderByDescending(x => x.DATUM_KREIRANJA)
                    .ThenByDescending(x => x.ID)
                    .Select(x => new DeactivationAuditItemVM
                    {
                        Id = x.ID,
                        KorisnikId = x.KORISNIK_ID,
                        HrmId = x.HRM_ID,
                        Ime = x.IME,
                        Prezime = x.PREZIME,
                        Email = x.EMAIL,
                        AccessKey = x.ACCESS_KEY,
                        RazlogDeaktivacije = x.RAZLOG_DEAKTIVACIJE,
                        Opis = x.OPIS,
                        DatumKreiranja = x.DATUM_KREIRANJA,
                        Pregledano = x.PREGLEDANO,
                        Pregledao =
                            _context.Users
                                .Where(u =>
                                    u.Id == x.PREGLEDAO_USER_ID)
                                .Select(u =>
                                    ((u.FirstName ?? "") + " " +
                                     (u.LastName ?? "")).Trim())
                                .FirstOrDefault(),
                        DatumPregleda =
                            x.DATUM_PREGLEDA
                    })
                    .ToListAsync();
            }

            ViewBag.Status = status;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkImportAsReviewed(int id, string status = "all")
        {
            status = NormalizeStatus(status);

            var audit =
                await _context.EVIDENCIJA_PROMJENA_TIPA_KORISNIKA
                    .FirstOrDefaultAsync(x => x.ID == id);

            if (audit == null)
            {
                TempData["ErrorMessage"] =
                    "Audit zapis nije pronađen.";

                return RedirectToImportAudit(status);
            }

            if (audit.PREGLEDANO)
            {
                TempData["InfoMessage"] =
                    "Audit zapis je već označen kao pregledan.";

                return RedirectToImportAudit(status);
            }

            var currentUserId =
                _userManager.GetUserId(User);

            audit.PREGLEDANO = true;
            audit.PREGLEDAO_USER_ID = currentUserId;
            audit.DATUM_PREGLEDA = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Audit zapis je označen kao pregledan.";

            return RedirectToImportAudit(status);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllImportsAsReviewed(string status = "unreviewed")
        {
            status = NormalizeStatus(status);

            var auditZapisi =
                await _context.EVIDENCIJA_PROMJENA_TIPA_KORISNIKA
                    .Where(x => !x.PREGLEDANO)
                    .ToListAsync();

            if (auditZapisi.Count == 0)
            {
                TempData["InfoMessage"] =
                    "Nema nepregledanih import audit zapisa.";

                return RedirectToImportAudit(status);
            }

            var currentUserId =
                _userManager.GetUserId(User);

            var datumPregleda = DateTime.Now;

            foreach (var audit in auditZapisi)
            {
                audit.PREGLEDANO = true;
                audit.PREGLEDAO_USER_ID = currentUserId;
                audit.DATUM_PREGLEDA = datumPregleda;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Broj označenih audit zapisa: {auditZapisi.Count}.";

            return RedirectToImportAudit(status);
        }

        private IActionResult RedirectToImportAudit(string status)
        {
            return RedirectToAction(
                nameof(Index),
                new
                {
                    section = "import",
                    status
                });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkSyncAsReviewed(int id, string status = "all")
        {
            status = NormalizeStatus(status);

            var audit =
                await _context.EVIDENCIJA_SINHRONIZACIJE_KORISNIKA
                    .FirstOrDefaultAsync(x => x.ID == id);

            if (audit == null)
            {
                TempData["ErrorMessage"] =
                    "Audit zapis nije pronađen.";

                return RedirectToSyncAudit(status);
            }

            if (audit.PREGLEDANO)
            {
                TempData["InfoMessage"] =
                    "Audit zapis je već označen kao pregledan.";

                return RedirectToSyncAudit(status);
            }

            var currentUserId =
                _userManager.GetUserId(User);

            audit.PREGLEDANO = true;
            audit.PREGLEDAO_USER_ID = currentUserId;
            audit.DATUM_PREGLEDA = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Audit zapis je označen kao pregledan.";

            return RedirectToSyncAudit(status);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllSyncsAsReviewed(string status = "unreviewed")
        {
            status = NormalizeStatus(status);

            var auditZapisi =
                await _context.EVIDENCIJA_SINHRONIZACIJE_KORISNIKA
                    .Where(x => !x.PREGLEDANO)
                    .ToListAsync();

            if (auditZapisi.Count == 0)
            {
                TempData["InfoMessage"] =
                    "Nema nepregledanih sync audit zapisa.";

                return RedirectToSyncAudit(status);
            }

            var currentUserId =
                _userManager.GetUserId(User);

            var datumPregleda = DateTime.Now;

            foreach (var audit in auditZapisi)
            {
                audit.PREGLEDANO = true;
                audit.PREGLEDAO_USER_ID = currentUserId;
                audit.DATUM_PREGLEDA = datumPregleda;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Broj označenih audit zapisa: {auditZapisi.Count}.";

            return RedirectToSyncAudit(status);
        }

        private IActionResult RedirectToSyncAudit(string status = "all")
        {
            return RedirectToAction(
                nameof(Index),
                new
                {
                    section = "sync",
                    status
                });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkDeactivationAsReviewed(int id, string? search, int? length)
        {
            var item = await _context.EVIDENCIJA_DEAKTIVACIJE_KORISNIKA
                .FirstOrDefaultAsync(x => x.ID == id);

            if (item == null)
            {
                return NotFound();
            }

            if (!item.PREGLEDANO)
            {
                item.PREGLEDANO = true;
                item.PREGLEDAO_USER_ID = _userManager.GetUserId(User);
                item.DATUM_PREGLEDA = DateTime.Now;

                await _context.SaveChangesAsync();
            }

            return RedirectToDeactivationAudit(search, length);
        }

        private IActionResult RedirectToDeactivationAudit(string? search = null, int? length = null)
        {
            var routeValues = new RouteValueDictionary
            {
                ["tab"] = "deactivation"
            };

            if (!string.IsNullOrWhiteSpace(search))
            {
                routeValues["search"] = search;
            }

            if (length.HasValue)
            {
                routeValues["length"] = length.Value;
            }

            return RedirectToAction(nameof(Index), routeValues);
        }

        private static string NormalizeSection(string? section)
        {
            section = section?.Trim().ToLowerInvariant();

            return section switch
            {
                "sync" => "sync",
                "deactivation" => "deactivation",
                _ => "import"
            };
        }

        private static string NormalizeStatus(string? status)
        {
            status = status?.Trim().ToLowerInvariant();

            return status switch
            {
                "reviewed" => "reviewed",
                "unreviewed" => "unreviewed",
                _ => "all"
            };
        }

        private static string GetTipKorisnikaNaziv(int? tipKorisnikaId)
        {
            return tipKorisnikaId switch
            {
                1 => "Interni",
                2 => "Eksterni",
                3 => "Sezonski",
                _ => "-"
            };
        }
    }
}