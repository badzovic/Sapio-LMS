using System.ComponentModel.DataAnnotations;

namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class CreateUserVM
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        public string Password { get; set; }

        [Required]
        public string Role { get; set; }

        public bool IsExternalCollaborator { get; set; }
    }
}