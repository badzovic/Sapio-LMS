using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SIA.Sapio_LMS.Areas.Instructor.Controllers
{
    [Authorize(Roles = "Instruktor")]
    public abstract class BaseInstructorController : Controller
    {
        protected string? CurrentUserId =>
            User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        protected string? CurrentUserEmail =>
            User?.FindFirst(ClaimTypes.Email)?.Value;

        protected bool IsInstructor =>
            User?.IsInRole("Instruktor") ?? false;
    }
}