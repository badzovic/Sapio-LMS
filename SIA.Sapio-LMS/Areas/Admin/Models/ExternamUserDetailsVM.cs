namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class ExternalUserDetailsVM
    {
        public int ID { get; set; }

        public int? ExternalV3Id { get; set; }

        public string? UserId { get; set; }

        public string? Ime { get; set; }

        public string? Prezime { get; set; }

        public string? ImeOca { get; set; }

        public string? MjestoRodjenja { get; set; }

        public string? Email { get; set; }

        public string? Kompanija { get; set; }

        public int? KompanijaId { get; set; }

        public int? TipKorisnikaId { get; set; }

        public string? Aktivan { get; set; }
    }
}