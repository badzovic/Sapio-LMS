namespace SIA.Sapio_LMS.Areas.Instructor.Models
{
    public class SubjectTestAnswerDetailsVM
    {
        public int ID { get; set; }
        public string TekstOdgovora { get; set; } = "";
        public string IsTacan { get; set; } = "N";
        public decimal? Bodovi { get; set; }
        public decimal? NegativniBodovi { get; set; }
        public int Redoslijed { get; set; }
    }
}