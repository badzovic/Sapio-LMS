using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("PREDMET_INSTRUKTORI")]
    public class PredmetInstruktor
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("PREDMET_ID")]
        public int PREDMET_ID { get; set; }

        [Column("KORISNIK_ID")]
        public int KORISNIK_ID { get; set; }

        [Column("AKTIVNO")]
        public string AKTIVNO { get; set; } = "Y";

        [Column("DATUM_OD")]
        public DateTime DATUM_OD { get; set; }

        [Column("DATUM_DO")]
        public DateTime? DATUM_DO { get; set; }

        [Column("DATUM_KREIRANJA")]
        public DateTime DATUM_KREIRANJA { get; set; }

        [Column("DATUM_IZMJENE")]
        public DateTime? DATUM_IZMJENE { get; set; }

        [ForeignKey("PREDMET_ID")]
        public Predmet? Predmet { get; set; }

        [ForeignKey("KORISNIK_ID")]
        public Korisnik? Korisnik { get; set; }
    }
}