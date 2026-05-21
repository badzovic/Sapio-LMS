using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Identity;
using SIA.Sapio_LMS.Areas.Admin.Models;
using SIA.Sapio_LMS.Services;

namespace SIA.Sapio_LMS.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class InternalUsersManagementController : BaseAdminController
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly InternalUsersApiService _internalUsersApiService;
        private readonly UserTrainingMatrixService _userTrainingMatrixService;

        public InternalUsersManagementController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        InternalUsersApiService internalUsersApiService,
        UserTrainingMatrixService userTrainingMatrixService)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _roleManager = roleManager;
            _internalUsersApiService = internalUsersApiService;
            _userTrainingMatrixService = userTrainingMatrixService;
        }

        public async Task<IActionResult> Details(string userId, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return NotFound();

            var identityUser = await _userManager.FindByIdAsync(userId);
            if (identityUser == null)
                return NotFound();

            var localUser = await _dbContext.KORISNICI
                .Include(x => x.Kompanija)
                .FirstOrDefaultAsync(x => x.USER_ID == userId);

            var userRoles = await _userManager.GetRolesAsync(identityUser);

            var model = new InternalUserDto
            {
                UserId = identityUser.Id,
                Ime = identityUser.FirstName,
                Prezime = identityUser.LastName,
                Email = identityUser.Email,
                HrmId = localUser?.HRM_ID ?? 0,
                ImeOca = localUser?.IME_OCA,
                Kompanija = localUser?.Kompanija != null ? localUser.Kompanija.NAZIV : localUser?.KOMPANIJA,
                AccessKey = localUser?.ACCESS_KEY
            };

            ViewBag.UserRoles = userRoles;
            ViewBag.AllRoles = new List<string> { "Admin", "Student", "Instruktor" };
            ViewBag.IsActive = localUser?.AKTIVAN == "Y";
            ViewBag.TrainingMatrix = await _userTrainingMatrixService.GetForRadnikAsync(localUser?.ACCESS_KEY);

            ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
            ? Url.Action("Index", "Users", new { area = "Admin", status = "all", type = "all" })
            : returnUrl;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string userId)
        {
            const string newPassword = "Sarajevo2026!";

            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["ImportMessage"] = "Nedostaje userId.";
                TempData["ImportErrors"] = "Reset password nije moguć bez userId.";
                return RedirectToAction(nameof(Details), new { userId });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ImportMessage"] = "Korisnik nije pronađen.";
                TempData["ImportErrors"] = "Traženi korisnik ne postoji u AspNetUsers.";
                return RedirectToAction(nameof(Index), "InternalUsers", new { area = "Admin" });
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (!result.Succeeded)
            {
                TempData["ImportMessage"] = "Reset password nije uspio.";
                TempData["ImportErrors"] = string.Join("\n", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
                return RedirectToAction(nameof(Details), new { userId });
            }

            TempData["ImportMessage"] = $"Password je resetovan za korisnika {user.UserName}.";
            return RedirectToAction(nameof(Details), new { userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleRole(string userId, string roleName)
        {
            var allowedRoles = new[] { "Admin", "Student", "Instruktor" };

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(roleName))
            {
                TempData["ImportMessage"] = "Nedostaju podaci za promjenu role.";
                TempData["ImportErrors"] = "userId ili roleName nije proslijeđen.";
                return RedirectToAction(nameof(Details), new { userId });
            }

            if (!allowedRoles.Contains(roleName))
            {
                TempData["ImportMessage"] = "Rola nije dozvoljena.";
                TempData["ImportErrors"] = $"Rola '{roleName}' nije podržana.";
                return RedirectToAction(nameof(Details), new { userId });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ImportMessage"] = "Korisnik nije pronađen.";
                TempData["ImportErrors"] = "Traženi korisnik ne postoji.";
                return RedirectToAction(nameof(Index), "InternalUsers", new { area = "Admin" });
            }

            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                TempData["ImportMessage"] = "Rola ne postoji u sistemu.";
                TempData["ImportErrors"] = $"Rola '{roleName}' nije pronađena u AspNetRoles.";
                return RedirectToAction(nameof(Details), new { userId });
            }

            IdentityResult result;

            if (await _userManager.IsInRoleAsync(user, roleName))
            {
                result = await _userManager.RemoveFromRoleAsync(user, roleName);

                if (!result.Succeeded)
                {
                    TempData["ImportMessage"] = "Uklanjanje role nije uspjelo.";
                    TempData["ImportErrors"] = string.Join("\n", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
                    return RedirectToAction(nameof(Details), new { userId });
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
                    return RedirectToAction(nameof(Details), new { userId });
                }

                TempData["ImportMessage"] = $"Rola '{roleName}' je dodijeljena korisniku {user.UserName}.";
            }

            return RedirectToAction(nameof(Details), new { userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncSingleUser(int hrmId, string userId)
        {
            try
            {
                var postojeci = await _dbContext.KORISNICI
                    .FirstOrDefaultAsync(x => x.HRM_ID == hrmId);

                if (postojeci == null)
                {
                    TempData["ImportMessage"] = "Korisnik ne postoji u lokalnoj bazi.";
                    TempData["ImportErrors"] = $"HRM_ID={hrmId} nije pronađen u LMS bazi.";
                    return RedirectToAction(nameof(Details), new { userId });
                }

                var identityUser = await _userManager.FindByIdAsync(userId);

                if (identityUser == null)
                {
                    TempData["ImportMessage"] = "Identity korisnik nije pronađen.";
                    TempData["ImportErrors"] = $"USER_ID={userId} nije pronađen u AspNetUsers.";
                    return RedirectToAction(nameof(Details), new { userId });
                }

                var apiUser = await _internalUsersApiService.GetUserByHrmIdAsync(hrmId);

                if (apiUser == null)
                {
                    postojeci.AKTIVAN = "N";

                    identityUser.IsActive = false;
                    identityUser.LockoutEnabled = true;
                    identityUser.LockoutEnd = DateTimeOffset.MaxValue;

                    var deactivateResult = await _userManager.UpdateAsync(identityUser);

                    if (!deactivateResult.Succeeded)
                    {
                        TempData["ImportMessage"] = "Korisnik nije pronađen u HRM API-u, ali deaktivacija Identity korisnika nije uspjela.";
                        TempData["ImportErrors"] = string.Join("\n", deactivateResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
                        return RedirectToAction(nameof(Details), new { userId });
                    }

                    await _dbContext.SaveChangesAsync();

                    TempData["ImportMessage"] = "Korisnik nije pronađen u HRM API-u i deaktiviran je u LMS-u.";
                    TempData["ImportErrors"] = $"HRM_ID={hrmId} ne postoji u API podacima.";

                    return RedirectToAction(nameof(Details), new { userId });
                }

                var novoIme = string.IsNullOrWhiteSpace(apiUser.Ime) ? null : apiUser.Ime.Trim();
                var novoPrezime = string.IsNullOrWhiteSpace(apiUser.Prezime) ? null : apiUser.Prezime.Trim();
                var novoImeOca = string.IsNullOrWhiteSpace(apiUser.ImeOca) ? null : apiUser.ImeOca.Trim();
                var noviEmail = string.IsNullOrWhiteSpace(apiUser.Email) ? null : apiUser.Email.Trim();
                var novaKompanija = string.IsNullOrWhiteSpace(apiUser.Kompanija) ? "MAS" : apiUser.Kompanija.Trim();

                bool hasChanges = false;

                // update KORISNICI
                if ((postojeci.IME ?? string.Empty).Trim() != (novoIme ?? string.Empty))
                {
                    postojeci.IME = novoIme;
                    hasChanges = true;
                }

                if ((postojeci.PREZIME ?? string.Empty).Trim() != (novoPrezime ?? string.Empty))
                {
                    postojeci.PREZIME = novoPrezime;
                    hasChanges = true;
                }

                if ((postojeci.IME_OCA ?? string.Empty).Trim() != (novoImeOca ?? string.Empty))
                {
                    postojeci.IME_OCA = novoImeOca;
                    hasChanges = true;
                }

                if ((postojeci.EMAIL ?? string.Empty).Trim() != (noviEmail ?? string.Empty))
                {
                    postojeci.EMAIL = noviEmail;
                    hasChanges = true;
                }

                if (postojeci.ACCESS_KEY != apiUser.AccessKey)
                {
                    postojeci.ACCESS_KEY = apiUser.AccessKey;
                    hasChanges = true;
                }

                if ((postojeci.KOMPANIJA ?? string.Empty).Trim() != novaKompanija)
                {
                    postojeci.KOMPANIJA = novaKompanija;
                    hasChanges = true;
                }

                // update AspNetUsers
                bool identityChanged = false;

                if ((identityUser.FirstName ?? string.Empty).Trim() != (novoIme ?? string.Empty))
                {
                    identityUser.FirstName = novoIme ?? string.Empty;
                    identityChanged = true;
                }

                if ((identityUser.LastName ?? string.Empty).Trim() != (novoPrezime ?? string.Empty))
                {
                    identityUser.LastName = novoPrezime ?? string.Empty;
                    identityChanged = true;
                }

                if ((identityUser.Email ?? string.Empty).Trim() != (noviEmail ?? string.Empty))
                {
                    identityUser.Email = noviEmail;
                    identityUser.NormalizedEmail = string.IsNullOrWhiteSpace(noviEmail)
                        ? null
                        : noviEmail.ToUpperInvariant();

                    identityChanged = true;
                }

                if (identityChanged)
                {
                    var identityResult = await _userManager.UpdateAsync(identityUser);

                    if (!identityResult.Succeeded)
                    {
                        TempData["ImportMessage"] = "Greška prilikom update-a Identity korisnika.";
                        TempData["ImportErrors"] = string.Join("\n", identityResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
                        return RedirectToAction(nameof(Details), new { userId });
                    }

                    hasChanges = true;
                }

                if (hasChanges)
                {
                    await _dbContext.SaveChangesAsync();
                    TempData["ImportMessage"] = $"Korisnik (HRM_ID={hrmId}) uspješno syncovan u obje tabele.";
                }
                else
                {
                    TempData["ImportMessage"] = $"Korisnik (HRM_ID={hrmId}) je već ažuran. Nema izmjena.";
                }
            }
            catch (Exception ex)
            {
                TempData["ImportMessage"] = "Greška prilikom sync-a korisnika.";
                TempData["ImportErrors"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int hrmId, string userId)
        {
            try
            {
                var korisnik = await _dbContext.KORISNICI
                    .FirstOrDefaultAsync(x => x.HRM_ID == hrmId);

                if (korisnik == null)
                {
                    TempData["ImportMessage"] = "Korisnik nije pronađen u bazi.";
                    return RedirectToAction(nameof(Details), new { userId });
                }

                var identityUser = await _userManager.FindByIdAsync(userId);

                if (identityUser == null)
                {
                    TempData["ImportMessage"] = "Identity korisnik nije pronađen.";
                    return RedirectToAction(nameof(Details), new { userId });
                }

                bool isCurrentlyActive = korisnik.AKTIVAN == "Y";

                if (isCurrentlyActive)
                {
                    korisnik.AKTIVAN = "N";

                    identityUser.IsActive = false;
                    identityUser.LockoutEnabled = true;
                    identityUser.LockoutEnd = DateTimeOffset.MaxValue;

                    TempData["ImportMessage"] = "Korisnik je deaktiviran.";
                }
                else
                {
                    korisnik.AKTIVAN = "Y";

                    identityUser.IsActive = true;
                    identityUser.LockoutEnabled = true;
                    identityUser.LockoutEnd = null;

                    TempData["ImportMessage"] = "Korisnik je aktiviran.";
                }

                await _userManager.UpdateAsync(identityUser);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                TempData["ImportMessage"] = "Greška prilikom promjene statusa.";
                TempData["ImportErrors"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { userId });
        }
    }
}