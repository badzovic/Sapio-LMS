namespace SIA.Sapio_LMS.Areas.Instructor.Models
{
    public class SubjectQuestionDetailsVM
    {
        public int ID { get; set; }

        public int PredmetId { get; set; }

        public string PredmetNaziv { get; set; } = "";

        public string TekstPitanja { get; set; } = "";

        public string TipPitanja { get; set; } = "";

        public string TipPitanjaSifra { get; set; } = "";

        public string Tezina { get; set; } = "";

        public decimal Bodovi { get; set; }

        public decimal? NegativniBodovi { get; set; }

        public string ParcijalnoBodovanje { get; set; } = "N";

        public string Aktivno { get; set; } = "Y";

        public DateTime DatumKreiranja { get; set; }

        public DateTime? DatumIzmjene { get; set; }

        public List<SubjectQuestionAnswerDetailsVM> Odgovori { get; set; } = new();
    }
}