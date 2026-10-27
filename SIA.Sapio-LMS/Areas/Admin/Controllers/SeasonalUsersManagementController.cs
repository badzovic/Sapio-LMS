using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Identity;
using SIA.Sapio_LMS.Areas.Admin.Models;

namespace SIA.Sapio_LMS.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SeasonalUsersManagementController : BaseAdminController
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public SeasonalUsersManagementController(
            ApplicationDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Details(int id, string? returnUrl = null)
        {
            var seasonalTipId = await _dbContext.TIPOVI_KORISNIKA
                .Where(x => x.Naziv == "Sezonski radnik")
                .Select(x => (int?)x.ID)
                .FirstOrDefaultAsync();

            if (seasonalTipId == null)
                return NotFound();

            var lokalniUser = await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x => x.ID == id && x.TIP_KORISNIKA_ID == seasonalTipId.Value);

            if (lokalniUser == null)
                return NotFound();

            ApplicationUser? identityUser = null;
            IList<string> userRoles = new List<string>();

            if (!string.IsNullOrWhiteSpace(lokalniUser.USER_ID))
            {
                identityUser = await _userManager.FindByIdAsync(lokalniUser.USER_ID);

                if (identityUser != null)
                {
                    userRoles = (await _userManager.GetRolesAsync(identityUser))
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x.Trim())
                        .Distinct()
                        .ToList();
                }
            }

            var allRoles = await _dbContext.Roles
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => x.Name!.Trim())
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            ViewBag.UserRoles = userRoles;
            ViewBag.AllRoles = allRoles;
            ViewBag.IsActive =
                lokalniUser.AKTIVAN == "Y" &&
                identityUser?.IsActive == true &&
                identityUser.IsDeleted == false;
            ViewBag.TipKorisnikaText = "Sezonski radnik";

            var model = new ExternalUserDetailsVM
            {
                ID = lokalniUser.ID,
                ExternalV3Id = lokalniUser.EXTERNI_V3_ID,
                UserId = lokalniUser.USER_ID,
                Ime = lokalniUser.IME,
                Prezime = lokalniUser.PREZIME,
                ImeOca = lokalniUser.IME_OCA,
                MjestoRodjenja = lokalniUser.MJESTO_RODJENJA,
                Email = identityUser?.Email ?? lokalniUser.EMAIL,
                Kompanija = null,
                KompanijaId = null,
                TipKorisnikaId = lokalniUser.TIP_KORISNIKA_ID,
                Aktivan = lokalniUser.AKTIVAN
            };

            ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
               ? Url.Action("Index", "Users", new { area = "Admin", status = "all", type = "all" })
               : returnUrl;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleRole(int id, string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                TempData["ImportMessage"] = "Rola nije proslijeđena.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var seasonalTipId = await _dbContext.TIPOVI_KORISNIKA
                .Where(x => x.Naziv == "Sezonski radnik")
                .Select(x => (int?)x.ID)
                .FirstOrDefaultAsync();

            if (seasonalTipId == null)
            {
                TempData["ImportMessage"] = "Tip korisnika 'Sezonski radnik' nije pronađen.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var lokalniUser = await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x => x.ID == id && x.TIP_KORISNIKA_ID == seasonalTipId.Value);

            if (lokalniUser == null || string.IsNullOrWhiteSpace(lokalniUser.USER_ID))
            {
                TempData["ImportMessage"] = "Korisnik nije pronađen.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var user = await _userManager.FindByIdAsync(lokalniUser.USER_ID);
            if (user == null)
            {
                TempData["ImportMessage"] = "Identity korisnik nije pronađen.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                TempData["ImportMessage"] = "Rola ne postoji u sistemu.";
                return RedirectToAction(nameof(Details), new { id });
            }

            IdentityResult result;

            if (await _userManager.IsInRoleAsync(user, roleName))
            {
                result = await _userManager.RemoveFromRoleAsync(user, roleName);

                if (!result.Succeeded)
                {
                    TempData["ImportMessage"] = "Uklanjanje role nije uspjelo.";
                    TempData["ImportErrors"] = string.Join("\n", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
                    return RedirectToAction(nameof(Details), new { id });
                }

                TempData["ImportMessage"] = $"Rola '{roleName}' je uklonjena korisniku {user.UserName}.";
            }
            else
            {
                result = await _userManager.AddToRoleAsync(user, roleName);

                if (!result.Succeeded)
                {
                    TempData["ImportMessage"] = "Dodjela role nije uspjela.";
                    TempData["ImportErrors"] = string.Join("\n", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
                    return RedirectToAction(nameof(Details), new { id });
                }

                TempData["ImportMessage"] = $"Rola '{roleName}' je dodijeljena korisniku {user.UserName}.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            try
            {
                /*
                 * ========================================================
                 * 1. PRONALAZAK TIPA SEZONSKOG KORISNIKA
                 * ========================================================
                 */

                var seasonalTipId = await _dbContext.TIPOVI_KORISNIKA
                    .Where(x => x.Naziv == "Sezonski radnik")
                    .Select(x => (int?)x.ID)
                    .FirstOrDefaultAsync();

                if (seasonalTipId == null)
                {
                    TempData["ImportMessage"] =
                        "Tip korisnika 'Sezonski radnik' nije pronađen.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                /*
                 * ========================================================
                 * 2. PRONALAZAK LOKALNOG KORISNIKA
                 * ========================================================
                 */

                var lokalniUser = await _dbContext.KORISNICI
                    .FirstOrDefaultAsync(x =>
                        x.ID == id &&
                        x.TIP_KORISNIKA_ID == seasonalTipId.Value);

                if (lokalniUser == null)
                {
                    TempData["ImportMessage"] =
                        "Sezonski korisnik nije pronađen.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                /*
                 * ========================================================
                 * 3. PROVJERA IDENTITY RAČUNA
                 * ========================================================
                 */

                if (string.IsNullOrWhiteSpace(lokalniUser.USER_ID))
                {
                    TempData["ImportMessage"] =
                        "Korisnik nema povezan Identity račun.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                var identityUser =
                    await _userManager.FindByIdAsync(lokalniUser.USER_ID);

                if (identityUser == null)
                {
                    TempData["ImportMessage"] =
                        "Povezani Identity korisnik nije pronađen.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                /*
                 * ========================================================
                 * 4. ODREĐIVANJE NOVOG STATUSA
                 * ========================================================
                 *
                 * Korisnik je aktivan samo ako su obje tabele
                 * usklađene i Identity nije soft-deleteovan.
                 */

                bool trenutnoAktivan =
                    lokalniUser.AKTIVAN == "Y" &&
                    identityUser.IsActive &&
                    !identityUser.IsDeleted;

                bool noviStatus = !trenutnoAktivan;

                /*
                 * ========================================================
                 * 5. TRANSAKCIJA
                 * ========================================================
                 */

                await using var transaction =
                    await _dbContext.Database.BeginTransactionAsync();

                try
                {
                    /*
                     * ====================================================
                     * 6. KORISNICI - POSLOVNA TABELA
                     * ====================================================
                     */

                    lokalniUser.AKTIVAN = noviStatus ? "Y" : "N";

                    /*
                     * ====================================================
                     * 7. ASP.NET IDENTITY
                     * ====================================================
                     */

                    identityUser.IsActive = noviStatus;
                    identityUser.IsDeleted = !noviStatus;
                    identityUser.LockoutEnabled = true;

                    if (noviStatus)
                    {
                        /*
                         * Ponovna aktivacija korisnika.
                         * Uklanjamo trajnu blokadu koja je
                         * postavljena prilikom deaktivacije.
                         */

                        if (identityUser.LockoutEnd ==
                            DateTimeOffset.MaxValue)
                        {
                            identityUser.LockoutEnd = null;
                        }
                    }
                    else
                    {
                        /*
                         * Deaktivacija korisnika.
                         * Postavljamo trajnu blokadu prijave.
                         */

                        identityUser.LockoutEnd =
                            DateTimeOffset.MaxValue;
                    }

                    /*
                     * ====================================================
                     * 8. UPDATE IDENTITY
                     * ====================================================
                     */

                    var updateResult =
                        await _userManager.UpdateAsync(identityUser);

                    if (!updateResult.Succeeded)
                    {
                        throw new InvalidOperationException(
                            "Identity update nije uspio: " +
                            string.Join(
                                " | ",
                                updateResult.Errors.Select(
                                    x => x.Description)));
                    }

                    /*
                     * ====================================================
                     * 9. ČUVANJE PROMJENA
                     * ====================================================
                     */

                    await _dbContext.SaveChangesAsync();

                    await transaction.CommitAsync();

                    /*
                     * ====================================================
                     * 10. PORUKA ADMINISTRATORU
                     * ====================================================
                     */

                    TempData["ImportMessage"] = noviStatus
                        ? "Sezonski korisnik je uspješno aktiviran."
                        : "Sezonski korisnik je uspješno deaktiviran.";

                    return RedirectToAction(nameof(Details), new { id });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                TempData["ImportMessage"] =
                    "Greška prilikom promjene statusa sezonskog korisnika.";

                TempData["ImportErrors"] = ex.Message;

                return RedirectToAction(nameof(Details), new { id });
            }
            finally
            {
                _dbContext.ChangeTracker.Clear();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id)
        {
            const string newPassword = "Sarajevo2026!";

            var seasonalTipId = await _dbContext.TIPOVI_KORISNIKA
                .Where(x => x.Naziv == "Sezonski radnik")
                .Select(x => (int?)x.ID)
                .FirstOrDefaultAsync();

            if (seasonalTipId == null)
            {
                TempData["ImportMessage"] = "Tip korisnika 'Sezonski radnik' nije pronađen.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var lokalniUser = await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x => x.ID == id && x.TIP_KORISNIKA_ID == seasonalTipId.Value);

            if (lokalniUser == null || string.IsNullOrWhiteSpace(lokalniUser.USER_ID))
            {
                TempData["ImportMessage"] = "Korisnik nije pronađen.";
                TempData["ImportErrors"] = "Nije moguće resetovati password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var user = await _userManager.FindByIdAsync(lokalniUser.USER_ID);
            if (user == null)
            {
                TempData["ImportMessage"] = "Identity korisnik nije pronađen.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (!result.Succeeded)
            {
                TempData["ImportMessage"] = "Reset password nije uspio.";
                TempData["ImportErrors"] = string.Join("\n", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["ImportMessage"] = $"Password je resetovan za korisnika {user.UserName}.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}