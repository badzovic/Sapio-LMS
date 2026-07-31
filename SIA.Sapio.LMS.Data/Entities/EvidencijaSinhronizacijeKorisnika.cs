using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIA.Sapio.LMS.Data.Entities
{
    [Table("EVIDENCIJA_SINHRONIZACIJE_KORISNIKA")]
    public class EvidencijaSinhronizacijeKorisnika
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }

        [Column("KORISNIK_ID")]
        public int KORISNIK_ID { get; set; }

        [Column("USER_ID")]
        public string? USER_ID { get; set; }

        [Column("HRM_ID")]
        public int HRM_ID { get; set; }

        [Column("STARO_IME")]
        public string? STARO_IME { get; set; }

        [Column("NOVO_IME")]
        public string? NOVO_IME { get; set; }

        [Column("STARO_PREZIME")]
        public string? STARO_PREZIME { get; set; }

        [Column("NOVO_PREZIME")]
        public string? NOVO_PREZIME { get; set; }

        [Column("STARO_IME_OCA")]
        public string? STARO_IME_OCA { get; set; }

        [Column("NOVO_IME_OCA")]
        public string? NOVO_IME_OCA { get; set; }

        [Column("STARI_EMAIL")]
        public string? STARI_EMAIL { get; set; }

        [Column("NOVI_EMAIL")]
        public string? NOVI_EMAIL { get; set; }

        [Column("STARI_ACCESS_KEY")]
        public int? STARI_ACCESS_KEY { get; set; }

        [Column("NOVI_ACCESS_KEY")]
        public int? NOVI_ACCESS_KEY { get; set; }

        [Column("PROMIJENJENA_POLJA")]
        public string PROMIJENJENA_POLJA { get; set; } = string.Empty;

        [Column("OPIS")]
        public string? OPIS { get; set; }

        [Column("DATUM_KREIRANJA")]
        public DateTime DATUM_KREIRANJA { get; set; }

        [Column("PREGLEDANO")]
        public bool PREGLEDANO { get; set; }

        [Column("PREGLEDAO_USER_ID")]
        public string? PREGLEDAO_USER_ID { get; set; }

        [Column("DATUM_PREGLEDA")]
        public DateTime? DATUM_PREGLEDA { get; set; }

        [ForeignKey(nameof(KORISNIK_ID))]
        public Korisnik KORISNIK { get; set; }
    }
}