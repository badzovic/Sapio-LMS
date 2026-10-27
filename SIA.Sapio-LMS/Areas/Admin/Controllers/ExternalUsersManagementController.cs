using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Identity;
using SIA.Sapio_LMS.Areas.Admin.Models;
using SIA.Sapio_LMS.Services;

namespace SIA.Sapio_LMS.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ExternalUsersManagementController : BaseAdminController
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ExternalUsersApiService _externalUsersApiService;

        public ExternalUsersManagementController(
            ApplicationDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ExternalUsersApiService externalUsersApiService)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _roleManager = roleManager;
            _externalUsersApiService = externalUsersApiService;
        }

        public async Task<IActionResult> Details(int id, string? returnUrl = null)
        {
            var lokalniUser = await _dbContext.KORISNICI
                .Include(x => x.Kompanija)
                .FirstOrDefaultAsync(x => x.ID == id && x.TIP_KORISNIKA_ID == 2);

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

            var kompanije = await _dbContext.KOMPANIJE
                .Where(x => x.AKTIVNA == "Y")
                .OrderBy(x => x.NAZIV)
                .ToListAsync();

            var allRoles = await _dbContext.Roles
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => x.Name!.Trim())
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            var tipKorisnikaText = await _dbContext.TIPOVI_KORISNIKA
                .Where(x => x.ID == lokalniUser.TIP_KORISNIKA_ID)
                .Select(x => x.Naziv)
                .FirstOrDefaultAsync();

            ViewBag.UserRoles = userRoles;
            ViewBag.AllRoles = allRoles;
            ViewBag.IsActive =
                lokalniUser.AKTIVAN == "Y" &&
                identityUser?.IsActive == true &&
                identityUser.IsDeleted == false;
            ViewBag.TipKorisnikaText = string.IsNullOrWhiteSpace(tipKorisnikaText) ? "-" : tipKorisnikaText;
            ViewBag.Companies = kompanije
                .Select(x => new SelectListItem
                {
                    Value = x.ID.ToString(),
                    Text = x.NAZIV
                })
                .ToList();

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
                Kompanija = lokalniUser.Kompanija != null
                    ? lokalniUser.Kompanija.NAZIV
                    : lokalniUser.KOMPANIJA,
                KompanijaId = lokalniUser.KOMPANIJA_ID,
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
        public async Task<IActionResult> SyncSingleUser(int id)
        {
            try
            {
                /*
                 * ============================================================
                 * 1. PRONALAZAK LOKALNOG EKSTERNOG KORISNIKA
                 * ============================================================
                 */

                var lokalniUser = await _dbContext.KORISNICI
                    .FirstOrDefaultAsync(x =>
                        x.ID == id &&
                        x.TIP_KORISNIKA_ID == 2);

                if (lokalniUser == null)
                {
                    TempData["ImportMessage"] =
                        "Korisnik nije pronađen u lokalnoj bazi.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                if (lokalniUser.EXTERNI_V3_ID == null)
                {
                    TempData["ImportMessage"] =
                        "Korisnik nema EXTERNI_V3_ID.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                /*
                 * ============================================================
                 * 2. PREUZIMANJE PODATAKA IZ V3 API-JA
                 * ============================================================
                 */

                var apiUser =
                    await _externalUsersApiService.GetUserByExternalIdAsync(
                        lokalniUser.EXTERNI_V3_ID.Value);

                if (apiUser == null)
                {
                    TempData["ImportMessage"] =
                        "Korisnik nije pronađen u V3 API-ju.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                /*
                 * ============================================================
                 * 3. PRONALAZAK KOMPANIJE
                 * ============================================================
                 */

                var normalizedCompany =
                    string.IsNullOrWhiteSpace(apiUser.Kompanija)
                        ? null
                        : NormalizeCompanyName(apiUser.Kompanija);

                var kompanija =
                    string.IsNullOrWhiteSpace(normalizedCompany)
                        ? null
                        : await _dbContext.KOMPANIJE
                            .FirstOrDefaultAsync(x =>
                                x.NAZIV_NORMALIZED == normalizedCompany);

                /*
                 * ============================================================
                 * 4. PROVJERA IDENTITY RAČUNA
                 * ============================================================
                 *
                 * Provjeru radimo prije promjena u bazi.
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
                 * ============================================================
                 * 5. TRANSAKCIJA
                 * ============================================================
                 *
                 * Sve promjene u KORISNICI i AspNetUsers
                 * moraju biti uspješno sačuvane.
                 */

                await using var transaction =
                    await _dbContext.Database.BeginTransactionAsync();

                try
                {
                    /*
                     * ========================================================
                     * 6. SINHRONIZACIJA KORISNICI
                     * ========================================================
                     */

                    lokalniUser.IME = apiUser.Ime;
                    lokalniUser.PREZIME = apiUser.Prezime;
                    lokalniUser.IME_OCA = apiUser.ImeOca;
                    lokalniUser.MJESTO_RODJENJA = apiUser.MjestoRodjenja;

                    lokalniUser.KOMPANIJA_ID = kompanija?.ID;
                    lokalniUser.KOMPANIJA = null;

                    /*
                     * ========================================================
                     * 7. SINHRONIZACIJA ASP.NET IDENTITY
                     * ========================================================
                     *
                     * Sinhronizujemo samo ime i prezime.
                     * Ne diramo status niti pristup korisnika.
                     */

                    identityUser.FirstName = apiUser.Ime ?? string.Empty;
                    identityUser.LastName = apiUser.Prezime ?? string.Empty;

                    var updateResult =
                        await _userManager.UpdateAsync(identityUser);

                    if (!updateResult.Succeeded)
                    {
                        throw new InvalidOperationException(
                            "Identity update nije uspio: " +
                            string.Join(
                                " | ",
                                updateResult.Errors.Select(
                                    x => $"{x.Code}: {x.Description}")));
                    }

                    /*
                     * ========================================================
                     * 8. ČUVANJE PROMJENA
                     * ========================================================
                     */

                    await _dbContext.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["ImportMessage"] =
                        "Korisnik je uspješno sinhronizovan iz V3 sistema.";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    TempData["ImportMessage"] =
                        "Greška prilikom sinhronizacije korisnika.";

                    TempData["ImportErrors"] = ex.Message;
                }
                finally
                {
                    _dbContext.ChangeTracker.Clear();
                }
            }
            catch (Exception ex)
            {
                TempData["ImportMessage"] =
                    "Greška prilikom povezivanja s V3 sistemom.";

                TempData["ImportErrors"] = ex.Message;

                _dbContext.ChangeTracker.Clear();
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id)
        {
            const string newPassword = "Sarajevo2026!";

            var lokalniUser = await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x => x.ID == id && x.TIP_KORISNIKA_ID == 2);

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleRole(int id, string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                TempData["ImportMessage"] = "Rola nije proslijeđena.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var lokalniUser = await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x => x.ID == id && x.TIP_KORISNIKA_ID == 2);

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
                var lokalniUser = await _dbContext.KORISNICI
                    .FirstOrDefaultAsync(x =>
                        x.ID == id &&
                        x.TIP_KORISNIKA_ID == 2);

                if (lokalniUser == null)
                {
                    TempData["ImportMessage"] = "Eksterni korisnik nije pronađen.";
                    return RedirectToAction(nameof(Details), new { id });
                }

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
                 * ODREĐIVANJE NOVOG STATUSA
                 * ========================================================
                 *
                 * Ako su oba osnovna statusa aktivna i račun
                 * nije soft-deleteovan, deaktiviramo ga.
                 *
                 * U svim ostalim slučajevima administrator
                 * može izvršiti reaktivaciju.
                 */

                bool trenutnoAktivan =
                    lokalniUser.AKTIVAN == "Y" &&
                    identityUser.IsActive &&
                    !identityUser.IsDeleted;

                bool noviStatus = !trenutnoAktivan;

                await using var transaction =
                    await _dbContext.Database.BeginTransactionAsync();

                try
                {
                    /*
                     * ====================================================
                     * KORISNICI
                     * ====================================================
                     */

                    lokalniUser.AKTIVAN = noviStatus ? "Y" : "N";

                    /*
                     * ====================================================
                     * ASP.NET IDENTITY
                     * ====================================================
                     */

                    identityUser.IsActive = noviStatus;
                    identityUser.IsDeleted = !noviStatus;
                    identityUser.LockoutEnabled = true;

                    if (noviStatus)
                    {
                        // Uklanjamo trajnu blokadu od deaktivacije.
                        if (identityUser.LockoutEnd ==
                            DateTimeOffset.MaxValue)
                        {
                            identityUser.LockoutEnd = null;
                        }
                    }
                    else
                    {
                        identityUser.LockoutEnd =
                            DateTimeOffset.MaxValue;
                    }

                    /*
                     * ====================================================
                     * UPDATE IDENTITY
                     * ====================================================
                     */

                    var updateResult =
                        await _userManager.UpdateAsync(identityUser);

                    if (!updateResult.Succeeded)
                    {
                        throw new InvalidOperationException(
                            "Identity update nije uspio: " +
                            string.Join(" | ",
                                updateResult.Errors.Select(
                                    x => x.Description)));
                    }

                    /*
                     * ====================================================
                     * ČUVANJE PROMJENA
                     * ====================================================
                     */

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["ImportMessage"] = noviStatus
                        ? "Eksterni korisnik je uspješno aktiviran."
                        : "Eksterni korisnik je uspješno deaktiviran.";

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
                    "Greška prilikom promjene statusa eksternog korisnika.";

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
        public async Task<IActionResult> UpdateCompany(int id, int? kompanijaId)
        {
            var lokalniUser = await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x => x.ID == id && x.TIP_KORISNIKA_ID == 2);

            if (lokalniUser == null)
            {
                TempData["ImportMessage"] = "Korisnik nije pronađen.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (kompanijaId == null)
            {
                lokalniUser.KOMPANIJA_ID = null;
                await _dbContext.SaveChangesAsync();

                TempData["ImportMessage"] = "Kompanija je uklonjena sa korisnika.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var kompanijaExists = await _dbContext.KOMPANIJE
                .AnyAsync(x => x.ID == kompanijaId.Value && x.AKTIVNA == "Y");

            if (!kompanijaExists)
            {
                TempData["ImportMessage"] = "Odabrana kompanija ne postoji ili nije aktivna.";
                return RedirectToAction(nameof(Details), new { id });
            }

            lokalniUser.KOMPANIJA_ID = kompanijaId.Value;
            await _dbContext.SaveChangesAsync();

            TempData["ImportMessage"] = "Kompanija je uspješno ažurirana.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEmail(int id, string? email)
        {
            try
            {
                var lokalniUser = await _dbContext.KORISNICI
                    .FirstOrDefaultAsync(x => x.ID == id && x.TIP_KORISNIKA_ID == 2);

                if (lokalniUser == null)
                {
                    TempData["ImportMessage"] = "Korisnik nije pronađen.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var cleanEmail = string.IsNullOrWhiteSpace(email)
                    ? null
                    : email.Trim();

                if (!string.IsNullOrWhiteSpace(cleanEmail))
                {
                    var emailExistsOnAnotherIdentityUser = await _dbContext.Users
                        .AnyAsync(x => x.Email == cleanEmail && x.Id != lokalniUser.USER_ID);

                    if (emailExistsOnAnotherIdentityUser)
                    {
                        TempData["ImportMessage"] = "Ova email adresa je već dodijeljena drugom korisniku.";
                        return RedirectToAction(nameof(Details), new { id });
                    }
                }

                lokalniUser.EMAIL = cleanEmail;

                if (!string.IsNullOrWhiteSpace(lokalniUser.USER_ID))
                {
                    var identityUser = await _userManager.FindByIdAsync(lokalniUser.USER_ID);

                    if (identityUser != null)
                    {
                        identityUser.Email = cleanEmail;
                        identityUser.NormalizedEmail = cleanEmail?.ToUpperInvariant();

                        var result = await _userManager.UpdateAsync(identityUser);

                        if (!result.Succeeded)
                        {
                            TempData["ImportMessage"] = "Email je ažuriran u lokalnoj tabeli, ali update Identity korisnika nije uspio.";
                            TempData["ImportErrors"] = string.Join("\n", result.Errors.Select(e => $"{e.Code}: {e.Description}"));

                            await _dbContext.SaveChangesAsync();
                            return RedirectToAction(nameof(Details), new { id });
                        }
                    }
                }

                await _dbContext.SaveChangesAsync();

                TempData["ImportMessage"] = "Email je uspješno ažuriran.";
            }
            catch (Exception ex)
            {
                TempData["ImportMessage"] = "Greška prilikom ažuriranja email adrese.";
                TempData["ImportErrors"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        private static string NormalizeCompanyName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var value = input.Trim().ToLowerInvariant();

            value = value
                .Replace("đ", "d")
                .Replace("č", "c")
                .Replace("ć", "c")
                .Replace("š", "s")
                .Replace("ž", "z")
                .Replace("&", " ")
                .Replace("/", " ")
                .Replace("-", " ")
                .Replace(".", " ")
                .Replace(",", " ")
                .Replace("(", " ")
                .Replace(")", " ")
                .Replace("'", " ")
                .Replace("\"", " ");

            var chars = value
                .Where(char.IsLetterOrDigit)
                .ToArray();

            return new string(chars);
        }
    }
}