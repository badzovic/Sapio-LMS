namespace SIA.Sapio_LMS.Areas.Instructor.Models
{
    public class MySubjectVM
    {
        public int PredmetId { get; set; }
        public string Naziv { get; set; } = "";
        public string? NazivEng { get; set; }
        public DateTime DatumOd { get; set; }
    }
}