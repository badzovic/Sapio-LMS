using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio_LMS.Areas.Student.Models;
using SIA.Sapio_LMS.Services;

namespace SIA.Sapio_LMS.Areas.Student.Controllers
{
    [Area("Student")]
    public class SubjectsController : BaseStudentController
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserTrainingMatrixService _userTrainingMatrixService;
        private readonly IWebHostEnvironment _environment;
        private readonly MaterialPdfService _materialPdfService;

        public SubjectsController(
            ApplicationDbContext dbContext,
            UserTrainingMatrixService userTrainingMatrixService,
            IWebHostEnvironment environment,
            MaterialPdfService materialPdfService)
        {
            _dbContext = dbContext;
            _userTrainingMatrixService = userTrainingMatrixService;
            _environment = environment;
            _materialPdfService = materialPdfService;
        }

        public async Task<IActionResult> Index()
        {
            var korisnik = await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x => x.USER_ID == CurrentUserId);

            if (korisnik == null)
            {
                ViewBag.TrainingMatrix = null;
                return View();
            }

            ViewBag.TrainingMatrix =
                await _userTrainingMatrixService.GetForRadnikAsync(korisnik.ACCESS_KEY);

            return View();
        }

        public async Task<IActionResult> Details(int id)
        {
            var korisnik = await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x => x.USER_ID == CurrentUserId);

            if (korisnik == null)
                return RedirectToAction(nameof(Index));

            var predmet = await _dbContext.PREDMETI
                .FirstOrDefaultAsync(x => x.ID == id && x.AKTIVNO == "Y");

            if (predmet == null)
                return NotFound();

            var model = new StudentSubjectDetailsVM
            {
                PredmetId = predmet.ID,
                Naziv = predmet.NAZIV,
                NazivEng = predmet.NAZIV_ENG,
                Materijali = await _dbContext.PREDMET_MATERIJALI
                    .Where(x => x.PREDMET_ID == predmet.ID && x.AKTIVNO == "Y")
                    .OrderBy(x => x.REDOSLIJED ?? int.MaxValue)
                    .ThenByDescending(x => x.DATUM_KREIRANJA)
                    .Select(x => new StudentSubjectMaterialVM
                    {
                        Id = x.ID,
                        Naziv = x.NASLOV,
                        Opis = x.OPIS,
                        TipMaterijala = x.TIP_MATERIJALA,
                        PutanjaFajla = x.FILE_PATH,
                        Url = x.URL,
                        NazivOriginalnogFajla = x.ORIGINAL_FILE_NAME,
                        NazivFajla = x.FILE_NAME,
                        ContentType = x.CONTENT_TYPE
                    })
                    .ToListAsync()
            };

            return View(model);
        }

        public async Task<IActionResult> ViewMaterial(int id)
        {
            var korisnik = await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x => x.USER_ID == CurrentUserId);

            if (korisnik == null)
                return RedirectToAction(nameof(Index));

            var materijal = await _dbContext.PREDMET_MATERIJALI
                .Include(x => x.Predmet)
                .FirstOrDefaultAsync(x => x.ID == id && x.AKTIVNO == "Y");

            if (materijal == null || materijal.Predmet == null)
                return NotFound();

            if (materijal.Predmet.AKTIVNO != "Y")
                return NotFound();

            var tip = (materijal.TIP_MATERIJALA ?? "").Trim().ToUpper();

            var nazivFajla = !string.IsNullOrWhiteSpace(materijal.ORIGINAL_FILE_NAME)
                ? materijal.ORIGINAL_FILE_NAME
                : materijal.FILE_NAME;

            var ekstenzija = Path.GetExtension(nazivFajla ?? "").Trim().ToLower();

            var isPdf = ekstenzija == ".pdf";
            var isImage = ekstenzija is ".jpg" or ".jpeg" or ".png" or ".webp";
            var isOfficeFile = ekstenzija is ".doc" or ".docx" or ".ppt" or ".pptx";

            var model = new StudentMaterialViewerVM
            {
                MaterijalId = materijal.ID,
                PredmetId = materijal.PREDMET_ID,
                NazivPredmeta = materijal.Predmet.NAZIV,
                NazivMaterijala = materijal.NASLOV,
                Opis = materijal.OPIS,
                TipMaterijala = materijal.TIP_MATERIJALA,
                Url = materijal.URL,
                EmbedUrl = GetYoutubeEmbedUrl(materijal.URL),
                IsYoutube = !string.IsNullOrWhiteSpace(GetYoutubeEmbedUrl(materijal.URL)),
                IsLink = tip == "LINK",
                IsFile = tip == "FILE",
                NazivOriginalnogFajla = materijal.ORIGINAL_FILE_NAME,
                NazivFajla = materijal.FILE_NAME,
                ContentType = materijal.CONTENT_TYPE,
                Ekstenzija = ekstenzija,
                FileViewerUrl = tip == "FILE"
                    ? Url.Action(nameof(RenderMaterialFile), "Subjects", new { area = "Student", id = materijal.ID })
                    : null,
                IsPdf = isPdf || isOfficeFile,
                IsImage = isImage,
                IsOfficeFile = isOfficeFile
            };

            return View(model);
        }

        public async Task<IActionResult> RenderMaterialFile(int id)
        {
            var korisnik = await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x => x.USER_ID == CurrentUserId);

            if (korisnik == null)
                return Unauthorized();

            var materijal = await _dbContext.PREDMET_MATERIJALI
                .Include(x => x.Predmet)
                .FirstOrDefaultAsync(x => x.ID == id && x.AKTIVNO == "Y");

            if (materijal == null || materijal.Predmet == null)
                return NotFound();

            if (materijal.Predmet.AKTIVNO != "Y")
                return NotFound();

            if ((materijal.TIP_MATERIJALA ?? "").Trim().ToUpper() != "FILE")
                return BadRequest();

            if (string.IsNullOrWhiteSpace(materijal.FILE_PATH))
                return NotFound("Putanja fajla nije upisana.");

            var originalPath = ResolvePhysicalPath(materijal.FILE_PATH);

            if (!System.IO.File.Exists(originalPath))
                return NotFound($"Fajl nije pronađen: {originalPath}");

            var fileName = !string.IsNullOrWhiteSpace(materijal.ORIGINAL_FILE_NAME)
                ? materijal.ORIGINAL_FILE_NAME
                : materijal.FILE_NAME ?? Path.GetFileName(originalPath);

            var ekstenzija = Path.GetExtension(fileName).Trim().ToLower();

            var isOfficeFile = ekstenzija is ".doc" or ".docx" or ".ppt" or ".pptx";

            string fileToRender;
            string contentType;

            if (isOfficeFile)
            {
                fileToRender = await _materialPdfService.GetOrCreatePdfAsync(materijal.ID, materijal.FILE_PATH);
                contentType = "application/pdf";
                fileName = Path.GetFileNameWithoutExtension(fileName) + ".pdf";
            }
            else
            {
                fileToRender = originalPath;

                contentType = materijal.CONTENT_TYPE ?? "";

                if (string.IsNullOrWhiteSpace(contentType))
                {
                    var provider = new FileExtensionContentTypeProvider();

                    if (!provider.TryGetContentType(fileToRender, out contentType))
                        contentType = "application/octet-stream";
                }
            }

            Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
            Response.Headers["X-Content-Type-Options"] = "nosniff";

            return PhysicalFile(fileToRender, contentType, enableRangeProcessing: true);
        }

        private string ResolvePhysicalPath(string filePath)
        {
            var normalizedPath = filePath.Trim();

            if (normalizedPath.StartsWith("~/"))
                normalizedPath = normalizedPath.Substring(2);

            normalizedPath = normalizedPath.TrimStart('/', '\\');

            if (Path.IsPathRooted(normalizedPath))
                return Path.GetFullPath(normalizedPath);

            return Path.GetFullPath(Path.Combine(_environment.WebRootPath, normalizedPath));
        }

        private string? GetYoutubeEmbedUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;

            var host = uri.Host.ToLower();
            string? videoId = null;

            if (host.Contains("youtu.be"))
            {
                videoId = uri.AbsolutePath.Trim('/');
            }
            else if (host.Contains("youtube.com"))
            {
                if (uri.AbsolutePath.StartsWith("/watch"))
                {
                    var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);

                    if (query.TryGetValue("v", out var value))
                        videoId = value.FirstOrDefault();
                }
                else if (uri.AbsolutePath.StartsWith("/embed/"))
                {
                    videoId = uri.AbsolutePath.Replace("/embed/", "").Split('/').FirstOrDefault();
                }
                else if (uri.AbsolutePath.StartsWith("/shorts/"))
                {
                    videoId = uri.AbsolutePath.Replace("/shorts/", "").Split('/').FirstOrDefault();
                }
            }

            if (string.IsNullOrWhiteSpace(videoId))
                return null;

            return $"https://www.youtube.com/embed/{videoId}";
        }
    }
}