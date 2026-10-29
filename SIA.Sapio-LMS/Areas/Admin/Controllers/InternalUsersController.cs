using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio.LMS.Data.Identity;
using SIA.Sapio_LMS.Services;
using SIA.Sapio_LMS.Services.InternalUsersSync;

namespace SIA.Sapio_LMS.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class InternalUsersController : BaseAdminController
    {
        private readonly InternalUsersApiService _internalUsersApiService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<InternalUsersController> _logger;
        private readonly IInternalUsersSyncService _internalUsersSyncService;

        public InternalUsersController(
        InternalUsersApiService internalUsersApiService,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ILogger<InternalUsersController> logger,
        IInternalUsersSyncService internalUsersSyncService)
        {
            _internalUsersApiService = internalUsersApiService;
            _userManager = userManager;
            _dbContext = dbContext;
            _logger = logger;
            _internalUsersSyncService = internalUsersSyncService;
        }

        //podatke uzme apijem
        //public async Task<IActionResult> Index()
        //{
        //    var users = await _internalUsersApiService.GetUsersAsync();
        //    return View(users);
        //}

        public async Task<IActionResult> Index()
        {
            var users = await (
                from k in _dbContext.KORISNICI

                join u in _userManager.Users
                    on k.USER_ID equals u.Id into identityUsers

                from u in identityUsers.DefaultIfEmpty()

                where k.TIP_KORISNIKA_ID == 1
                      && k.AKTIVAN == "Y"

                select new SIA.Sapio_LMS.Areas.Admin.Models.InternalUserDto
                {
                    HrmId = k.HRM_ID ?? 0,
                    Ime = k.IME,
                    Prezime = k.PREZIME,
                    ImeOca = k.IME_OCA,
                    Jmb = null,

                    // Jedini izvor emaila za UI:
                    // AspNetUsers.Email
                    Email = u != null ? u.Email : null,

                    Kompanija = k.Kompanija != null
                        ? k.Kompanija.NAZIV
                        : k.KOMPANIJA,

                    AccessKey = k.ACCESS_KEY,
                    UserId = k.USER_ID
                })
                .OrderBy(x => x.Prezime)
                .ThenBy(x => x.Ime)
                .ToListAsync();

            return View(users);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportAll()
        {
            var result =
                await _internalUsersSyncService.RunImportAsync();

            TempData["ImportMessage"] = result.Message;

            if (result.ErrorMessages.Any())
            {
                TempData["ImportErrors"] =
                    string.Join(
                        Environment.NewLine,
                        result.ErrorMessages.Take(20));
            }

            return RedirectToAction(
                "Index",
                "Users",
                new
                {
                    area = "Admin",
                    status = "all",
                    type = "internal"
                });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncInternalUsers()
        {
            try
            {
                var importResult = await _internalUsersSyncService.RunImportAsync();
                var syncResult = await _internalUsersSyncService.RunSyncAsync();
                var deactivationResult = await _internalUsersSyncService.RunDeactivationAsync();

                var errors = importResult.ErrorMessages
                    .Concat(syncResult.ErrorMessages)
                    .Concat(deactivationResult.ErrorMessages)
                    .Take(20)
                    .ToList();

                var failed = importResult.Failed
                           + syncResult.Failed
                           + deactivationResult.Failed;

                return Json(new
                {
                    success = failed == 0 && errors.Count == 0 && !deactivationResult.ApiReturnedNoUsers,
                    message = "Kompletna sinhronizacija internih korisnika je završena.",
                    imported = importResult.Imported,
                    converted = importResult.Converted,
                    updated = syncResult.Updated,
                    deactivated = deactivationResult.Deactivated,
                    failed,
                    deactivationSkipped = deactivationResult.ApiReturnedNoUsers,
                    errors
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Došlo je do neočekivane greške tokom sinhronizacije korisnika."
                });
            }
        }


        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> ResetAllPasswords()
        //{
        //    const string newPassword = "Sarajevo2026!";
        //    const string excludedUserId = "f85dd151-df66-4ee9-8ce7-741f2ee9e569";

        //    int updated = 0;
        //    int failed = 0;
        //    var errorMessages = new List<string>();

        //    var users = await _userManager.Users
        //        .Where(u => u.Id != excludedUserId)
        //        .ToListAsync();

        //    foreach (var user in users)
        //    {
        //        try
        //        {
        //            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        //            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

        //            if (result.Succeeded)
        //            {
        //                updated++;
        //            }
        //            else
        //            {
        //                failed++;
        //                var errors = string.Join(" | ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
        //                errorMessages.Add($"{user.UserName}: {errors}");
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            failed++;
        //            errorMessages.Add($"{user.UserName}: {ex.Message}");
        //        }
        //    }

        //    TempData["ImportMessage"] = $"Reset password završen. Updated: {updated}, Failed: {failed}";

        //    if (errorMessages.Any())
        //    {
        //        TempData["ImportErrors"] = string.Join("\n", errorMessages.Take(20));
        //    }

        //    return RedirectToAction(nameof(Index));
        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string userId)
        {
            const string newPassword = "Sarajevo2026!";

            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["ImportMessage"] = "Reset password nije uspio: nedostaje userId.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var user = await _userManager.FindByIdAsync(userId);

                if (user == null)
                {
                    TempData["ImportMessage"] = "Korisnik nije pronađen.";
                    return RedirectToAction(nameof(Index));
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

                if (!result.Succeeded)
                {
                    TempData["ImportMessage"] = "Reset password nije uspio.";
                    TempData["ImportErrors"] = string.Join("\n", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
                    return RedirectToAction(nameof(Index));
                }

                TempData["ImportMessage"] = $"Password resetovan za korisnika: {user.UserName}";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ImportMessage"] = "Došlo je do greške prilikom resetovanja passworda.";
                TempData["ImportErrors"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateMissingInternalUsers()
        {
            var result =
                await _internalUsersSyncService.RunDeactivationAsync();

            TempData["ImportMessage"] = result.Message;

            if (result.ErrorMessages.Any())
            {
                TempData["ImportErrors"] =
                    string.Join(
                        Environment.NewLine,
                        result.ErrorMessages.Take(20));
            }

            return RedirectToAction(
                "Index",
                "Users",
                new
                {
                    area = "Admin",
                    status = "all",
                    type = "internal"
                });
        }



        private static string NormalizeName(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToUpperInvariant();
        }

        private static string? NormalizeValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static bool AreEqual(string? currentValue, string? newValue)
        {
            return string.Equals(
                currentValue?.Trim() ?? string.Empty,
                newValue?.Trim() ?? string.Empty,
                StringComparison.Ordinal);
        }
    }
}