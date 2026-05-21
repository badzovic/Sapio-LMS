namespace SIA.Sapio_LMS.Areas.Instructor.Models
{
    public class SubjectQuestionAnswerDetailsVM
    {
        public int ID { get; set; }

        public string TekstOdgovora { get; set; } = "";

        public string IsTacan { get; set; } = "N";

        public int? Redoslijed { get; set; }

        public decimal? Bodovi { get; set; }

        public decimal? NegativniBodovi { get; set; }

        public string Aktivno { get; set; } = "Y";
    }
}