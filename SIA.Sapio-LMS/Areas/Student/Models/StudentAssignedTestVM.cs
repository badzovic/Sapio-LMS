
namespace SIA.Sapio_LMS.Areas.Student.Models
{
    public class StudentAssignedTestVM
    {
        // ID konkretne dodjele testa
        public int DodjelaId { get; set; }

        public int TestId { get; set; }

        public string NazivTesta { get; set; } = string.Empty;
        public string? Opis { get; set; }
        public string? Predmet { get; set; }

        public DateTime? DatumOd { get; set; }
        public DateTime? DatumDo { get; set; }

        // Individualni rok dodijeljen korisniku
        public DateTime? RokZaPolaganje { get; set; }

        public int VrijemeTrajanjaMin { get; set; }

        public int BrojPokusajaDozvoljeno { get; set; }
        public int BrojPokusajaIskoristeno { get; set; }

        public int BrojPokusajaPreostalo =>
            BrojPokusajaDozvoljeno - BrojPokusajaIskoristeno;

        // Da li student može pokrenuti polaganje
        public bool MozePolagati { get; set; }

        // Status za prikaz u tabeli
        public string StatusDostupnosti { get; set; } = string.Empty;

        // Objašnjenje ako polaganje nije omogućeno
        public string? RazlogNedostupnosti { get; set; }
    }
}
