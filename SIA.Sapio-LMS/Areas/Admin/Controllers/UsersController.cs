using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio.LMS.Data.Identity;
using SIA.Sapio_LMS.Areas.Admin.Models;

namespace SIA.Sapio_LMS.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UsersController : BaseAdminController
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;

        private const string DefaultPassword = "Sarajevo2026!";

        public UsersController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            _dbContext = dbContext;
            _userManager = userManager;
        }

        public IActionResult Index(string status = "all")
        {
            var usersQuery = _userManager.Users
                .Where(x => !x.IsDeleted)
                .AsQueryable();

            if (status == "pending")
            {
                usersQuery = usersQuery.Where(x => !x.IsActive);
            }
            else if (status == "active")
            {
                usersQuery = usersQuery.Where(x => x.IsActive);
            }

            var users = usersQuery.ToList();

            ViewBag.Status = status;

            return View(users);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PopulateDropdownsAsync();
            ViewBag.DefaultPassword = DefaultPassword;
            return View(new CreateUserVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserVM model)
        {
            await PopulateDropdownsAsync();
            ViewBag.DefaultPassword = DefaultPassword;

            if (!ModelState.IsValid)
                return View(model);

            if (model.TipKorisnikaId == null)
            {
                ModelState.AddModelError(nameof(model.TipKorisnikaId), "Tip korisnika je obavezan.");
                return View(model);
            }

            var selectedTip = await _dbContext.TIPOVI_KORISNIKA
                .FirstOrDefaultAsync(x => x.ID == model.TipKorisnikaId.Value);

            if (selectedTip == null)
            {
                ModelState.AddModelError(nameof(model.TipKorisnikaId), "Odabrani tip korisnika ne postoji.");
                return View(model);
            }

            var allowedTipNames = new[] { "Eksterni", "Sezonski radnik" };
            if (!allowedTipNames.Contains(selectedTip.Naziv))
            {
                ModelState.AddModelError(nameof(model.TipKorisnikaId), "Dozvoljeno je kreirati samo eksterne i sezonske korisnike.");
                return View(model);
            }

            var roleExists = await _dbContext.Roles
                .AnyAsync(x => x.Name != null && x.Name == model.Role);

            if (!roleExists)
            {
                ModelState.AddModelError(nameof(model.Role), "Odabrana rola ne postoji.");
                return View(model);
            }

            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                var normalizedEmail = model.Email.Trim();

                var existingIdentityUser = await _userManager.FindByEmailAsync(normalizedEmail);
                if (existingIdentityUser != null)
                {
                    ModelState.AddModelError(nameof(model.Email), "Korisnik sa ovim emailom već postoji.");
                    return View(model);
                }

                var existingLocalUserWithEmail = await _dbContext.KORISNICI
                    .AnyAsync(x => x.EMAIL != null && x.EMAIL == normalizedEmail);

                if (existingLocalUserWithEmail)
                {
                    ModelState.AddModelError(nameof(model.Email), "Email već postoji u lokalnoj tabeli korisnika.");
                    return View(model);
                }
            }

            var isExternalCollaborator =
                selectedTip.Naziv == "Eksterni" ||
                selectedTip.Naziv == "Sezonski radnik";

            var generatedUserName = !string.IsNullOrWhiteSpace(model.Email)
                ? model.Email.Trim()
                : $"manual_{Guid.NewGuid():N}";

            var existingUserName = await _userManager.FindByNameAsync(generatedUserName);
            if (existingUserName != null)
            {
                ModelState.AddModelError("", "Generisani username već postoji. Pokušajte ponovo.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = generatedUserName,
                Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                IsExternalCollaborator = isExternalCollaborator,
                IsActive = true,
                EmailConfirmed = !string.IsNullOrWhiteSpace(model.Email),
                CreatedAt = DateTime.UtcNow,
                MustChangePassword = true
            };

            var createResult = await _userManager.CreateAsync(user, DefaultPassword);

            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                return View(model);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, model.Role);

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);

                foreach (var error in roleResult.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                return View(model);
            }

            try
            {
                var noviKorisnik = new Korisnik
                {
                    IME = model.FirstName.Trim(),
                    PREZIME = model.LastName.Trim(),
                    IME_OCA = model.ImeOca.Trim(),
                    MJESTO_RODJENJA = null,
                    EMAIL = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
                    USER_ID = user.Id,
                    ACCESS_KEY = null,
                    HRM_ID = null,
                    AKTIVAN = "Y",
                    KOMPANIJA = null,
                    KOMPANIJA_ID = null,
                    TIP_KORISNIKA_ID = selectedTip.ID,
                    EXTERNI_V3_ID = null                    
                };

                _dbContext.KORISNICI.Add(noviKorisnik);
                await _dbContext.SaveChangesAsync();
            }
            catch
            {
                await _userManager.DeleteAsync(user);
                ModelState.AddModelError("", "Korisnik je kreiran u Identity sistemu, ali upis u lokalnu tabelu nije uspio.");
                return View(model);
            }

            TempData["ImportMessage"] = $"Korisnik je uspješno kreiran. Dodijeljen je defaultni password: {DefaultPassword}";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdownsAsync()
        {
            ViewBag.Roles = await _dbContext.Roles
                .Where(x => x.Name != null)
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem
                {
                    Value = x.Name!,
                    Text = x.Name!
                })
                .ToListAsync();

            ViewBag.TipoviKorisnika = await _dbContext.TIPOVI_KORISNIKA
                .Where(x => x.Naziv == "Eksterni" || x.Naziv == "Sezonski radnik" || x.Naziv == "Interni")
                .OrderBy(x => x.Naziv)
                .Select(x => new SelectListItem
                {
                    Value = x.ID.ToString(),
                    Text = x.Naziv
                })
                .ToListAsync();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveAsExternal(string userId)
        {
            return await ApproveUser(userId, "Eksterni");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveAsSeasonal(string userId)
        {
            return await ApproveUser(userId, "Sezonski radnik");
        }

        private async Task<IActionResult> ApproveUser(string userId, string tipNaziv)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["ImportMessage"] = "User ID nije validan.";
                return RedirectToAction(nameof(Index));
            }

            var identityUser = await _userManager.FindByIdAsync(userId);
            if (identityUser == null)
            {
                TempData["ImportMessage"] = "Korisnik nije pronađen.";
                return RedirectToAction(nameof(Index));
            }

            var lokalniUser = await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x => x.USER_ID == userId);

            if (lokalniUser == null)
            {
                TempData["ImportMessage"] = "Lokalni korisnik nije pronađen.";
                return RedirectToAction(nameof(Index));
            }

            var tip = await _dbContext.TIPOVI_KORISNIKA
                .FirstOrDefaultAsync(x => x.Naziv == tipNaziv);

            if (tip == null)
            {
                TempData["ImportMessage"] = $"Tip korisnika '{tipNaziv}' ne postoji.";
                return RedirectToAction(nameof(Index));
            }

            // update lokalna tabela
            lokalniUser.TIP_KORISNIKA_ID = tip.ID;
            lokalniUser.AKTIVAN = "Y";

            // update identity
            identityUser.IsActive = true;
            identityUser.LockoutEnd = null;

            // opcionalno: eksterni flag
            identityUser.IsExternalCollaborator = true;

            var result = await _userManager.UpdateAsync(identityUser);

            if (!result.Succeeded)
            {
                TempData["ImportMessage"] = "Greška prilikom aktivacije korisnika.";
                TempData["ImportErrors"] = string.Join("\n", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            await _dbContext.SaveChangesAsync();

            TempData["ImportMessage"] = $"Korisnik je aktiviran kao '{tipNaziv}'.";
            return RedirectToAction(nameof(Index));
        }
    }
}