namespace SIA.Sapio_LMS.Areas.Student.Models
{
    public class StudentAssignedTestVM
    {
        public int TestId { get; set; }
        public string NazivTesta { get; set; } = string.Empty;
        public string? Opis { get; set; }
        public string? Predmet { get; set; }

        public DateTime? DatumOd { get; set; }
        public DateTime? DatumDo { get; set; }

        public int VrijemeTrajanjaMin { get; set; }
        public int BrojPokusajaDozvoljeno { get; set; }
        public int BrojPokusajaIskoristeno { get; set; }

        public int BrojPokusajaPreostalo =>
            BrojPokusajaDozvoljeno - BrojPokusajaIskoristeno;

        public bool MozePolagati { get; set; }
    }
}