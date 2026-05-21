using SIA.Sapio.LMS.Data.Entities;

namespace SIA.Sapio_LMS.Areas.Instructor.Models
{
    public class CreateSubjectTestVM
    {
        public int PredmetId { get; set; }

        public string PredmetNaziv { get; set; } = "";

        public string Naziv { get; set; } = "";

        public string? Opis { get; set; }

        public DateTime DatumOd { get; set; } = DateTime.Now;

        public DateTime DatumDo { get; set; } = DateTime.Now.AddDays(365);

        public int VrijemeTrajanjaMin { get; set; } = 30;

        public int BrojPitanja { get; set; }

        public int BrojLakih { get; set; }

        public int BrojSrednjih { get; set; }

        public int BrojTeskih { get; set; }

        public decimal MinProcenatProlaza { get; set; } = 70;

        public int BrojPokusaja { get; set; } = 1;

        public string RandomPitanja { get; set; } = "Y";

        public string RandomOdgovori { get; set; } = "Y";

        public int DostupnoLakih { get; set; }

        public int DostupnoSrednjih { get; set; }

        public int DostupnoTeskih { get; set; }

        public List<int> ObaveznaPitanjaIds { get; set; } = new();

        public List<CreateSubjectTestQuestionOptionVM> DostupnaPitanja { get; set; } = new();

        public int TestId { get; set; }
        public int TestKlasaId { get; set; }

        public List<TestKlasa> TestKlase { get; set; } = new();
    }
}