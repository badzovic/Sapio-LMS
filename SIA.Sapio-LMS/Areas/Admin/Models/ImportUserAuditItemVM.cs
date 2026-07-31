namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class ImportUserAuditItemVM
    {
        public int Id { get; set; }

        public int? KorisnikId { get; set; }

        public string? Ime { get; set; }

        public string? Prezime { get; set; }

        public int? HrmId { get; set; }

        public string? StariTipKorisnika { get; set; }

        public string? NoviTipKorisnika { get; set; }

        public DateTime DatumPromjene { get; set; }

        public string? PokrenuoUserId { get; set; }

        public string? PregledaoUserId { get; set; }

        public DateTime? DatumPregleda { get; set; }

        public bool JePregledano =>
            !string.IsNullOrWhiteSpace(PregledaoUserId) &&
            DatumPregleda.HasValue;

        public string? Pregledao { get; set; }
    }
}