namespace SIA.Sapio_LMS.Areas.Student.Models
{
    public class TakeTestVM
    {
        public int PolaganjeId { get; set; }

        public int TestId { get; set; }

        public string NazivTesta { get; set; } = string.Empty;

        public string? Opis { get; set; }

        public DateTime VrijemeIsteka { get; set; }

        public List<TakeTestQuestionVM> Pitanja { get; set; } = new();
        public string PredmetNaziv { get; set; } = "";
    }
}