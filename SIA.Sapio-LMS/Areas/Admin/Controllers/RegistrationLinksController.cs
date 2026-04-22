using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio_LMS.Areas.Admin.Models.RegistrationLinks;

namespace SIA.Sapio_LMS.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class RegistrationLinksController : BaseAdminController
    {
        private readonly ApplicationDbContext _dbContext;

        public RegistrationLinksController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;

            var links = await _dbContext.REGISTRATION_TOKENS
                .OrderByDescending(x => x.CREATED_AT)
                .Select(x => new RegistrationLinkRowVM
                {
                    Id = x.ID,
                    Token = x.TOKEN,
                    CreatedAt = x.CREATED_AT,
                    ExpiresAt = x.EXPIRES_AT,
                    IsActive = x.IS_ACTIVE,
                    UsedCount = x.USED_COUNT,
                    Note = x.NOTE,
                    IsCurrentlyValid = x.IS_ACTIVE == "Y" && x.EXPIRES_AT >= now
                })
                .ToListAsync();

            var activeLink = links.FirstOrDefault(x => x.IsCurrentlyValid);

            var model = new RegistrationLinkIndexVM
            {
                Links = links,
                ActiveRegistrationUrl = activeLink != null
                    ? $"{Request.Scheme}://{Request.Host}/Registration/Register?token={activeLink.Token}"
                    : null
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind(Prefix = "CreateForm")] CreateRegistrationLinkVM model)
        {
            if (!ModelState.IsValid)
            {
                return await RebuildIndexWithModel(model);
            }

            var userId = User?.Claims?.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var activeLinks = await _dbContext.REGISTRATION_TOKENS
                .Where(x => x.IS_ACTIVE == "Y")
                .ToListAsync();

            foreach (var link in activeLinks)
            {
                link.IS_ACTIVE = "N";
                link.DEACTIVATED_AT = DateTime.Now;
                link.DEACTIVATED_BY_USER_ID = userId;
            }

            var token = GenerateSecureToken();
            var now = DateTime.Now;

            var entity = new SIA.Sapio.LMS.Data.Entities.RegistrationToken
            {
                TOKEN = token,
                CREATED_AT = now,
                EXPIRES_AT = now.AddMinutes(model.DurationMinutes),
                IS_ACTIVE = "Y",
                USED_COUNT = 0,
                CREATED_BY_USER_ID = userId,
                NOTE = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim()
            };

            _dbContext.REGISTRATION_TOKENS.Add(entity);
            await _dbContext.SaveChangesAsync();

            TempData["ImportMessage"] = $"Registracijski link je uspješno generisan i važi {model.DurationMinutes} minuta.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id)
        {
            var token = await _dbContext.REGISTRATION_TOKENS
                .FirstOrDefaultAsync(x => x.ID == id);

            if (token == null)
            {
                TempData["ImportMessage"] = "Registracijski link nije pronađen.";
                return RedirectToAction(nameof(Index));
            }

            if (token.IS_ACTIVE != "Y")
            {
                TempData["ImportMessage"] = "Registracijski link je već deaktiviran.";
                return RedirectToAction(nameof(Index));
            }

            var userId = User?.Claims?.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            token.IS_ACTIVE = "N";
            token.DEACTIVATED_AT = DateTime.Now;
            token.DEACTIVATED_BY_USER_ID = userId;

            await _dbContext.SaveChangesAsync();

            TempData["ImportMessage"] = "Registracijski link je deaktiviran.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> RebuildIndexWithModel(CreateRegistrationLinkVM createModel)
        {
            var now = DateTime.Now;

            var links = await _dbContext.REGISTRATION_TOKENS
                .OrderByDescending(x => x.CREATED_AT)
                .Select(x => new RegistrationLinkRowVM
                {
                    Id = x.ID,
                    Token = x.TOKEN,
                    CreatedAt = x.CREATED_AT,
                    ExpiresAt = x.EXPIRES_AT,
                    IsActive = x.IS_ACTIVE,
                    UsedCount = x.USED_COUNT,
                    Note = x.NOTE,
                    IsCurrentlyValid = x.IS_ACTIVE == "Y" && x.EXPIRES_AT >= now
                })
                .ToListAsync();

            var activeLink = links.FirstOrDefault(x => x.IsCurrentlyValid);

            var model = new RegistrationLinkIndexVM
            {
                CreateForm = createModel,
                Links = links,
                ActiveRegistrationUrl = activeLink != null
                    ? $"{Request.Scheme}://{Request.Host}/Registration/Register?token={activeLink.Token}"
                    : null
            };

            return View("Index", model);
        }

        private static string GenerateSecureToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
    }
}