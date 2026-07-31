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

        [Column("ROK_ZA_POLAGANJE")]
        public DateTime? ROK_ZA_POLAGANJE { get; set; }

        [Column("STATUS")]
        public string? STATUS { get; set; }

        [Column("AUTO_DODJELA")]
        public string AUTO_DODJELA { get; set; } = "N";

        [Column("RAZLOG_DODJELE")]
        public string? RAZLOG_DODJELE { get; set; }

        [Column("TEST_KLASA_ID")]
        public int? TEST_KLASA_ID { get; set; }

        [Column("DATUM_IZMJENE")]
        public DateTime? DATUM_IZMJENE { get; set; }

        [Column("AKTIVNO")]
        public string AKTIVNO { get; set; } = "Y";

        [Column("NAPOMENA")]
        public string? NAPOMENA { get; set; }

        [ForeignKey(nameof(TEST_ID))]
        public Test TEST { get; set; }

        [ForeignKey(nameof(KORISNIK_ID))]
        public Korisnik KORISNIK { get; set; }

        [ForeignKey(nameof(TEST_KLASA_ID))]
        public TestKlasa? TEST_KLASA { get; set; }
    }
}