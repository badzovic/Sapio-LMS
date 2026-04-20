using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("TIPOVI_KORISNIKA")]
    public class TipKorisnika
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("NAZIV")]
        public string? Naziv { get; set; }

        [Column("AKTIVNO")]
        public string? Aktivno { get; set; }
    }
}