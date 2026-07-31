using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("PREDMET_PITANJA")]
    public class PredmetPitanje
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("PREDMET_ID")]
        public int PREDMET_ID { get; set; }

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

        [Column("AKTIVNO")]
        public string AKTIVNO { get; set; } = "Y";

        [Column("CREATED_BY_USER_ID")]
        public string? CREATED_BY_USER_ID { get; set; }

        [Column("DATUM_KREIRANJA")]
        public DateTime DATUM_KREIRANJA { get; set; }

        [Column("DATUM_IZMJENE")]
        public DateTime? DATUM_IZMJENE { get; set; }

        [Column("SLIKA_PUTANJA")]
        public string? SLIKA_PUTANJA { get; set; }

        [Column("SLIKA_NAZIV")]
        public string? SLIKA_NAZIV { get; set; }

        // NAVIGATION

        [ForeignKey("PREDMET_ID")]
        public Predmet? Predmet { get; set; }

        [ForeignKey("TIP_PITANJA_ID")]
        public TipPitanja? TipPitanja { get; set; }

        [ForeignKey("TIP_TEZINE_ID")]
        public TipTezinePitanja? TipTezinePitanja { get; set; }
    }
}