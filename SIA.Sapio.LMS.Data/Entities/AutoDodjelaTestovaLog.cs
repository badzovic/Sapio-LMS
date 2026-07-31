using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("AUTO_DODJELA_TESTOVA_LOG")]
    public class AutoDodjelaTestovaLog
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("RUN_ID")]
        public int RUN_ID { get; set; }

        [Column("PREDMET_ID")]
        public int? PREDMET_ID { get; set; }

        [Column("KORISNIK_ID")]
        public int? KORISNIK_ID { get; set; }

        [Column("TEST_ID")]
        public int? TEST_ID { get; set; }

        [Column("TEST_KLASA_ID")]
        public int? TEST_KLASA_ID { get; set; }

        [Column("TIP_DOGADJAJA")]
        public string TIP_DOGADJAJA { get; set; } = "";

        [Column("RAZLOG")]
        public string? RAZLOG { get; set; }

        [Column("POGODJENO_KORISNIKA")]
        public int? POGODJENO_KORISNIKA { get; set; }

        [Column("PORUKA")]
        public string? PORUKA { get; set; }

        [Column("DATUM_KREIRANJA")]
        public DateTime DATUM_KREIRANJA { get; set; }

        [ForeignKey(nameof(RUN_ID))]
        public AutoDodjelaTestovaRun RUN { get; set; }

        [ForeignKey(nameof(PREDMET_ID))]
        public Predmet? PREDMET { get; set; }

        [ForeignKey(nameof(KORISNIK_ID))]
        public Korisnik? KORISNIK { get; set; }

        [ForeignKey(nameof(TEST_ID))]
        public Test? TEST { get; set; }

        [ForeignKey(nameof(TEST_KLASA_ID))]
        public TestKlasa? TEST_KLASA { get; set; }
    }
}