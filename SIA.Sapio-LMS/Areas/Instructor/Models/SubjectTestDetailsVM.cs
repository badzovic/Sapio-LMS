namespace SIA.Sapio_LMS.Areas.Instructor.Models
{
    public class SubjectTestDetailsVM
    {
        public int ID { get; set; }
        public int PredmetId { get; set; }

        public string PredmetNaziv { get; set; } = "";
        public string Naziv { get; set; } = "";
        public string? Opis { get; set; }

        public DateTime DatumOd { get; set; }
        public DateTime DatumDo { get; set; }

        public int VrijemeTrajanjaMin { get; set; }
        public int BrojPitanja { get; set; }

        public int BrojLakih { get; set; }
        public int BrojSrednjih { get; set; }
        public int BrojTeskih { get; set; }

        public decimal MinProcenatProlaza { get; set; }
        public int BrojPokusaja { get; set; }

        public string RandomPitanja { get; set; } = "Y";
        public string RandomOdgovori { get; set; } = "Y";

        public string Status { get; set; } = "DRAFT";
        public string Aktivno { get; set; } = "Y";

        public DateTime DatumKreiranja { get; set; }
        public DateTime? DatumIzmjene { get; set; }

        public List<SubjectTestQuestionDetailsVM> Pitanja { get; set; } = new();
        public int? TestKlasaId { get; set; }
        public string? TestKlasaNaziv { get; set; }
    }
}