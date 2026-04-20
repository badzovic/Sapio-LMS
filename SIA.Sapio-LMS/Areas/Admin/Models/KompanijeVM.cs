namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class CompaniesVM
    {
        public int ID { get; set; }

        public string? Naziv { get; set; }

        public string? NazivNormalized { get; set; }

        public string? Aktivna { get; set; }

        public DateTime DatumKreiranja { get; set; }

        public string? Napomena { get; set; }
    }
}