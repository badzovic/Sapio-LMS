using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("STUDENT_ODGOVORI")]
    public class StudentOdgovor
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("POLAGANJE_TESTA_ID")]
        public int POLAGANJE_TESTA_ID { get; set; }

        [Column("POLAGANJE_PITANJE_ID")]
        public int POLAGANJE_PITANJE_ID { get; set; }

        [Column("TEST_PITANJE_ODGOVOR_ID")]
        public int? TEST_PITANJE_ODGOVOR_ID { get; set; }

        [Column("ESSAY_ODGOVOR")]
        public string? ESSAY_ODGOVOR { get; set; }

        [Column("IS_TACAN")]
        public string? IS_TACAN { get; set; }

        [Column("OSTVARENI_BODOVI")]
        public decimal? OSTVARENI_BODOVI { get; set; }

        [Column("DATUM_ODGOVORA")]
        public DateTime DATUM_ODGOVORA { get; set; }

        [ForeignKey(nameof(POLAGANJE_TESTA_ID))]
        public PolaganjeTesta PolaganjeTesta { get; set; }

        [ForeignKey(nameof(POLAGANJE_PITANJE_ID))]
        public PolaganjePitanja PolaganjePitanje { get; set; }

        [ForeignKey(nameof(TEST_PITANJE_ODGOVOR_ID))]
        public TestPitanjeOdgovor? TestPitanjeOdgovor { get; set; }
    }
}