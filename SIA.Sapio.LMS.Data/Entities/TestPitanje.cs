using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("TEST_PITANJA")]
    public class TestPitanje
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("TEST_ID")]
        public int TEST_ID { get; set; }

        [Column("ORIGINAL_PITANJE_ID")]
        public int? ORIGINAL_PITANJE_ID { get; set; }

        [Column("TIP_PITANJA_ID")]
        public int TIP_PITANJA_ID { get; set; }

        [Column("TIP_TEZINE_ID")]
        public int TIP_TEZINE_ID { get; set; }

        [Column("TEKST_PITANJA")]
        public string TEKST_PITANJA { get; set; } = "";

        [Column("BODOVI")]
        public decimal BODOVI { get; set; }

        [Column("NEGATIVNI_BODOVI")]
        public decimal? NEGATIVNI_BODOVI { get; set; }

        [Column("PARCIJALNO_BODOVANJE")]
        public string PARCIJALNO_BODOVANJE { get; set; } = "N";

        [Column("REDOSLIJED")]
        public int REDOSLIJED { get; set; }

        [Column("DATUM_KREIRANJA")]
        public DateTime DATUM_KREIRANJA { get; set; }

        [ForeignKey("TEST_ID")]
        public Test? Test { get; set; }

        [ForeignKey("ORIGINAL_PITANJE_ID")]
        public PredmetPitanje? OriginalPitanje { get; set; }

        [ForeignKey("TIP_PITANJA_ID")]
        public TipPitanja? TipPitanja { get; set; }

        [ForeignKey("TIP_TEZINE_ID")]
        public TipTezinePitanja? TipTezinePitanja { get; set; }

        [Column("IS_OBAVEZNO")]
        public string IS_OBAVEZNO { get; set; } = "N";

        [Column("SLIKA_PUTANJA")]
        public string? SLIKA_PUTANJA { get; set; }

        [Column("SLIKA_NAZIV")]
        public string? SLIKA_NAZIV { get; set; }
    }
}