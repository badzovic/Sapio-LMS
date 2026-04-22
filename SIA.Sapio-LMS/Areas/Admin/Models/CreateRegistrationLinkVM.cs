using System.ComponentModel.DataAnnotations;

namespace SIA.Sapio_LMS.Areas.Admin.Models.RegistrationLinks
{
    public class CreateRegistrationLinkVM
    {
        [Required(ErrorMessage = "Trajanje u minutama je obavezno.")]
        [Range(1, 1440, ErrorMessage = "Trajanje mora biti između 1 i 1440 minuta.")]
        public int DurationMinutes { get; set; }

        public string? Note { get; set; }
    }
}