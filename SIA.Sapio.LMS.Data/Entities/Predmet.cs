using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("PREDMETI")]
    public class Predmet
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("V3_ID_OBUKE")]
        public int? V3_ID_OBUKE { get; set; }

        [Column("OZNAKA")]
        public string? OZNAKA { get; set; }

        [Column("NAZIV")]
        public string NAZIV { get; set; } = "";

        [Column("NAZIV_ENG")]
        public string? NAZIV_ENG { get; set; }

        [Column("V3_ID_VRSTA")]
        public int? V3_ID_VRSTA { get; set; }

        [Column("VRSTA_PROGRAMA_ID")]
        public int? VRSTA_PROGRAMA_ID { get; set; }

        [Column("XRAY_ALLOW")]
        public int? XRAY_ALLOW { get; set; }

        [Column("POINT_SIFRA")]
        public int? POINT_SIFRA { get; set; }

        [Column("AKTIVNO")]
        public string AKTIVNO { get; set; } = "Y";

        [Column("DATUM_KREIRANJA")]
        public DateTime DATUM_KREIRANJA { get; set; }

        [Column("DATUM_IZMJENE")]
        public DateTime? DATUM_IZMJENE { get; set; }

        [Column("DATUM_ZADNJEG_SYNCA")]
        public DateTime? DATUM_ZADNJEG_SYNCA { get; set; }

        [Column("PRAKTICNA_OBUKA")]
        public string? PRAKTICNA_OBUKA { get; set; }

        // NAVIGATION
        [ForeignKey("VRSTA_PROGRAMA_ID")]
        public VrstaPrograma? VrstaPrograma { get; set; }
    }
}