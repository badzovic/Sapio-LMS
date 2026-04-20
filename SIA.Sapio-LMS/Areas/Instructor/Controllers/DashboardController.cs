using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SIA.Sapio_LMS.Areas.Instructor.Controllers
{
    [Area("Instructor")]
    [Authorize(Roles = "Instruktor")]
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
