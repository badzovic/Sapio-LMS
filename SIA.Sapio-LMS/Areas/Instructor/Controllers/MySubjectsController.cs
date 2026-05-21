using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio_LMS.Areas.Instructor.Models;

namespace SIA.Sapio_LMS.Areas.Instructor.Controllers
{
    [Area("Instructor")]
    public class MySubjectsController : BaseInstructorController
    {
        private readonly ApplicationDbContext _dbContext;

        public MySubjectsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index()
        {
            var lokalniKorisnik = await GetCurrentLokalniKorisnikAsync();

            if (lokalniKorisnik == null)
                return View(new List<MySubjectVM>());

            var model = await _dbContext.PREDMET_INSTRUKTORI
                .Include(x => x.Predmet)
                .Where(x =>
                    x.KORISNIK_ID == lokalniKorisnik.ID &&
                    x.AKTIVNO == "Y" &&
                    x.Predmet != null &&
                    x.Predmet.AKTIVNO == "Y")
                .OrderBy(x => x.Predmet!.NAZIV)
                .Select(x => new MySubjectVM
                {
                    PredmetId = x.PREDMET_ID,
                    Naziv = x.Predmet!.NAZIV,
                    NazivEng = x.Predmet.NAZIV_ENG,
                    DatumOd = x.DATUM_OD
                })
                .ToListAsync();

            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var lokalniKorisnik = await GetCurrentLokalniKorisnikAsync();

            if (lokalniKorisnik == null)
                return Forbid();

            var model = await _dbContext.PREDMET_INSTRUKTORI
                .Include(x => x.Predmet)
                .Where(x =>
                    x.PREDMET_ID == id &&
                    x.KORISNIK_ID == lokalniKorisnik.ID &&
                    x.AKTIVNO == "Y" &&
                    x.Predmet != null &&
                    x.Predmet.AKTIVNO == "Y")
                .Select(x => new MySubjectDetailsVM
                {
                    PredmetId = x.PREDMET_ID,
                    Naziv = x.Predmet!.NAZIV,
                    NazivEng = x.Predmet.NAZIV_ENG,
                    InstruktorOd = x.DATUM_OD
                })
                .FirstOrDefaultAsync();

            if (model == null)
                return NotFound();

            return View(model);
        }

        private async Task<Korisnik?> GetCurrentLokalniKorisnikAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentUserId))
                return null;

            return await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x => x.USER_ID == CurrentUserId && x.AKTIVAN == "Y");
        }
    }
}