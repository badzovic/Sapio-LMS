namespace SIA.Sapio.LMS.Web.Areas.Instructor.Models.TestResults
{
    public class InstructorTestResultDetailsVM
    {
        public int PolaganjeId { get; set; }

        public string StudentImePrezime { get; set; }

        public string TestNaziv { get; set; }

        public string Status { get; set; }

        public decimal OstvareniBodovi { get; set; }

        public decimal MaxBodovi { get; set; }

        public decimal Procenat { get; set; }

        public bool IsPassed { get; set; }
        public int PredmetId { get; set; }

        public List<InstructorEssayAnswerGradeVM> Questions { get; set; } = new();

        public List<InstructorEssayAnswerGradeVM> EssayAnswers { get; set; }
            = new();
    }
}