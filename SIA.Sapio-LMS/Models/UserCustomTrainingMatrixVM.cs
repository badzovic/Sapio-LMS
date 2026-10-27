
namespace SIA.Sapio_LMS.Models
{
    public class UserCustomTrainingMatrixVM
    {
        public int KorisnikId { get; set; }

        public string ImePrezime { get; set; } = "";

        public List<UserCustomTrainingMatrixItemVM> Predmeti { get; set; } = new();
    }
}
