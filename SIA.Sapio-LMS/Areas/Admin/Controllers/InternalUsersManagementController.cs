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

        public InternalUsersManagementController(
         ApplicationDbContext dbContext,
         UserManager<ApplicationUser> userManager,
         RoleManager<IdentityRole> roleManager,
         InternalUsersApiService internalUsersApiService)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _roleManager = roleManager;
            _internalUsersApiService = internalUsersApiService;
        }

        public async Task<IActionResult> Details(string userId)
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
                var apiUser = await _internalUsersApiService.GetUserByHrmIdAsync(hrmId);

                if (apiUser == null)
                {
                    TempData["ImportMessage"] = "Korisnik nije pronađen u API-u.";
                    TempData["ImportErrors"] = $"HRM_ID={hrmId} ne postoji u API podacima.";
                    return RedirectToAction(nameof(Details), new { userId });
                }

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

                // update KORISNICI
                postojeci.IME = apiUser.Ime;
                postojeci.PREZIME = apiUser.Prezime;
                postojeci.IME_OCA = apiUser.ImeOca;
                postojeci.EMAIL = string.IsNullOrWhiteSpace(apiUser.Email) ? null : apiUser.Email.Trim();
                postojeci.ACCESS_KEY = apiUser.AccessKey;
                postojeci.KOMPANIJA = apiUser.Kompanija ?? "MAS";
                postojeci.AKTIVAN = "Y";

                // update AspNetUsers
                identityUser.FirstName = apiUser.Ime ?? string.Empty;
                identityUser.LastName = apiUser.Prezime ?? string.Empty;
                identityUser.Email = string.IsNullOrWhiteSpace(apiUser.Email) ? null : apiUser.Email.Trim();
                identityUser.NormalizedEmail = string.IsNullOrWhiteSpace(apiUser.Email)
                    ? null
                    : apiUser.Email.Trim().ToUpperInvariant();

                // samo ako želiš da username prati email
                if (!string.IsNullOrWhiteSpace(apiUser.Email))
                {
                    identityUser.UserName = apiUser.Email.Trim();
                    identityUser.NormalizedUserName = apiUser.Email.Trim().ToUpperInvariant();
                }

                var identityResult = await _userManager.UpdateAsync(identityUser);

                if (!identityResult.Succeeded)
                {
                    TempData["ImportMessage"] = "Greška prilikom update-a Identity korisnika.";
                    TempData["ImportErrors"] = string.Join("\n", identityResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
                    return RedirectToAction(nameof(Details), new { userId });
                }

                await _dbContext.SaveChangesAsync();

                TempData["ImportMessage"] = $"Korisnik (HRM_ID={hrmId}) uspješno syncovan u obje tabele.";
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
                    // deaktivacija
                    korisnik.AKTIVAN = "N";

                    identityUser.LockoutEnabled = true;
                    identityUser.LockoutEnd = DateTimeOffset.MaxValue;

                    TempData["ImportMessage"] = "Korisnik je deaktiviran.";
                }
                else
                {
                    // aktivacija
                    korisnik.AKTIVAN = "Y";

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