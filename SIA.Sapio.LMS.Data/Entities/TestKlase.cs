using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("TEST_KLASE")]
    public class TestKlasa
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("NAZIV")]
        public string NAZIV { get; set; }

        [Column("SIFRA")]
        public string SIFRA { get; set; }

        [Column("AKTIVNO")]
        public string AKTIVNO { get; set; } = "Y";

        [Column("DATUM_KREIRANJA")]
        public DateTime DATUM_KREIRANJA { get; set; }
    }
}