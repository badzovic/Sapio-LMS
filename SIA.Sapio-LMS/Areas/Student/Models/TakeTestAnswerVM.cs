namespace SIA.Sapio_LMS.Areas.Student.Models
{
    public class TakeTestAnswerVM
    {
        public int PolaganjeOdgovorId { get; set; }

        public int TestPitanjeOdgovorId { get; set; }

        public string TekstOdgovora { get; set; } = string.Empty;

        public int Redoslijed { get; set; }
        public string PredmetNaziv { get; set; } = "";
    }
}