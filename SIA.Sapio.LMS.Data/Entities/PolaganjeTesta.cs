using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("POLAGANJE_TESTA")]
    public class PolaganjeTesta
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("TEST_ID")]
        public int TEST_ID { get; set; }

        [Column("KORISNIK_ID")]
        public int KORISNIK_ID { get; set; }

        [Column("DATUM_POCETKA")]
        public DateTime DATUM_POCETKA { get; set; }

        [Column("DATUM_ZAVRSETKA")]
        public DateTime? DATUM_ZAVRSETKA { get; set; }

        [Column("VRIJEME_ISTEKA")]
        public DateTime VRIJEME_ISTEKA { get; set; }

        [Column("STATUS")]
        public string STATUS { get; set; } = "IN_PROGRESS";

        [Column("REDNI_BROJ_POKUSAJA")]
        public int REDNI_BROJ_POKUSAJA { get; set; }

        [Column("OSTVARENI_BODOVI")]
        public decimal? OSTVARENI_BODOVI { get; set; }

        [Column("MAX_BODOVI")]
        public decimal? MAX_BODOVI { get; set; }

        [Column("PROCENAT")]
        public decimal? PROCENAT { get; set; }

        [Column("IS_PASSED")]
        public string? IS_PASSED { get; set; }

        [Column("AUTO_SUBMITTED")]
        public string AUTO_SUBMITTED { get; set; } = "N";

        [Column("DATUM_KREIRANJA")]
        public DateTime DATUM_KREIRANJA { get; set; }
        [Column("DATUM_IZMJENE")]
        public DateTime? DATUM_IZMJENE { get; set; }

        [ForeignKey(nameof(TEST_ID))]
        public Test? Test { get; set; }

        [ForeignKey(nameof(KORISNIK_ID))]
        public Korisnik? Korisnik { get; set; }        

        public ICollection<PolaganjePitanja> Pitanja { get; set; } = new List<PolaganjePitanja>();

        public ICollection<StudentOdgovor> StudentOdgovori { get; set; } = new List<StudentOdgovor>();
    }
}