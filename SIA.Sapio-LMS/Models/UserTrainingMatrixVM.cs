namespace SIA.Sapio_LMS.Models
{
    public class UserTrainingMatrixVM
    {
        public int? RadnikId { get; set; }
        public int? IdZanimanja { get; set; }
        public string? NazivRadnogMjesta { get; set; }

        public List<UserTrainingMatrixItemVM> Predmeti { get; set; } = new();

    }
}