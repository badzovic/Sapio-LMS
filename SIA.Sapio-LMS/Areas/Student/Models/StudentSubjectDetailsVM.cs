namespace SIA.Sapio_LMS.Areas.Student.Models
{
    public class StudentSubjectDetailsVM
    {
        public int PredmetId { get; set; }

        public string? Naziv { get; set; }

        public string? NazivEng { get; set; }

        public List<StudentSubjectMaterialVM> Materijali { get; set; } = new();

    }
}