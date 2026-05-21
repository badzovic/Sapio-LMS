namespace SIA.Sapio_LMS.Areas.Student.Models
{
    public class TakeTestQuestionVM
    {
        public int PolaganjePitanjeId { get; set; }

        public int TestPitanjeId { get; set; }

        public string TekstPitanja { get; set; } = string.Empty;

        public string TipPitanjaSifra { get; set; } = string.Empty;

        public int Redoslijed { get; set; }

        public List<TakeTestAnswerVM> Odgovori { get; set; } = new();
    }
}