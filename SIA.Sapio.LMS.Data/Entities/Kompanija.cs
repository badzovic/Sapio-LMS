using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("KOMPANIJE")]
    public class Kompanija
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("NAZIV")]
        public string NAZIV { get; set; }

        [Column("NAZIV_NORMALIZED")]
        public string NAZIV_NORMALIZED { get; set; }

        [Column("AKTIVNA")]
        public string AKTIVNA { get; set; } = "Y";

        [Column("DATUM_KREIRANJA")]
        public DateTime DATUM_KREIRANJA { get; set; }

        [Column("NAPOMENA")]
        public string? NAPOMENA { get; set; }
    }
}