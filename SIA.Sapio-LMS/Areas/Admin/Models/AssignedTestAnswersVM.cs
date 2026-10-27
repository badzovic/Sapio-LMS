
namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class AssignedTestAnswersVM
    {
        public int DodjelaId { get; set; }
        public int TestId { get; set; }
        public int KorisnikId { get; set; }

        public string ImePrezime { get; set; } = "";
        public string PredmetNaziv { get; set; } = "";
        public string TestNaziv { get; set; } = "";
        public string TestKlasaNaziv { get; set; } = "";

        public DateTime DatumDodjele { get; set; }
        public DateTime? RokZaPolaganje { get; set; }

        public int? OdabranoPolaganjeId { get; set; }

        public List<AssignedTestAttemptVM> Pokusaji { get; set; } = new();

        public List<AssignedTestQuestionVM> Pitanja { get; set; } = new();
    }
}
