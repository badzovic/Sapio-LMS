namespace SIA.Sapio_LMS.Areas.Student.Models
{
    public class StudentMaterialViewerVM
    {
        public int MaterijalId { get; set; }

        public int PredmetId { get; set; }

        public string? NazivPredmeta { get; set; }

        public string? NazivMaterijala { get; set; }

        public string? Opis { get; set; }

        public string? TipMaterijala { get; set; }

        public string? Url { get; set; }

        public string? EmbedUrl { get; set; }

        public bool IsYoutube { get; set; }

        public bool IsLink { get; set; }

        public bool IsFile { get; set; }

        public string? NazivOriginalnogFajla { get; set; }

        public string? NazivFajla { get; set; }

        public string? ContentType { get; set; }

        public string? Ekstenzija { get; set; }

        public string? FileViewerUrl { get; set; }

        public bool IsPdf { get; set; }

        public bool IsImage { get; set; }

        public bool IsOfficeFile { get; set; }

        public List<string> PreviewPageUrls { get; set; } = new();

        public bool HasPreviewPages { get; set; }

        public string? PreviewErrorMessage { get; set; }
    }
}