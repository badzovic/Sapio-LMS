using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("TEST_PITANJE_ODGOVORI")]
    public class TestPitanjeOdgovor
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("TEST_PITANJE_ID")]
        public int TEST_PITANJE_ID { get; set; }

        [Column("ORIGINAL_ODGOVOR_ID")]
        public int? ORIGINAL_ODGOVOR_ID { get; set; }

        [Column("TEKST_ODGOVORA")]
        public string TEKST_ODGOVORA { get; set; } = "";

        [Column("IS_TACAN")]
        public string IS_TACAN { get; set; } = "N";

        [Column("BODOVI")]
        public decimal? BODOVI { get; set; }

        [Column("NEGATIVNI_BODOVI")]
        public decimal? NEGATIVNI_BODOVI { get; set; }

        [Column("REDOSLIJED")]
        public int REDOSLIJED { get; set; }

        [Column("DATUM_KREIRANJA")]
        public DateTime DATUM_KREIRANJA { get; set; }

        [ForeignKey("TEST_PITANJE_ID")]
        public TestPitanje? TestPitanje { get; set; }

        [ForeignKey("ORIGINAL_ODGOVOR_ID")]
        public PredmetPitanjeOdgovor? OriginalOdgovor { get; set; }
    }
}