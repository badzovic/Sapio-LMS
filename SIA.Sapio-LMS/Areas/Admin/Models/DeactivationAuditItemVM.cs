namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class DeactivationAuditItemVM
    {
        public int Id { get; set; }

        public int KorisnikId { get; set; }

        public int? HrmId { get; set; }

        public string? Ime { get; set; }

        public string? Prezime { get; set; }

        public string? Email { get; set; }

        public int? AccessKey { get; set; }

        public string? RazlogDeaktivacije { get; set; }

        public string? Opis { get; set; }

        public DateTime DatumKreiranja { get; set; }

        public bool Pregledano { get; set; }

        public string? Pregledao { get; set; }

        public DateTime? DatumPregleda { get; set; }
    }
}