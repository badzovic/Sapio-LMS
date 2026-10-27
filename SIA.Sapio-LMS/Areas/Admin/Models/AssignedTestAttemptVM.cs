
namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class AssignedTestAttemptVM
    {
        public int ID { get; set; }

        public int RedniBrojPokusaja { get; set; }

        public DateTime DatumPocetka { get; set; }
        public DateTime? DatumZavrsetka { get; set; }

        public string Status { get; set; } = "";

        public decimal? OstvareniBodovi { get; set; }
        public decimal? MaxBodovi { get; set; }
        public decimal? Procenat { get; set; }

        public string? IsPassed { get; set; }

        public bool AutoSubmitted { get; set; }
    }
}
