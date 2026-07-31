using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SIA.Sapio_LMS.Services
{
    public class MaterialPdfService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public MaterialPdfService(
            IWebHostEnvironment environment,
            IConfiguration configuration)
        {
            _environment = environment;
            _configuration = configuration;
        }

        public async Task<string> GetOrCreatePdfAsync(int materijalId, string filePath)
        {
            var originalFilePath = ResolvePhysicalPath(filePath);

            if (!File.Exists(originalFilePath))
                throw new FileNotFoundException("Originalni fajl nije pronađen.", originalFilePath);

            var cacheFolder = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "subject-material-pdf-cache");

            Directory.CreateDirectory(cacheFolder);

            var cachedPdfPath = Path.Combine(cacheFolder, $"{materijalId}.pdf");

            if (File.Exists(cachedPdfPath))
                return cachedPdfPath;

            var tempFolder = Path.Combine(cacheFolder, "_temp", materijalId.ToString());

            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, true);

            Directory.CreateDirectory(tempFolder);

            try
            {
                var generatedPdfPath = await ConvertToPdfAsync(originalFilePath, tempFolder);

                if (File.Exists(cachedPdfPath))
                    File.Delete(cachedPdfPath);

                File.Copy(generatedPdfPath, cachedPdfPath);

                return cachedPdfPath;
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                    Directory.Delete(tempFolder, true);
            }
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

        private async Task<string> ConvertToPdfAsync(string originalFilePath, string outputFolder)
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
                throw new Exception("Konverzija fajla u PDF nije uspjela.");

            return pdfFile;
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
                    $"LibreOffice konverzija nije uspjela. ExitCode={process.ExitCode}. Output={output}. Error={error}");
            }
        }
    }
}