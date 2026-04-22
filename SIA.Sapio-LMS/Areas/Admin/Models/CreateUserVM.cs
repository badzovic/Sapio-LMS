using System.ComponentModel.DataAnnotations;

namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class CreateUserVM
    {
        [EmailAddress(ErrorMessage = "Email nije ispravan.")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Ime je obavezno.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Prezime je obavezno.")]
        public string LastName { get; set; } = string.Empty;

        //[Required(ErrorMessage = "Password je obavezan.")]
        //[DataType(DataType.Password)]

        [Required(ErrorMessage = "Ime oca je obavezno.")]
        public string ImeOca { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Rola je obavezna.")]
        public string Role { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tip korisnika je obavezan.")]
        public int? TipKorisnikaId { get; set; }
        public bool IsExternalCollaborator { get; set; }
    }
}


