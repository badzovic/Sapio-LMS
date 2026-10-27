
namespace SIA.Sapio_LMS.Models
{
    public class UserOtherTrainingsVM
    {
        public int KorisnikId { get; set; }

        public List<UserOtherTrainingItemVM> Predmeti { get; set; } = new();
    }
}
