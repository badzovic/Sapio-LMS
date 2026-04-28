using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio.LMS.Data.Identity;
using SIA.Sapio_LMS.Services;

namespace SIA.Sapio_LMS.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class InternalUsersController : BaseAdminController
    {
        private readonly InternalUsersApiService _internalUsersApiService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<InternalUsersController> _logger;

        public InternalUsersController(
            InternalUsersApiService internalUsersApiService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext dbContext,
            ILogger<InternalUsersController> logger)
        {
            _internalUsersApiService = internalUsersApiService;
            _userManager = userManager;
            _dbContext = dbContext;
            _logger = logger;
        }

        //podatke uzme apijem
        //public async Task<IActionResult> Index()
        //{
        //    var users = await _internalUsersApiService.GetUsersAsync();
        //    return View(users);
        //}

        public async Task<IActionResult> Index()
        {
            var users = await _dbContext.KORISNICI
                .Include(x => x.Kompanija)
                .Where(x => x.TIP_KORISNIKA_ID == 1)
                .Select(x => new SIA.Sapio_LMS.Areas.Admin.Models.InternalUserDto                
                {
                    HrmId = x.HRM_ID ?? 0,
                    Ime = x.IME,
                    Prezime = x.PREZIME,
                    ImeOca = x.IME_OCA,
                    Jmb = null,
                    Email = x.EMAIL,
                    Kompanija = x.Kompanija != null ? x.Kompanija.NAZIV : x.KOMPANIJA,
                    AccessKey = x.ACCESS_KEY,
                    UserId = x.USER_ID
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
            var apiUsers = (await _internalUsersApiService.GetUsersAsync())
                .ToList();

            int imported = 0;
            int skipped = 0;
            int failed = 0;

            var errorMessages = new List<string>();
            const string defaultPassword = "Sarajevo2026!";

            try
            {
                foreach (var apiUser in apiUsers)
                {
                    await using var transaction = await _dbContext.Database.BeginTransactionAsync();

                    try
                    {
                        _logger.LogInformation("=== START IMPORT HRM_ID={HrmId} ===", apiUser.HrmId);
                        _logger.LogInformation(
                            "API DATA => HRM_ID={HrmId}, Ime={Ime}, Prezime={Prezime}, Jmb={Jmb}, Email={Email}, AccessKey={AccessKey}, Kompanija={Kompanija}",
                            apiUser.HrmId, apiUser.Ime, apiUser.Prezime, apiUser.Jmb, apiUser.Email, apiUser.AccessKey, apiUser.Kompanija);

                        _logger.LogInformation("STEP 1: Provjera da li postoji u KORISNICI za HRM_ID={HrmId}", apiUser.HrmId);

                        var postojiUKorisnici = await _dbContext.KORISNICI
                            .AnyAsync(x => x.HRM_ID == apiUser.HrmId);

                        _logger.LogInformation("STEP 1 RESULT: postojiUKorisnici={Postoji} za HRM_ID={HrmId}", postojiUKorisnici, apiUser.HrmId);

                        if (postojiUKorisnici)
                        {
                            skipped++;
                            _logger.LogWarning("SKIP: HRM_ID={HrmId} već postoji u KORISNICI.", apiUser.HrmId);
                            await transaction.RollbackAsync();
                            continue;
                        }

                        var cleanEmail = string.IsNullOrWhiteSpace(apiUser.Email)
                            ? null
                            : apiUser.Email.Trim();

                        var userName = !string.IsNullOrWhiteSpace(cleanEmail)
                            ? cleanEmail
                            : $"hrm_{apiUser.HrmId}";

                        _logger.LogInformation(
                            "STEP 2: Pripremljen username/email za HRM_ID={HrmId}. UserName={UserName}, Email={Email}",
                            apiUser.HrmId, userName, cleanEmail ?? "NULL");

                        _logger.LogInformation("STEP 3: FindByNameAsync za UserName={UserName}", userName);

                        var identityUser = await _userManager.FindByNameAsync(userName);

                        _logger.LogInformation(
                            "STEP 3 RESULT: IdentityUser {Status} za HRM_ID={HrmId}",
                            identityUser == null ? "NE POSTOJI" : $"POSTOJI (Id={identityUser.Id})",
                            apiUser.HrmId);

                        if (identityUser == null)
                        {
                            identityUser = new ApplicationUser
                            {
                                FirstName = apiUser.Ime ?? string.Empty,
                                LastName = apiUser.Prezime ?? string.Empty,
                                IsExternalCollaborator = false,
                                IsActive = true,
                                CreatedAt = DateTime.UtcNow,

                                UserName = userName,
                                Email = cleanEmail,

                                EmailConfirmed = false,
                                PhoneNumberConfirmed = false,
                                TwoFactorEnabled = false,
                                LockoutEnabled = true,
                                AccessFailedCount = 0,

                                PhoneNumber = null,
                                LockoutEnd = null
                            };

                            _logger.LogInformation(
                                "STEP 4: CreateAsync za HRM_ID={HrmId}. FirstName={FirstName}, LastName={LastName}, UserName={UserName}, Email={Email}",
                                apiUser.HrmId,
                                identityUser.FirstName,
                                identityUser.LastName,
                                identityUser.UserName,
                                identityUser.Email ?? "NULL");

                            var createResult = await _userManager.CreateAsync(identityUser, defaultPassword);

                            _logger.LogInformation(
                                "STEP 4 RESULT: CreateAsync success={Success} za HRM_ID={HrmId}",
                                createResult.Succeeded, apiUser.HrmId);

                            if (!createResult.Succeeded)
                            {
                                var createErrors = string.Join(" | ", createResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
                                failed++;

                                var msg = $"HRM_ID={apiUser.HrmId} CREATE USER FAILED: {createErrors}";
                                errorMessages.Add(msg);
                                _logger.LogError(msg);

                                await transaction.RollbackAsync();
                                continue;
                            }

                            _logger.LogInformation(
                                "STEP 4 SUCCESS: Kreiran Identity user za HRM_ID={HrmId}, UserId={UserId}",
                                apiUser.HrmId, identityUser.Id);
                        }

                        var noviKorisnik = new Korisnik
                        {
                            IME = apiUser.Ime,
                            PREZIME = apiUser.Prezime,
                            ACCESS_KEY = apiUser.AccessKey,
                            USER_ID = identityUser.Id,
                            EMAIL = cleanEmail,
                            KOMPANIJA = apiUser.Kompanija ?? "MAS",
                            HRM_ID = apiUser.HrmId
                        };

                        _logger.LogInformation(
                            "STEP 5: Pripremljen Korisnik entity za HRM_ID={HrmId}. IME={Ime}, PREZIME={Prezime}, ACCESS_KEY={AccessKey}, USER_ID={UserId}, EMAIL={Email}, KOMPANIJA={Kompanija}",
                            noviKorisnik.HRM_ID,
                            noviKorisnik.IME,
                            noviKorisnik.PREZIME,
                            noviKorisnik.ACCESS_KEY ?? 0,
                            noviKorisnik.USER_ID ?? "NULL",
                            noviKorisnik.EMAIL ?? "NULL",
                            noviKorisnik.KOMPANIJA ?? "NULL");

                        _dbContext.KORISNICI.Add(noviKorisnik);

                        _logger.LogInformation("STEP 6: Poziv SaveChangesAsync za HRM_ID={HrmId}", apiUser.HrmId);

                        await _dbContext.SaveChangesAsync();

                        _logger.LogInformation("STEP 6 SUCCESS: SaveChangesAsync prošao za HRM_ID={HrmId}", apiUser.HrmId);

                        await transaction.CommitAsync();

                        _logger.LogInformation("STEP 7 SUCCESS: CommitAsync prošao za HRM_ID={HrmId}", apiUser.HrmId);

                        imported++;
                        _logger.LogInformation("=== END IMPORT SUCCESS HRM_ID={HrmId} ===", apiUser.HrmId);
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        failed++;

                        var inner = ex.InnerException?.Message ?? "NO INNER EXCEPTION";
                        var fullMessage = $"HRM_ID={apiUser.HrmId} EXCEPTION: {ex.Message} | INNER: {inner}";

                        errorMessages.Add(fullMessage);

                        _logger.LogError(ex, "=== END IMPORT FAILED HRM_ID={HrmId} ===", apiUser.HrmId);
                        _logger.LogError("DETAILED ERROR FOR HRM_ID={HrmId}: {Message}", apiUser.HrmId, fullMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Opšta greška prije ili tokom importa.");
                TempData["ImportMessage"] = "Import prekinut zbog opšte greške.";
                TempData["ImportErrors"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }

            TempData["ImportMessage"] = $"Import završen. Imported: {imported}, Skipped: {skipped}, Failed: {failed}";

            if (errorMessages.Any())
            {
                TempData["ImportErrors"] = string.Join("\n", errorMessages.Take(20));
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncInternalUsers()
        {
            var apiUsers = await _internalUsersApiService.GetUsersAsync();

            int updated = 0;
            int missing = 0;
            int failed = 0;

            var errorMessages = new List<string>();

            foreach (var apiUser in apiUsers)
            {
                try
                {
                    var postojeci = await _dbContext.KORISNICI
                        .FirstOrDefaultAsync(x => x.HRM_ID == apiUser.HrmId);

                    if (postojeci == null)
                    {
                        missing++;
                        continue;
                    }

                    bool hasChanges = false;

                    var novoIme = string.IsNullOrWhiteSpace(apiUser.Ime) ? null : apiUser.Ime.Trim();
                    var novoPrezime = string.IsNullOrWhiteSpace(apiUser.Prezime) ? null : apiUser.Prezime.Trim();
                    var novoImeOca = string.IsNullOrWhiteSpace(apiUser.ImeOca) ? null : apiUser.ImeOca.Trim();
                    var noviEmail = string.IsNullOrWhiteSpace(apiUser.Email) ? null : apiUser.Email.Trim();

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

                    // ACCESS_KEY je int
                    if (postojeci.ACCESS_KEY != apiUser.AccessKey)
                    {
                        postojeci.ACCESS_KEY = apiUser.AccessKey;
                        hasChanges = true;
                    }

                    if (!string.IsNullOrWhiteSpace(postojeci.USER_ID))
                    {
                        var identityUser = await _userManager.FindByIdAsync(postojeci.USER_ID);

                        if (identityUser != null)
                        {
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
                                identityChanged = true;
                            }

                            if (identityChanged)
                            {
                                var updateResult = await _userManager.UpdateAsync(identityUser);

                                if (!updateResult.Succeeded)
                                {
                                    throw new Exception("Identity update failed: " +
                                        string.Join(" | ", updateResult.Errors.Select(e => e.Description)));
                                }

                                hasChanges = true;
                            }
                        }
                    }

                    if (hasChanges)
                    {
                        updated++;
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    errorMessages.Add($"HRM_ID={apiUser.HrmId}: {ex.Message}");
                }
            }

            await _dbContext.SaveChangesAsync();

            TempData["ImportMessage"] = $"Sync završen. Updated: {updated}, Missing: {missing}, Failed: {failed}";

            if (errorMessages.Any())
            {
                TempData["ImportErrors"] = string.Join("\n", errorMessages.Take(20));
            }

            return RedirectToAction(nameof(Index));
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


    }
}