using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("EVIDENCIJA_DEAKTIVACIJE_KORISNIKA")]
    public class EvidencijaDeaktivacijeKorisnika
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("KORISNIK_ID")]
        public int KORISNIK_ID { get; set; }

        [Column("USER_ID")]
        public string? USER_ID { get; set; }

        [Column("HRM_ID")]
        public int? HRM_ID { get; set; }

        [Column("IME")]
        public string? IME { get; set; }

        [Column("PREZIME")]
        public string? PREZIME { get; set; }

        [Column("EMAIL")]
        public string? EMAIL { get; set; }

        [Column("ACCESS_KEY")]
        public int? ACCESS_KEY { get; set; }

        [Column("STARI_AKTIVAN")]
        public bool? STARI_AKTIVAN { get; set; }

        [Column("NOVI_AKTIVAN")]
        public bool? NOVI_AKTIVAN { get; set; }

        [Column("STARI_IDENTITY_AKTIVAN")]
        public bool? STARI_IDENTITY_AKTIVAN { get; set; }

        [Column("NOVI_IDENTITY_AKTIVAN")]
        public bool? NOVI_IDENTITY_AKTIVAN { get; set; }

        [Column("RAZLOG_DEAKTIVACIJE")]
        public string? RAZLOG_DEAKTIVACIJE { get; set; }

        [Column("OPIS")]
        public string? OPIS { get; set; }

        [Column("DATUM_KREIRANJA")]
        public DateTime DATUM_KREIRANJA { get; set; }

        [Column("PREGLEDANO")]
        public bool PREGLEDANO { get; set; }

        [Column("PREGLEDAO_USER_ID")]
        public string? PREGLEDAO_USER_ID { get; set; }

        [Column("DATUM_PREGLEDA")]
        public DateTime? DATUM_PREGLEDA { get; set; }

        [ForeignKey(nameof(KORISNIK_ID))]
        public Korisnik? KORISNIK { get; set; }
    }
}