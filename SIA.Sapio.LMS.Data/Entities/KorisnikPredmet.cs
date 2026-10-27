
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("KORISNIK_PREDMETI")]
    public class KorisnikPredmet
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("KORISNIK_ID")]
        public int KORISNIK_ID { get; set; }

        [Column("PREDMET_ID")]
        public int PREDMET_ID { get; set; }

        [Column("DATUM_DODJELE")]
        public DateTime DATUM_DODJELE { get; set; }

        [Column("DODIJELIO_USER_ID")]
        public string? DODIJELIO_USER_ID { get; set; }

        [Column("AKTIVNO")]
        public string AKTIVNO { get; set; } = "Y";

        [Column("DATUM_DEAKTIVACIJE")]
        public DateTime? DATUM_DEAKTIVACIJE { get; set; }

        [Column("DEAKTIVIRAO_USER_ID")]
        public string? DEAKTIVIRAO_USER_ID { get; set; }

        [Column("NAPOMENA")]
        public string? NAPOMENA { get; set; }

        // Navigacije
        [ForeignKey(nameof(KORISNIK_ID))]
        public Korisnik? KORISNIK { get; set; }

        [ForeignKey(nameof(PREDMET_ID))]
        public Predmet? PREDMET { get; set; }
    }
}
