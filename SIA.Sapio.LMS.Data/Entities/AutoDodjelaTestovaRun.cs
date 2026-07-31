using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("AUTO_DODJELA_TESTOVA_RUN")]
    public class AutoDodjelaTestovaRun
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("DATUM_POKRETANJA")]
        public DateTime DATUM_POKRETANJA { get; set; }

        [Column("DATUM_ZAVRSETKA")]
        public DateTime? DATUM_ZAVRSETKA { get; set; }

        [Column("TIP_POKRETANJA")]
        public string TIP_POKRETANJA { get; set; } = "";

        [Column("POKRENUTO_OD_USER_ID")]
        public string? POKRENUTO_OD_USER_ID { get; set; }

        [Column("PREDMET_ID")]
        public int? PREDMET_ID { get; set; }

        [Column("STATUS")]
        public string STATUS { get; set; } = "U_TOKU";

        [Column("UKUPNO_PREDMETA")]
        public int UKUPNO_PREDMETA { get; set; }

        [Column("UKUPNO_KORISNIKA_U_MATRICI")]
        public int UKUPNO_KORISNIKA_U_MATRICI { get; set; }

        [Column("UKUPNO_DODIJELJENO")]
        public int UKUPNO_DODIJELJENO { get; set; }

        [Column("UKUPNO_STICANJE")]
        public int UKUPNO_STICANJE { get; set; }

        [Column("UKUPNO_OBNOVA")]
        public int UKUPNO_OBNOVA { get; set; }

        [Column("UKUPNO_NEMA_VALIDNOG_TESTA")]
        public int UKUPNO_NEMA_VALIDNOG_TESTA { get; set; }

        [Column("UKUPNO_VEC_DODIJELJENO")]
        public int UKUPNO_VEC_DODIJELJENO { get; set; }

        [Column("UKUPNO_GRESAKA")]
        public int UKUPNO_GRESAKA { get; set; }

        [Column("NAPOMENA")]
        public string? NAPOMENA { get; set; }

        [ForeignKey(nameof(PREDMET_ID))]
        public Predmet? PREDMET { get; set; }

        public ICollection<AutoDodjelaTestovaLog> LOGOVI { get; set; } = new List<AutoDodjelaTestovaLog>();
    }
}