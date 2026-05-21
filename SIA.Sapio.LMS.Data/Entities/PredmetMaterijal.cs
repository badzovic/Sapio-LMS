using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("PREDMET_MATERIJALI")]
    public class PredmetMaterijal
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("PREDMET_ID")]
        public int PREDMET_ID { get; set; }

        [Column("NASLOV")]
        public string NASLOV { get; set; } = "";

        [Column("OPIS")]
        public string? OPIS { get; set; }

        [Column("TIP_MATERIJALA")]
        public string TIP_MATERIJALA { get; set; } = "";

        [Column("FILE_NAME")]
        public string? FILE_NAME { get; set; }

        [Column("ORIGINAL_FILE_NAME")]
        public string? ORIGINAL_FILE_NAME { get; set; }

        [Column("FILE_PATH")]
        public string? FILE_PATH { get; set; }

        [Column("CONTENT_TYPE")]
        public string? CONTENT_TYPE { get; set; }

        [Column("FILE_SIZE_BYTES")]
        public long? FILE_SIZE_BYTES { get; set; }

        [Column("URL")]
        public string? URL { get; set; }

        [Column("REDOSLIJED")]
        public int? REDOSLIJED { get; set; }

        [Column("AKTIVNO")]
        public string AKTIVNO { get; set; } = "Y";

        [Column("CREATED_BY_USER_ID")]
        public string? CREATED_BY_USER_ID { get; set; }

        [Column("DATUM_KREIRANJA")]
        public DateTime DATUM_KREIRANJA { get; set; }

        [Column("DATUM_IZMJENE")]
        public DateTime? DATUM_IZMJENE { get; set; }

        [ForeignKey("PREDMET_ID")]
        public Predmet? Predmet { get; set; }
    }
}