namespace SIA.Sapio.LMS.Web.Areas.Instructor.Models.TestResults
{
    public class InstructorTestResultListItemVM
    {
        public int PolaganjeId { get; set; }

        public string StudentImePrezime { get; set; }

        public string TestNaziv { get; set; }

        public DateTime DatumPocetka { get; set; }

        public DateTime? DatumZavrsetka { get; set; }

        public string Status { get; set; }

        public int RedniBrojPokusaja { get; set; }

        public decimal OstvareniBodovi { get; set; }

        public decimal MaxBodovi { get; set; }

        public decimal Procenat { get; set; }

        public bool IsPassed { get; set; }

        public bool HasPendingEssayReview { get; set; }
    }
}