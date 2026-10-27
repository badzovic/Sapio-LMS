
namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class CustomTrainingMatrixItemVM
    {
        public int ID { get; set; }

        public int KorisnikId { get; set; }
        public int PredmetId { get; set; }

        public string NazivPredmeta { get; set; } = "";

        public DateTime DatumDodjele { get; set; }

        public string? DodijelioUserId { get; set; }

        public string DodijelioImePrezime { get; set; } = "-";

        public string DeaktiviraoImePrezime { get; set; } = "-";


        public bool Aktivno { get; set; }

        public DateTime? DatumDeaktivacije { get; set; }

        public string? Napomena { get; set; }
    }
}
