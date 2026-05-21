using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("VRSTE_PROGRAMA")]
    public class VrstaPrograma
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("V3_ID_VRSTA")]
        public int? V3_ID_VRSTA { get; set; }

        [Column("NAZIV")]
        public string NAZIV { get; set; } = "";

        [Column("AKTIVNO")]
        public string AKTIVNO { get; set; } = "AKTIVNO";

        [Column("DATUM_KREIRANJA")]
        public DateTime DATUM_KREIRANJA { get; set; }

        [Column("DATUM_IZMJENE")]
        public DateTime? DATUM_IZMJENE { get; set; }

        [Column("DATUM_ZADNJEG_SYNCA")]
        public DateTime? DATUM_ZADNJEG_SYNCA { get; set; }
    }
}