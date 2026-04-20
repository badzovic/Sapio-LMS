using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("KORISNICI")]
    public class Korisnik
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("IME")]
        public string? IME { get; set; }

        [Column("PREZIME")]
        public string? PREZIME { get; set; }

        [Column("ACCESS_KEY")]
        public int? ACCESS_KEY { get; set; }

        [Column("USER_ID")]
        public string? USER_ID { get; set; }

        [Column("EMAIL")]
        public string? EMAIL { get; set; }

        [Column("KOMPANIJA")]
        public string? KOMPANIJA { get; set; }

        [Column("HRM_ID")]
        public int? HRM_ID { get; set; }
        [Column("AKTIVAN")]
        public string? AKTIVAN { get; set; }

        [Column("IME_OCA")]
        public string? IME_OCA { get; set; }
        [Column("KOMPANIJA_ID")]
        public int? KOMPANIJA_ID { get; set; }

        [Column("TIP_KORISNIKA_ID")]
        public int? TIP_KORISNIKA_ID { get; set; }

        [Column("EXTERNI_V3_ID")]
        public int? EXTERNI_V3_ID { get; set; }

        [Column("MJESTO_RODJENJA")]
        public string? MJESTO_RODJENJA { get; set; }

        [ForeignKey("KOMPANIJA_ID")]
        public Kompanija? Kompanija { get; set; }
    }
}