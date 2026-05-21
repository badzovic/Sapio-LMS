using System.Collections.Generic;

namespace SIA.Sapio_LMS.Areas.Models
{
    public class AssignTestStudentsVM
    {
        public int TestId { get; set; }

        public string TestNaziv { get; set; } = "";

        public string PredmetNaziv { get; set; } = "";

        public List<AssignTestStudentItemVM> Studenti { get; set; } = new();

        public List<int> SelectedStudentIds { get; set; } = new();
    }
}