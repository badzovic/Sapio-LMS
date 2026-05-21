namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class PredmetVM
    {
        public int ID { get; set; }

        public int? V3IdObuke { get; set; }

        public string? Oznaka { get; set; }
        public string Naziv { get; set; } = "";
        public string? NazivEng { get; set; }

        public int? V3IdVrsta { get; set; }
        public string? VrstaNaziv { get; set; }

        public int? XrayAllow { get; set; }
        public int? PointSifra { get; set; }

        public string Aktivno { get; set; } = "Y";

        public string? PrakticnaObuka { get; set; }

        public DateTime DatumKreiranja { get; set; }

        public List<PredmetInstruktorVM> Instruktori { get; set; } = new();
        public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> DostupniInstruktori { get; set; } = new();
        public int? NoviInstruktorKorisnikId { get; set; }
        public List<PredmetInstruktorVM> HistorijaInstruktora { get; set; } = new();
    }
}