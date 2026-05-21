using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("POLAGANJE_ODGOVORI")]
    public class PolaganjeOdgovori
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("POLAGANJE_PITANJE_ID")]
        public int POLAGANJE_PITANJE_ID { get; set; }

        [Column("TEST_PITANJE_ODGOVOR_ID")]
        public int TEST_PITANJE_ODGOVOR_ID { get; set; }

        [Column("REDOSLIJED")]
        public int REDOSLIJED { get; set; }

        [ForeignKey(nameof(POLAGANJE_PITANJE_ID))]
        public PolaganjePitanja? PolaganjePitanje { get; set; }

        [ForeignKey(nameof(TEST_PITANJE_ODGOVOR_ID))]
        public TestPitanjeOdgovor? TestPitanjeOdgovor { get; set; }
    }
}