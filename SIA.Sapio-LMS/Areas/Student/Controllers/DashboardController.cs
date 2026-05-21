using Microsoft.AspNetCore.Mvc;

namespace SIA.Sapio_LMS.Areas.Student.Controllers
{
    [Area("Student")]
    public class DashboardController : BaseStudentController
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}