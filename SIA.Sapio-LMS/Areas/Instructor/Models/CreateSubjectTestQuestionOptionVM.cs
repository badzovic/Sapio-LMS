namespace SIA.Sapio_LMS.Areas.Instructor.Models
{
    public class CreateSubjectTestQuestionOptionVM
    {
        public int ID { get; set; }

        public string TekstPitanja { get; set; } = "";

        public string TipPitanja { get; set; } = "";

        public string Tezina { get; set; } = "";

        public string TezinaSifra { get; set; } = "";

        public decimal Bodovi { get; set; }
    }
}