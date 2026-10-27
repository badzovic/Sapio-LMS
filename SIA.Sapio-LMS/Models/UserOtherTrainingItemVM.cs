
namespace SIA.Sapio_LMS.Models
{
    public class UserOtherTrainingItemVM
    {
        public int PredmetId { get; set; }

        public string NazivPredmeta { get; set; } = "";

        public int BrojDodjela { get; set; }

        public int BrojPokusaja { get; set; }

        public DateTime? PosljednjaDodjela { get; set; }

        public DateTime? PosljednjePolaganje { get; set; }

        public decimal? NajboljiProcenat { get; set; }

        public bool ImaPolozenTest { get; set; }
    }
}
