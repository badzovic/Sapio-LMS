using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio_LMS.Areas.Instructor.Models;

namespace SIA.Sapio_LMS.Areas.Instructor.Controllers
{
    [Area("Instructor")]
    public class SubjectMaterialsController : BaseInstructorController
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IWebHostEnvironment _environment;

        public SubjectMaterialsController(
            ApplicationDbContext dbContext,
            IWebHostEnvironment environment)
        {
            _dbContext = dbContext;
            _environment = environment;
        }

        public async Task<IActionResult> Index(int predmetId)
        {
            var predmet = await GetInstructorPredmetAsync(predmetId);

            if (predmet == null)
                return NotFound();

            var model = await _dbContext.PREDMET_MATERIJALI
                .Where(x =>
                    x.PREDMET_ID == predmetId &&
                    x.AKTIVNO == "Y")
                .OrderBy(x => x.REDOSLIJED)
                .ThenByDescending(x => x.DATUM_KREIRANJA)
                .Select(x => new SubjectMaterialVM
                {
                    ID = x.ID,
                    PredmetId = x.PREDMET_ID,
                    PredmetNaziv = predmet.NAZIV,

                    Naslov = x.NASLOV,
                    Opis = x.OPIS,

                    TipMaterijala = x.TIP_MATERIJALA,

                    OriginalFileName = x.ORIGINAL_FILE_NAME,
                    FilePath = x.FILE_PATH,
                    Url = x.URL,

                    Redoslijed = x.REDOSLIJED,
                    Aktivno = x.AKTIVNO,

                    DatumKreiranja = x.DATUM_KREIRANJA
                })
                .ToListAsync();

            ViewBag.PredmetId = predmet.ID;
            ViewBag.PredmetNaziv = predmet.NAZIV;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(
            int predmetId,
            string naslov,
            string? opis,
            string tipMaterijala,
            string? url,
            IFormFile? file)
        {
            var predmet = await GetInstructorPredmetAsync(predmetId);

            if (predmet == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(naslov))
            {
                TempData["ImportMessage"] = "Naslov je obavezan.";
                return RedirectToAction(nameof(Index), new { predmetId });
            }

            var entity = new PredmetMaterijal
            {
                PREDMET_ID = predmetId,
                NASLOV = naslov.Trim(),
                OPIS = opis,
                TIP_MATERIJALA = tipMaterijala,
                AKTIVNO = "Y",
                CREATED_BY_USER_ID = CurrentUserId,
                DATUM_KREIRANJA = DateTime.Now
            };

            // LINK
            if (tipMaterijala == "LINK")
            {
                entity.URL = url;
            }

            // FILE
            if (tipMaterijala == "FILE")
            {
                if (file == null || file.Length == 0)
                {
                    TempData["ImportMessage"] = "Odaberite fajl.";
                    return RedirectToAction(nameof(Index), new { predmetId });
                }

                var uploadsRoot = Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "subject-materials",
                    predmetId.ToString());

                if (!Directory.Exists(uploadsRoot))
                    Directory.CreateDirectory(uploadsRoot);

                var generatedFileName =
                    $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

                var fullPath = Path.Combine(uploadsRoot, generatedFileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                entity.FILE_NAME = generatedFileName;
                entity.ORIGINAL_FILE_NAME = file.FileName;
                entity.FILE_PATH =
                    $"/uploads/subject-materials/{predmetId}/{generatedFileName}";
                entity.CONTENT_TYPE = file.ContentType;
                entity.FILE_SIZE_BYTES = file.Length;
            }

            _dbContext.PREDMET_MATERIJALI.Add(entity);

            await _dbContext.SaveChangesAsync();

            TempData["ImportMessage"] = "Materijal uspješno dodan.";

            return RedirectToAction(nameof(Index), new { predmetId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id, int predmetId)
        {
            var predmet = await GetInstructorPredmetAsync(predmetId);

            if (predmet == null)
                return NotFound();

            var entity = await _dbContext.PREDMET_MATERIJALI
                .FirstOrDefaultAsync(x =>
                    x.ID == id &&
                    x.PREDMET_ID == predmetId &&
                    x.AKTIVNO == "Y");

            if (entity == null)
                return NotFound();

            entity.AKTIVNO = "N";
            entity.DATUM_IZMJENE = DateTime.Now;

            await _dbContext.SaveChangesAsync();

            TempData["ImportMessage"] = "Materijal deaktiviran.";

            return RedirectToAction(nameof(Index), new { predmetId });
        }

        private async Task<Predmet?> GetInstructorPredmetAsync(int predmetId)
        {
            if (string.IsNullOrWhiteSpace(CurrentUserId))
                return null;

            var lokalniKorisnik = await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x =>
                    x.USER_ID == CurrentUserId &&
                    x.AKTIVAN == "Y");

            if (lokalniKorisnik == null)
                return null;

            return await _dbContext.PREDMET_INSTRUKTORI
                .Include(x => x.Predmet)
                .Where(x =>
                    x.PREDMET_ID == predmetId &&
                    x.KORISNIK_ID == lokalniKorisnik.ID &&
                    x.AKTIVNO == "Y" &&
                    x.Predmet != null &&
                    x.Predmet.AKTIVNO == "Y")
                .Select(x => x.Predmet)
                .FirstOrDefaultAsync();
        }
    }
}