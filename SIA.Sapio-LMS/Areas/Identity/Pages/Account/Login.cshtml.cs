#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Identity;

namespace SIA.Sapio_LMS.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _dbContext;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LoginModel> logger,
            ApplicationDbContext dbContext)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _dbContext = dbContext;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "Email ili ime i prezime")]
            public string LoginIdentifier { get; set; }

            [Display(Name = "Ime oca")]
            public string FatherName { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }

            public bool RequireFatherName { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            ReturnUrl = returnUrl;

            Input ??= new InputModel();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var identifier = Input.LoginIdentifier?.Trim();
            if (string.IsNullOrWhiteSpace(identifier))
            {
                ModelState.AddModelError(string.Empty, "Unesite email ili ime i prezime.");
                return Page();
            }

            ApplicationUser user = null;

            // 1. EMAIL LOGIN
            if (identifier.Contains("@"))
            {
                user = await _userManager.FindByEmailAsync(identifier);
            }
            else
            {
                // 2. IME + PREZIME LOGIN
                var parts = identifier
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .ToList();

                if (parts.Count < 2)
                {
                    ModelState.AddModelError(string.Empty, "Unesite ime i prezime ili email adresu.");
                    return Page();
                }

                var ime = parts.First();
                var prezime = parts.Last();

                var kandidati = await _dbContext.KORISNICI
                    .Where(x =>
                        x.AKTIVAN == "Y" &&
                        x.USER_ID != null &&
                        x.IME != null &&
                        x.PREZIME != null &&
                        x.IME.ToLower() == ime.ToLower() &&
                        x.PREZIME.ToLower() == prezime.ToLower())
                    .ToListAsync();

                if (!string.IsNullOrWhiteSpace(Input.FatherName))
                {
                    var fatherName = Input.FatherName.Trim();

                    kandidati = kandidati
                        .Where(x => !string.IsNullOrWhiteSpace(x.IME_OCA) &&
                                    x.IME_OCA.ToLower() == fatherName.ToLower())
                        .ToList();
                }

                if (kandidati.Count == 0)
                {
                    ModelState.AddModelError(string.Empty, "Korisnik nije pronađen.");
                    return Page();
                }

                if (kandidati.Count > 1 && string.IsNullOrWhiteSpace(Input.FatherName))
                {
                    Input.RequireFatherName = true;
                    ModelState.AddModelError(string.Empty, "Postoji više korisnika sa istim imenom i prezimenom. Unesite i ime oca.");
                    return Page();
                }

                if (kandidati.Count > 1)
                {
                    Input.RequireFatherName = true;
                    ModelState.AddModelError(string.Empty, "Nije moguće jednoznačno odrediti korisnika. Provjerite ime oca.");
                    return Page();
                }

                var userId = kandidati.First().USER_ID;
                user = await _userManager.FindByIdAsync(userId);
            }

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, Input.Password, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, Input.RememberMe);
                _logger.LogInformation("User logged in.");

                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                }

                if (await _userManager.IsInRoleAsync(user, "Instruktor"))
                {
                    return RedirectToAction("Index", "Dashboard", new { area = "Instructor" });
                }

                if (await _userManager.IsInRoleAsync(user, "Student"))
                {
                    return RedirectToAction("Index", "Dashboard", new { area = "Student" });
                }

                return LocalRedirect(returnUrl);
            }

            if (result.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out.");
                return RedirectToPage("./Lockout");
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return Page();
        }
    }
}