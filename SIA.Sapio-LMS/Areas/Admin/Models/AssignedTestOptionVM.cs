
namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class AssignedTestOptionVM
    {
        public int ID { get; set; }

        public int Redoslijed { get; set; }

        public string TekstOdgovora { get; set; } = "";

        public bool IsTacan { get; set; }
        public bool IsSelected { get; set; }

        public decimal? Bodovi { get; set; }
        public decimal? NegativniBodovi { get; set; }
    }
}
