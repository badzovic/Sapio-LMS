using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("DODIJELJENI_TESTOVI")]
    public class DodijeljeniTest
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("TEST_ID")]
        public int TEST_ID { get; set; }

        [Column("KORISNIK_ID")]
        public int KORISNIK_ID { get; set; }

        [Column("DODIJELIO_USER_ID")]
        public string? DODIJELIO_USER_ID { get; set; }

        [Column("DATUM_DODJELE")]
        public DateTime DATUM_DODJELE { get; set; }

        [Column("AKTIVNO")]
        public string AKTIVNO { get; set; } = "Y";

        [Column("NAPOMENA")]
        public string? NAPOMENA { get; set; }

        [ForeignKey(nameof(TEST_ID))]
        public Test Test { get; set; }

        [ForeignKey(nameof(KORISNIK_ID))]
        public Korisnik Korisnik { get; set; }
    }
}