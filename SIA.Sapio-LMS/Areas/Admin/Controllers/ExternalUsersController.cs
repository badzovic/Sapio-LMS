using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio.LMS.Data.Identity;
using SIA.Sapio_LMS.Areas.Admin.Models;
using SIA.Sapio_LMS.Services;
using System.Globalization;
using System.Text;

namespace SIA.Sapio_LMS.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ExternalUsersController : BaseAdminController
    {
        private readonly ExternalUsersApiService _externalUsersApiService;
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;

        public ExternalUsersController(
            ExternalUsersApiService externalUsersApiService,
            ApplicationDbContext dbContext,
            UserManager<ApplicationUser> userManager)
        {
            _externalUsersApiService = externalUsersApiService;
            _dbContext = dbContext;
            _userManager = userManager;
        }


        public async Task<IActionResult> Index(string status = "active")
        {
            var query = _dbContext.KORISNICI
                .Include(x => x.Kompanija)
                .Where(x => x.TIP_KORISNIKA_ID == 2)
                .AsQueryable();

            status = (status ?? "active").ToLower();

            switch (status)
            {
                case "inactive":
                    query = query.Where(x => x.AKTIVAN == "N");
                    break;

                case "all":
                    break;

                case "active":
                default:
                    query = query.Where(x => x.AKTIVAN == "Y");
                    status = "active";
                    break;
            }

            var users = await query
                .OrderBy(x => x.PREZIME)
                .ThenBy(x => x.IME)
                .Select(x => new ExternalUsersVM
                {
                    ID = x.ID,
                    ExternalId = x.EXTERNI_V3_ID ?? 0,
                    Ime = x.IME,
                    Prezime = x.PREZIME,
                    ImeOca = x.IME_OCA,
                    MjestoRodjenja = x.MJESTO_RODJENJA,
                    Kompanija = x.Kompanija != null ? x.Kompanija.NAZIV : x.KOMPANIJA,
                    KompanijaId = x.KOMPANIJA_ID,
                    UserId = x.USER_ID
                })
                .ToListAsync();

            ViewBag.CurrentStatus = status;

            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportAll()
        {
            var apiUsers = await _externalUsersApiService.GetUsersAsync();

            int imported = 0;
            int skipped = 0;
            int failed = 0;

            var errorMessages = new List<string>();

            const string defaultPassword = "Sarajevo2026!";
            const int externalUserTypeId = 2;
            const string defaultRoleId = "ccbc1522-8cc6-45b4-aaf1-eee37b438659";

            var kompanije = await _dbContext.KOMPANIJE.ToListAsync();

            foreach (var apiUser in apiUsers)
            {
                try
                {
                    var postojiUKorisnici = await _dbContext.KORISNICI
                        .AnyAsync(x => x.EXTERNI_V3_ID == apiUser.ExternalId);

                    if (postojiUKorisnici)
                    {
                        skipped++;
                        continue;
                    }

                    var normalizedCompany = string.IsNullOrWhiteSpace(apiUser.Kompanija)
                        ? null
                        : NormalizeCompanyName(apiUser.Kompanija);

                    var kompanija = string.IsNullOrWhiteSpace(normalizedCompany)
                        ? null
                        : kompanije.FirstOrDefault(x => x.NAZIV_NORMALIZED == normalizedCompany);

                    var userName = $"v3_{apiUser.ExternalId}";
                    var cleanEmail = (string?)null;

                    var identityUser = await _userManager.FindByNameAsync(userName);

                    if (identityUser == null)
                    {
                        identityUser = new ApplicationUser
                        {
                            FirstName = apiUser.Ime ?? string.Empty,
                            LastName = apiUser.Prezime ?? string.Empty,
                            IsExternalCollaborator = true,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            UserName = userName,
                            Email = cleanEmail,
                            EmailConfirmed = false
                        };

                        var createResult = await _userManager.CreateAsync(identityUser, defaultPassword);

                        if (!createResult.Succeeded)
                        {
                            failed++;
                            errorMessages.Add(
                                $"EXTERNI_V3_ID={apiUser.ExternalId} create user failed: " +
                                string.Join(" | ", createResult.Errors.Select(e => $"{e.Code}: {e.Description}")));
                            continue;
                        }
                    }

                    var roleExists = await _dbContext.Set<IdentityUserRole<string>>()
                        .AnyAsync(x => x.UserId == identityUser.Id && x.RoleId == defaultRoleId);

                    if (!roleExists)
                    {
                        _dbContext.Set<IdentityUserRole<string>>().Add(new IdentityUserRole<string>
                        {
                            UserId = identityUser.Id,
                            RoleId = defaultRoleId
                        });
                    }

                    var noviKorisnik = new Korisnik
                    {
                        IME = apiUser.Ime,
                        PREZIME = apiUser.Prezime,
                        ACCESS_KEY = null,
                        USER_ID = identityUser.Id,
                        EMAIL = cleanEmail,
                        KOMPANIJA = apiUser.Kompanija,
                        HRM_ID = null,
                        AKTIVAN = "Y",
                        IME_OCA = apiUser.ImeOca,
                        KOMPANIJA_ID = kompanija?.ID,
                        TIP_KORISNIKA_ID = externalUserTypeId,
                        EXTERNI_V3_ID = apiUser.ExternalId,
                        MJESTO_RODJENJA = apiUser.MjestoRodjenja
                    };

                    _dbContext.KORISNICI.Add(noviKorisnik);
                    await _dbContext.SaveChangesAsync();

                    imported++;
                }
                catch (Exception ex)
                {
                    failed++;
                    errorMessages.Add($"EXTERNI_V3_ID={apiUser.ExternalId}: {ex.Message}");
                }
            }

            TempData["ImportMessage"] = $"Import eksternih korisnika završen. Imported: {imported}, Skipped: {skipped}, Failed: {failed}";

            if (errorMessages.Any())
            {
                TempData["ImportErrors"] = string.Join("\n", errorMessages.Take(20));
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncAllExternalUsers()
        {
            try
            {
                var apiUsers = await _externalUsersApiService.GetUsersAsync();

                int synced = 0;
                int skipped = 0;
                int failed = 0;

                var errorMessages = new List<string>();

                var kompanije = await _dbContext.KOMPANIJE.ToListAsync();

                foreach (var apiUser in apiUsers)
                {
                    try
                    {
                        var lokalniUser = await _dbContext.KORISNICI
                            .FirstOrDefaultAsync(x => x.EXTERNI_V3_ID == apiUser.ExternalId && x.TIP_KORISNIKA_ID == 2);

                        if (lokalniUser == null)
                        {
                            skipped++;
                            continue;
                        }

                        var normalizedCompany = string.IsNullOrWhiteSpace(apiUser.Kompanija)
                            ? null
                            : NormalizeCompanyName(apiUser.Kompanija);

                        var kompanija = string.IsNullOrWhiteSpace(normalizedCompany)
                            ? null
                            : kompanije.FirstOrDefault(x => x.NAZIV_NORMALIZED == normalizedCompany);

                        // Update lokalne KORISNICI tabele
                        lokalniUser.IME = apiUser.Ime;
                        lokalniUser.PREZIME = apiUser.Prezime;
                        lokalniUser.IME_OCA = apiUser.ImeOca;
                        lokalniUser.MJESTO_RODJENJA = apiUser.MjestoRodjenja;

                        if (kompanija != null)
                        {
                            lokalniUser.KOMPANIJA_ID = kompanija.ID;
                            lokalniUser.KOMPANIJA = null;
                        }
                        else if (!string.IsNullOrWhiteSpace(apiUser.Kompanija))
                        {
                            // fallback: zadrži postojeći KOMPANIJA_ID, ali upiši tekstualni naziv iz V3
                            lokalniUser.KOMPANIJA = apiUser.Kompanija;
                        }

                        // Update Identity usera ako postoji veza
                        if (!string.IsNullOrWhiteSpace(lokalniUser.USER_ID))
                        {
                            var identityUser = await _userManager.FindByIdAsync(lokalniUser.USER_ID);

                            if (identityUser != null)
                            {
                                identityUser.FirstName = apiUser.Ime ?? string.Empty;
                                identityUser.LastName = apiUser.Prezime ?? string.Empty;

                                var updateResult = await _userManager.UpdateAsync(identityUser);

                                if (!updateResult.Succeeded)
                                {
                                    failed++;
                                    errorMessages.Add(
                                        $"EXTERNI_V3_ID={apiUser.ExternalId} identity update failed: " +
                                        string.Join(" | ", updateResult.Errors.Select(e => $"{e.Code}: {e.Description}")));

                                    continue;
                                }
                            }
                        }

                        await _dbContext.SaveChangesAsync();
                        synced++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        errorMessages.Add($"EXTERNI_V3_ID={apiUser.ExternalId}: {ex.Message}");
                    }
                }

                TempData["ImportMessage"] = $"Sync eksternih korisnika završen. Synced: {synced}, Skipped: {skipped}, Failed: {failed}";

                if (errorMessages.Any())
                {
                    TempData["ImportErrors"] = string.Join("\n", errorMessages.Take(20));
                }
            }
            catch (Exception ex)
            {
                TempData["ImportMessage"] = "Greška prilikom sync-a eksternih korisnika.";
                TempData["ImportErrors"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncCompanyIds()
        {
            int updated = 0;
            int missingApi = 0;
            int missingCompany = 0;
            int skipped = 0;
            int failed = 0;

            var errorMessages = new List<string>();

            try
            {
                var externalUsers = await _dbContext.KORISNICI
                    .Where(x => x.TIP_KORISNIKA_ID == 2 && x.EXTERNI_V3_ID != null)
                    .ToListAsync();

                var kompanije = await _dbContext.KOMPANIJE.ToListAsync();

                foreach (var korisnik in externalUsers)
                {
                    try
                    {
                        if (korisnik.EXTERNI_V3_ID == null)
                        {
                            skipped++;
                            continue;
                        }

                        var apiUser = await _externalUsersApiService.GetUserByExternalIdAsync(korisnik.EXTERNI_V3_ID.Value);

                        if (apiUser == null)
                        {
                            missingApi++;
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(apiUser.Kompanija))
                        {
                            korisnik.KOMPANIJA_ID = null;
                            skipped++;
                            continue;
                        }

                        var normalizedCompany = NormalizeCompanyName(apiUser.Kompanija);

                        var kompanija = kompanije.FirstOrDefault(x => x.NAZIV_NORMALIZED == normalizedCompany);

                        if (kompanija == null)
                        {
                            korisnik.KOMPANIJA_ID = null;
                            missingCompany++;
                            continue;
                        }

                        korisnik.KOMPANIJA_ID = kompanija.ID;
                        updated++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        errorMessages.Add($"EXTERNI_V3_ID={korisnik.EXTERNI_V3_ID}: {ex.Message}");
                    }
                }

                await _dbContext.SaveChangesAsync();

                TempData["ImportMessage"] =
                    $"Sync kompanija završen. Updated: {updated}, Missing API: {missingApi}, Missing Company: {missingCompany}, Skipped: {skipped}, Failed: {failed}";

                if (errorMessages.Any())
                {
                    TempData["ImportErrors"] = string.Join("\n", errorMessages.Take(20));
                }
            }
            catch (Exception ex)
            {
                TempData["ImportMessage"] = "Greška prilikom sync-a kompanija za eksterne korisnike.";
                TempData["ImportErrors"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        private static string NormalizeCompanyName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var value = input.Trim().ToLowerInvariant();

            // Ručna zamjena naših slova radi konzistentnog matchinga
            value = value
                .Replace("đ", "d")
                .Replace("č", "c")
                .Replace("ć", "c")
                .Replace("š", "s")
                .Replace("ž", "z");

            // Ukloni česte specijalne znakove i razdvojnike
            value = value
                .Replace("&", " ")
                .Replace("/", " ")
                .Replace("-", " ")
                .Replace(".", " ")
                .Replace(",", " ")
                .Replace("(", " ")
                .Replace(")", " ")
                .Replace("'", " ")
                .Replace("\"", " ");

            // Dodatno skini eventualne preostale dijakritike
            var normalized = value.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var ch in normalized)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(ch);
                }
            }

            var noDiacritics = sb.ToString().Normalize(NormalizationForm.FormC);

            // Zadrži samo slova i brojeve
            var chars = noDiacritics
                .Where(char.IsLetterOrDigit)
                .ToArray();

            return new string(chars);
        }
    }
}