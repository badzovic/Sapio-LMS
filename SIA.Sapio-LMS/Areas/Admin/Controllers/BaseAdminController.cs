using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SIA.Sapio_LMS.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    public abstract class BaseAdminController : Controller
    {
        protected string? CurrentUserId =>
            User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        protected string? CurrentUserEmail =>
            User?.FindFirst(ClaimTypes.Email)?.Value;

        protected bool IsAdmin =>
            User?.IsInRole("Admin") ?? false;
    }
}