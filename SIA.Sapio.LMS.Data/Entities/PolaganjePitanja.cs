using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("POLAGANJE_PITANJA")]
    public class PolaganjePitanja
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("POLAGANJE_TESTA_ID")]
        public int POLAGANJE_TESTA_ID { get; set; }

        [Column("TEST_PITANJE_ID")]
        public int TEST_PITANJE_ID { get; set; }

        [Column("REDOSLIJED")]
        public int REDOSLIJED { get; set; }

        [Column("DATUM_KREIRANJA")]
        public DateTime DATUM_KREIRANJA { get; set; }

        [ForeignKey(nameof(POLAGANJE_TESTA_ID))]
        public PolaganjeTesta? PolaganjeTesta { get; set; }

        [ForeignKey(nameof(TEST_PITANJE_ID))]
        public TestPitanje? TestPitanje { get; set; }

        public ICollection<PolaganjeOdgovori> Odgovori { get; set; } = new List<PolaganjeOdgovori>();
    }
}