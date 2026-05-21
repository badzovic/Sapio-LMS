namespace SIA.Sapio_LMS.Areas.Instructor.Models
{
    public class SubjectMaterialVM
    {
        public int ID { get; set; }
        public int PredmetId { get; set; }

        public string PredmetNaziv { get; set; } = "";

        public string Naslov { get; set; } = "";
        public string? Opis { get; set; }

        public string TipMaterijala { get; set; } = "";
        public string? OriginalFileName { get; set; }
        public string? FilePath { get; set; }
        public string? Url { get; set; }

        public int? Redoslijed { get; set; }
        public string Aktivno { get; set; } = "Y";

        public DateTime DatumKreiranja { get; set; }
    }
}