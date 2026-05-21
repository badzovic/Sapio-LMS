namespace SIA.Sapio_LMS.Areas.Instructor.Models
{
    public class CreateSubjectQuestionAnswerVM
    {
        public string? TekstOdgovora { get; set; } = "";
        public bool IsTacan { get; set; }
        public decimal? Bodovi { get; set; }
        public decimal? NegativniBodovi { get; set; }
        public int Redoslijed { get; set; }
    }
}