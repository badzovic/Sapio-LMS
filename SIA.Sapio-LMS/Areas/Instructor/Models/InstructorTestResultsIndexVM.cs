namespace SIA.Sapio.LMS.Web.Areas.Instructor.Models.TestResults
{
    public class InstructorTestResultsIndexVM
    {
        public int PredmetId { get; set; }

        public string PredmetNaziv { get; set; }

        public List<InstructorTestResultListItemVM> Results { get; set; }
            = new();
    }
}