namespace SIA.Sapio_LMS.Areas.Instructor.Models
{
    public class SubjectTestIndexVM
    {
        public int PredmetId { get; set; }
        public string PredmetNaziv { get; set; } = "";

        public string Status { get; set; } = "all";

        public DateTime? DatumOd { get; set; }
        public DateTime? DatumDo { get; set; }

        public List<SubjectTestVM> Tests { get; set; } = new();
    }
}