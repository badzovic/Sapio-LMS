
namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class AssignedTestQuestionVM
    {
        public int ID { get; set; }
        public int TestPitanjeId { get; set; }

        public int Redoslijed { get; set; }

        public string TekstPitanja { get; set; } = "";
        public string TipPitanja { get; set; } = "";
        public string TezinaPitanja { get; set; } = "";

        public decimal Bodovi { get; set; }
        public decimal? NegativniBodovi { get; set; }

        public bool ParcijalnoBodovanje { get; set; }
        public bool Obavezno { get; set; }

        public string? SlikaPutanja { get; set; }
        public string? SlikaNaziv { get; set; }

        // Studentski odgovor
        public string? EsejskiOdgovor { get; set; }
        public decimal? OstvareniBodovi { get; set; }
        public bool? IsTacan { get; set; }

        // Ponuđene opcije konkretnog pokušaja
        public List<AssignedTestOptionVM> Odgovori { get; set; } = new();
    }
}
