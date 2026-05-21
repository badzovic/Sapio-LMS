namespace SIA.Sapio_LMS.Areas.Instructor.Models
{
    public class MySubjectDetailsVM
    {
        public int PredmetId { get; set; }
        public string Naziv { get; set; } = "";
        public string? NazivEng { get; set; }
        public DateTime InstruktorOd { get; set; }
    }
}