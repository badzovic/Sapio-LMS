using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio.LMS.Data.Identity;
using SIA.Sapio_LMS.Models;

namespace SIA.Sapio_LMS.Controllers
{
    public class RegistrationController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;

        private const string DefaultPassword = "Sarajevo2026!";

        public RegistrationController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            _dbContext = dbContext;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Register(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["ImportMessage"] = "Registracijski link nije ispravan.";
                TempData["ImportErrors"] = "Token nije proslijeđen.";
                return RedirectToAction(nameof(LinkInvalid));
            }

            var registrationToken = await GetValidTokenAsync(token);
            if (registrationToken == null)
            {
                TempData["AuthMessage"] = "Registracijski link je istekao ili više nije aktivan.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var model = new RegisterUserVM
            {
                Token = token
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterUserVM model)
        {
            if (string.IsNullOrWhiteSpace(model.Token))
            {
                ModelState.AddModelError("", "Registracijski token nije proslijeđen.");
                return View(model);
            }

            var registrationToken = await GetValidTokenAsync(model.Token);
            if (registrationToken == null)
            {
                TempData["AuthMessage"] = "Registracijski link je istekao ili više nije aktivan.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var firstName = model.FirstName.Trim();
            var lastName = model.LastName.Trim();
            var imeOca = model.ImeOca.Trim();
            var email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
            var mjestoRodjenja = string.IsNullOrWhiteSpace(model.MjestoRodjenja) ? null : model.MjestoRodjenja.Trim();

            if (!string.IsNullOrWhiteSpace(email))
            {
                var existingIdentityUser = await _userManager.FindByEmailAsync(email);
                if (existingIdentityUser != null)
                {
                    ModelState.AddModelError(nameof(model.Email), "Korisnik sa ovim emailom već postoji.");
                    return View(model);
                }

                var existingLocalUserWithEmail = await _dbContext.KORISNICI
                    .AnyAsync(x => x.EMAIL != null && x.EMAIL == email);

                if (existingLocalUserWithEmail)
                {
                    ModelState.AddModelError(nameof(model.Email), "Email već postoji u lokalnoj tabeli korisnika.");
                    return View(model);
                }
            }

            var duplicatePerson = await _dbContext.KORISNICI.AnyAsync(x =>
                x.IME == firstName &&
                x.PREZIME == lastName &&
                x.IME_OCA == imeOca);

            if (duplicatePerson)
            {
                ModelState.AddModelError("", "Korisnik sa istim imenom, prezimenom i imenom oca već postoji.");
                return View(model);
            }

            var generatedUserName = !string.IsNullOrWhiteSpace(email)
                ? email
                : $"reg_{Guid.NewGuid():N}";

            var existingUserName = await _userManager.FindByNameAsync(generatedUserName);
            if (existingUserName != null)
            {
                ModelState.AddModelError("", "Generisani username već postoji. Pokušajte ponovo.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = generatedUserName,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                IsExternalCollaborator = true,
                IsActive = false,
                EmailConfirmed = !string.IsNullOrWhiteSpace(email),
                CreatedAt = DateTime.UtcNow,
                LockoutEnabled = true,
                LockoutEnd = DateTimeOffset.MaxValue,
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

            try
            {
                var noviKorisnik = new Korisnik
                {
                    IME = firstName,
                    PREZIME = lastName,
                    IME_OCA = imeOca,
                    MJESTO_RODJENJA = mjestoRodjenja,
                    EMAIL = email,
                    USER_ID = user.Id,
                    ACCESS_KEY = null,
                    HRM_ID = null,
                    AKTIVAN = "N",
                    KOMPANIJA = null,
                    KOMPANIJA_ID = null,                   
                    TIP_KORISNIKA_ID = null,

                    EXTERNI_V3_ID = null
                };

                _dbContext.KORISNICI.Add(noviKorisnik);

                registrationToken.USED_COUNT += 1;

                await _dbContext.SaveChangesAsync();
            }
            catch
            {
                await _userManager.DeleteAsync(user);
                ModelState.AddModelError("", "Nalog je kreiran u Identity sistemu, ali upis u lokalnu tabelu nije uspio.");
                return View(model);
            }

            TempData["AuthMessage"] = "Vaš nalog je kreiran i čeka odobrenje administratora.";
            return Redirect("/Identity/Account/Login");
        }

        [HttpGet]
        public IActionResult RegisterSuccess()
        {
            return View();
        }

        [HttpGet]
        public IActionResult LinkInvalid()
        {
            return View();
        }

        private async Task<RegistrationToken?> GetValidTokenAsync(string token)
        {
            var now = DateTime.Now;

            return await _dbContext.REGISTRATION_TOKENS
                .FirstOrDefaultAsync(x =>
                    x.TOKEN == token &&
                    x.IS_ACTIVE == "Y" &&
                    x.EXPIRES_AT >= now);
        }
    }
}