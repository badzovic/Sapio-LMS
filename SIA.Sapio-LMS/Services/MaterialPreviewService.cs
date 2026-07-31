using System.Diagnostics;

namespace SIA.Sapio_LMS.Services
{
    public class MaterialPreviewService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public MaterialPreviewService(
            IWebHostEnvironment environment,
            IConfiguration configuration)
        {
            _environment = environment;
            _configuration = configuration;
        }

        public async Task<List<string>> GetOrCreatePreviewAsync(int materijalId, string filePath)
        {
            var originalFilePath = ResolvePhysicalPath(filePath);

            if (!File.Exists(originalFilePath))
                throw new FileNotFoundException("Originalni fajl nije pronađen.", originalFilePath);

            var previewFolder = GetPreviewFolder(materijalId);

            Directory.CreateDirectory(previewFolder);

            var existingPages = Directory
                .GetFiles(previewFolder, "page-*.png")
                .OrderBy(x => x)
                .ToList();

            if (existingPages.Any())
                return existingPages;

            var tempFolder = Path.Combine(previewFolder, "_temp");
            Directory.CreateDirectory(tempFolder);

            try
            {
                var pdfPath = await ConvertOfficeToPdfAsync(originalFilePath, tempFolder);

                await ConvertPdfToImagesAsync(pdfPath, previewFolder);

                return Directory
                    .GetFiles(previewFolder, "page-*.png")
                    .OrderBy(x => x)
                    .ToList();
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                    Directory.Delete(tempFolder, true);
            }
        }

        public List<string> GetPreviewPages(int materijalId)
        {
            var previewFolder = GetPreviewFolder(materijalId);

            if (!Directory.Exists(previewFolder))
                return new List<string>();

            return Directory
                .GetFiles(previewFolder, "page-*.png")
                .OrderBy(x => x)
                .ToList();
        }

        public string GetPreviewFolder(int materijalId)
        {
            return Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "subject-material-previews",
                materijalId.ToString());
        }

        private string ResolvePhysicalPath(string filePath)
        {
            var normalizedPath = filePath.Trim();

            if (normalizedPath.StartsWith("~/"))
                normalizedPath = normalizedPath.Substring(2);

            normalizedPath = normalizedPath.TrimStart('/', '\\');

            if (Path.IsPathRooted(normalizedPath))
                return Path.GetFullPath(normalizedPath);

            return Path.GetFullPath(
                Path.Combine(_environment.WebRootPath, normalizedPath));
        }

        private async Task<string> ConvertOfficeToPdfAsync(string originalFilePath, string outputFolder)
        {
            var libreOfficePath = _configuration["MaterialPreview:LibreOfficePath"];

            if (string.IsNullOrWhiteSpace(libreOfficePath))
                libreOfficePath = @"C:\Program Files\LibreOffice\program\soffice.exe";

            if (!File.Exists(libreOfficePath))
                throw new FileNotFoundException("LibreOffice nije pronađen.", libreOfficePath);

            var arguments =
                $"--headless --convert-to pdf --outdir \"{outputFolder}\" \"{originalFilePath}\"";

            await RunProcessAsync(libreOfficePath, arguments);

            var pdfFile = Directory
                .GetFiles(outputFolder, "*.pdf")
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(pdfFile) || !File.Exists(pdfFile))
                throw new Exception("Konverzija Office dokumenta u PDF nije uspjela.");

            return pdfFile;
        }

        private async Task ConvertPdfToImagesAsync(string pdfPath, string outputFolder)
        {
            var popplerPath = _configuration["MaterialPreview:PopplerPdftoppmPath"];

            if (string.IsNullOrWhiteSpace(popplerPath))
                popplerPath = @"C:\poppler\Library\bin\pdftoppm.exe";

            if (!File.Exists(popplerPath))
                throw new FileNotFoundException("Poppler pdftoppm nije pronađen.", popplerPath);

            var outputPrefix = Path.Combine(outputFolder, "page");

            var arguments =
                $"-png -r 150 \"{pdfPath}\" \"{outputPrefix}\"";

            await RunProcessAsync(popplerPath, arguments);

            var generatedPages = Directory
                .GetFiles(outputFolder, "page-*.png")
                .OrderBy(x => x)
                .ToList();

            if (!generatedPages.Any())
                throw new Exception("Konverzija PDF-a u slike nije uspjela.");

            RenamePagesToPaddedFormat(generatedPages);
        }

        private void RenamePagesToPaddedFormat(List<string> generatedPages)
        {
            var orderedPages = generatedPages
                .OrderBy(x => x)
                .ToList();

            for (int i = 0; i < orderedPages.Count; i++)
            {
                var newPath = Path.Combine(
                    Path.GetDirectoryName(orderedPages[i])!,
                    $"page-{(i + 1).ToString("D4")}.png");

                if (orderedPages[i] == newPath)
                    continue;

                if (File.Exists(newPath))
                    File.Delete(newPath);

                File.Move(orderedPages[i], newPath);
            }
        }

        private async Task RunProcessAsync(string fileName, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process
            {
                StartInfo = startInfo
            };

            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception(
                    $"Proces nije uspješno završen. ExitCode={process.ExitCode}. Output={output}. Error={error}");
            }
        }
    }
}