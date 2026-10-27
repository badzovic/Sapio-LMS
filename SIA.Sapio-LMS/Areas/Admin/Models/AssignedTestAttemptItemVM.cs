using System;

namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class AssignedTestAttemptItemVM
    {
        public int PolaganjeId { get; set; }
        public int? DodjelaId { get; set; }

        public int KorisnikId { get; set; }
        public int TestId { get; set; }

        public string ImePrezime { get; set; } = "";
        public string PredmetNaziv { get; set; } = "";
        public string TestNaziv { get; set; } = "";

        public int RedniBrojPokusaja { get; set; }

        public DateTime DatumPocetka { get; set; }
        public DateTime? DatumZavrsetka { get; set; }

        public string Status { get; set; } = "";
        public string? IsPassed { get; set; }

        public decimal? Procenat { get; set; }
        public decimal? OstvareniBodovi { get; set; }
        public decimal? MaxBodovi { get; set; }

        public bool AutoSubmitted { get; set; }
    }
}