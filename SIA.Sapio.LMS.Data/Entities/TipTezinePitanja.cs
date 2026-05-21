using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("TIPOVI_TEZINE_PITANJA")]
    public class TipTezinePitanja
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("NAZIV")]
        public string NAZIV { get; set; } = "";

        [Column("SIFRA")]
        public string SIFRA { get; set; } = "";

        [Column("REDOSLIJED")]
        public int REDOSLIJED { get; set; }

        [Column("AKTIVNO")]
        public string AKTIVNO { get; set; } = "Y";

        [Column("DATUM_KREIRANJA")]
        public DateTime DATUM_KREIRANJA { get; set; }
    }
}