using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SIA.Sapio.LMS.Data.Identity;
using System.ComponentModel.DataAnnotations;

namespace SIA.Sapio_LMS.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private const string DefaultPassword = "Sarajevo2026!";

        public ProfileController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public class ForceChangePasswordVM
        {
            [Required(ErrorMessage = "Novi password je obavezan.")]
            public string NewPassword { get; set; } = string.Empty;

            [Required(ErrorMessage = "Potvrda passworda je obavezna.")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceChangePassword([FromForm] ForceChangePasswordVM model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Molimo popunite sva polja."
                });
            }

            if (model.NewPassword != model.ConfirmPassword)
            {
                return Json(new
                {
                    success = false,
                    message = "Password i potvrda passworda se ne poklapaju."
                });
            }

            if (model.NewPassword == DefaultPassword)
            {
                return Json(new
                {
                    success = false,
                    message = "Novi password ne smije biti Sarajevo2026!."
                });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Korisnik nije pronađen."
                });
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (!result.Succeeded)
            {
                return Json(new
                {
                    success = false,
                    message = string.Join(" ", result.Errors.Select(e => e.Description))
                });
            }

            user.MustChangePassword = false;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return Json(new
                {
                    success = false,
                    message = "Password je promijenjen, ali spremanje korisničkog statusa nije uspjelo."
                });
            }

            return Json(new
            {
                success = true,
                message = "Password je uspješno promijenjen."
            });
        }
    }
}