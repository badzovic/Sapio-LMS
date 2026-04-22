using System.ComponentModel.DataAnnotations;

namespace SIA.Sapio_LMS.Models
{
    public class RegisterUserVM
    {
        [Required(ErrorMessage = "Ime je obavezno.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Prezime je obavezno.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ime oca je obavezno.")]
        public string ImeOca { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email nije ispravan.")]
        public string? Email { get; set; }

        public string? MjestoRodjenja { get; set; }

        public string Token { get; set; } = string.Empty;
    }
}