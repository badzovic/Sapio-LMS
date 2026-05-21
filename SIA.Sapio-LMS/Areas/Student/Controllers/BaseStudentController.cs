using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SIA.Sapio_LMS.Areas.Student.Controllers
{
    [Area("Student")]
    [Authorize(Roles = "Student")]
    public abstract class BaseStudentController : Controller
    {
        protected string? CurrentUserId =>
            User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        protected string? CurrentUserEmail =>
            User?.FindFirst(ClaimTypes.Email)?.Value;

        protected bool IsStudent =>
            User?.IsInRole("Student") ?? false;
    }
}