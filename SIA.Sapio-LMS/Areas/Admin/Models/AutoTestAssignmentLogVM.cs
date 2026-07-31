namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class AutoTestAssignmentLogVM
    {
        public int ID { get; set; }

        public string TipDogadjaja { get; set; } = "";
        public string? Razlog { get; set; }

        public int? PredmetId { get; set; }
        public string? PredmetNaziv { get; set; }

        public int? KorisnikId { get; set; }
        public string? KorisnikImePrezime { get; set; }

        public int? TestId { get; set; }
        public string? TestNaziv { get; set; }

        public int? TestKlasaId { get; set; }
        public string? TestKlasaNaziv { get; set; }

        public int? PogodjenoKorisnika { get; set; }

        public string? Poruka { get; set; }

        public DateTime DatumKreiranja { get; set; }
    }
}