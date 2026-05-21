using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("TESTOVI")]
    public class Test
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("PREDMET_ID")]
        public int PREDMET_ID { get; set; }

        [Column("NAZIV")]
        public string NAZIV { get; set; } = "";

        [Column("OPIS")]
        public string? OPIS { get; set; }

        [Column("DATUM_OD")]
        public DateTime DATUM_OD { get; set; }

        [Column("DATUM_DO")]
        public DateTime DATUM_DO { get; set; }

        [Column("VRIJEME_TRAJANJA_MIN")]
        public int VRIJEME_TRAJANJA_MIN { get; set; }

        [Column("BROJ_PITANJA")]
        public int BROJ_PITANJA { get; set; }

        [Column("BROJ_LAKIH")]
        public int BROJ_LAKIH { get; set; }

        [Column("BROJ_SREDNJIH")]
        public int BROJ_SREDNJIH { get; set; }

        [Column("BROJ_TESKIH")]
        public int BROJ_TESKIH { get; set; }

        [Column("MIN_PROCENAT_PROLAZA")]
        public decimal MIN_PROCENAT_PROLAZA { get; set; }

        [Column("BROJ_POKUSAJA")]
        public int BROJ_POKUSAJA { get; set; }

        [Column("RANDOM_PITANJA")]
        public string RANDOM_PITANJA { get; set; } = "Y";

        [Column("RANDOM_ODGOVORI")]
        public string RANDOM_ODGOVORI { get; set; } = "Y";

        [Column("STATUS")]
        public string STATUS { get; set; } = "DRAFT";

        [Column("AKTIVNO")]
        public string AKTIVNO { get; set; } = "Y";

        [Column("CREATED_BY_USER_ID")]
        public string? CREATED_BY_USER_ID { get; set; }

        [Column("DATUM_KREIRANJA")]
        public DateTime DATUM_KREIRANJA { get; set; }

        [Column("DATUM_IZMJENE")]
        public DateTime? DATUM_IZMJENE { get; set; }

        [ForeignKey("PREDMET_ID")]
        public Predmet? Predmet { get; set; }

        [Column("TEST_KLASA_ID")]
        public int TEST_KLASA_ID { get; set; }

        [ForeignKey(nameof(TEST_KLASA_ID))]
        public TestKlasa? TEST_KLASA { get; set; }
    }
}