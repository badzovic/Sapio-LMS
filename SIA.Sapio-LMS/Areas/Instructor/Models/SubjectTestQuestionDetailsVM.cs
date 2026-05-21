namespace SIA.Sapio_LMS.Areas.Instructor.Models
{
    public class SubjectTestQuestionDetailsVM
    {
        public int ID { get; set; }
        public int? OriginalPitanjeId { get; set; }

        public string TekstPitanja { get; set; } = "";
        public string TipPitanja { get; set; } = "";
        public string Tezina { get; set; } = "";

        public decimal Bodovi { get; set; }
        public decimal? NegativniBodovi { get; set; }

        public string ParcijalnoBodovanje { get; set; } = "N";
        public string IsObavezno { get; set; } = "N";

        public int Redoslijed { get; set; }

        public List<SubjectTestAnswerDetailsVM> Odgovori { get; set; } = new();
    }
}