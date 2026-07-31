namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class AutoTestAssignmentRunDetailsVM : AutoTestAssignmentRunVM
    {
        public List<AutoTestAssignmentLogVM> Logovi { get; set; } = new();
    }
}