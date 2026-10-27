
namespace SIA.Sapio_LMS.Models
{
    public class UserCustomTrainingMatrixItemVM
    {
        public int ID { get; set; }

        public int PredmetId { get; set; }

        public string NazivPredmeta { get; set; } = "";

        // Dodjela predmeta u Custom matricu
        public DateTime DatumDodjele { get; set; }

        public string? Napomena { get; set; }

        // LMS testiranje
        public int? LmsDodjelaId { get; set; }
        public int? LmsTestId { get; set; }

        public string LmsStatus { get; set; } = "Nema dodijeljenog testa";

        public DateTime? LmsDatumDodjele { get; set; }
        public DateTime? LmsRokZaPolaganje { get; set; }

        public DateTime? LmsDatumPolaganja { get; set; }

        public int LmsBrojPokusaja { get; set; }

        public decimal? LmsNajboljiProcenat { get; set; }
    }
}
