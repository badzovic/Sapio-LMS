namespace SIA.Sapio.LMS.Web.Areas.Instructor.Models.TestResults
{
    public class InstructorEssayAnswerGradeVM
    {
        public int StudentOdgovorId { get; set; }

        public int PolaganjeId { get; set; }

        public int TestPitanjeId { get; set; }

        public string PitanjeTekst { get; set; }

        public string EssayOdgovor { get; set; }

        public decimal MaxBodovi { get; set; }

        public decimal? DodijeljeniBodovi { get; set; }
        public string TipPitanjaSifra { get; set; }

        public decimal Bodovi { get; set; }

        public decimal NegativniBodovi { get; set; }
        public int PolaganjePitanjeId { get; set; }

        public List<InstructorEssayAnswerOptionVM> Odgovori { get; set; } = new();
    }
}