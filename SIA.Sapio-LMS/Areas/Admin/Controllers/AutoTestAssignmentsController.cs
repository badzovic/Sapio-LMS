using Microsoft.AspNetCore.Mvc;
using SIA.Sapio_LMS.Services;
using System.Security.Claims;

namespace SIA.Sapio_LMS.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AutoTestAssignmentsController : BaseAdminController
    {
        private readonly AutoTestAssignmentService _autoTestAssignmentService;

        public AutoTestAssignmentsController(AutoTestAssignmentService autoTestAssignmentService)
        {
            _autoTestAssignmentService = autoTestAssignmentService;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunForSubject(int predmetId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            await _autoTestAssignmentService.RunAutoAssignmentsAsync(
                predmetId,
                "MANUAL",
                userId);

            TempData["ImportMessage"] = "Auto dodjela testova je pokrenuta za odabrani predmet.";

            return RedirectToAction("Index", "Predmeti", new { area = "Admin" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunAll()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            await _autoTestAssignmentService.RunAutoAssignmentsAsync(
                null,
                "MANUAL",
                userId);

            TempData["ImportMessage"] = "Auto dodjela testova je pokrenuta za sve predmete.";

            return RedirectToAction("Index", "Predmeti", new { area = "Admin" });
        }
    }
}