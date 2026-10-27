
namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class CustomTrainingMatrixIndexVM
    {
        public int? KorisnikId { get; set; }

        public string? Search { get; set; }

        public string ImePrezime { get; set; } = "";

        public List<CustomTrainingMatrixItemVM> Predmeti { get; set; } = new();
    }
}
