using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("PREDMET_PITANJE_ODGOVORI")]
    public class PredmetPitanjeOdgovor
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("PITANJE_ID")]
        public int PITANJE_ID { get; set; }

        [Column("TEKST_ODGOVORA")]
        public string TEKST_ODGOVORA { get; set; } = "";

        [Column("IS_TACAN")]
        public string IS_TACAN { get; set; } = "N";

        [Column("REDOSLIJED")]
        public int? REDOSLIJED { get; set; }

        [Column("AKTIVNO")]
        public string AKTIVNO { get; set; } = "Y";

        [Column("DATUM_KREIRANJA")]
        public DateTime DATUM_KREIRANJA { get; set; }

        [Column("DATUM_IZMJENE")]
        public DateTime? DATUM_IZMJENE { get; set; }

        // NAVIGATION

        [ForeignKey("PITANJE_ID")]
        public PredmetPitanje? PredmetPitanje { get; set; }

        [Column("BODOVI")]
        public decimal? BODOVI { get; set; }

        [Column("NEGATIVNI_BODOVI")]
        public decimal? NEGATIVNI_BODOVI { get; set; }
    }
}