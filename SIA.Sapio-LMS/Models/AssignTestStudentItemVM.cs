namespace SIA.Sapio_LMS.Areas.Models
{
    public class AssignTestStudentItemVM
    {
        public int KorisnikId { get; set; }

        public string ImePrezime { get; set; } = "";

        public string? Email { get; set; }

        public string? Kompanija { get; set; }

        public bool VecDodijeljen { get; set; }

        public bool Odabran { get; set; }

        public DateTime? DatumVaziDo { get; set; }

        public int? DanaDoIsteka { get; set; }

        public string StatusObuke { get; set; } = "";
    }
}