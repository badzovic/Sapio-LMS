using Microsoft.AspNetCore.Mvc;

namespace SIA.Sapio_LMS.Areas.Instructor.Controllers
{
    [Area("Instructor")]
    public class DashboardController : BaseInstructorController
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}