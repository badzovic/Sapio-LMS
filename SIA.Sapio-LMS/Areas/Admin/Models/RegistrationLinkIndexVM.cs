using System.Collections.Generic;

namespace SIA.Sapio_LMS.Areas.Admin.Models.RegistrationLinks
{
    public class RegistrationLinkIndexVM
    {
        public CreateRegistrationLinkVM CreateForm { get; set; } = new();
        public List<RegistrationLinkRowVM> Links { get; set; } = new();
        public string? ActiveRegistrationUrl { get; set; }
    }
}