
namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class AssignedTestItemVM
    {
        public int ID { get; set; }

        public int TestId { get; set; }
        public int KorisnikId { get; set; }

        // Korisnik i test
        public string ImePrezime { get; set; } = "";
        public string PredmetNaziv { get; set; } = "";
        public string TestNaziv { get; set; } = "";
        public string TestKlasaNaziv { get; set; } = "";

        // Dodjela
        public DateTime DatumDodjele { get; set; }
        public DateTime? RokZaPolaganje { get; set; }

        public string NacinDodjele { get; set; } = "";
        public string? DodijelioUserId { get; set; }

        // Status i polaganje
        public string StatusDodjele { get; set; } = "";
        public string StatusPolaganja { get; set; } = "";

        public int BrojPokusaja { get; set; }
        public decimal? NajboljiProcenat { get; set; }

        // Aktivnost dodjele
        public bool Aktivno { get; set; }
    }
}
