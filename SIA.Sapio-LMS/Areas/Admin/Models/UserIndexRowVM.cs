namespace SIA.Sapio_LMS.Areas.Admin.Models.Users
{
    public class UserIndexRowVM
    {
        public string UserId { get; set; } = string.Empty;
        public int? KorisnikId { get; set; }

        public string? Email { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? ImeOca { get; set; }

        public bool IsActiveIdentity { get; set; }
        public string AktivanLocal { get; set; } = "N";

        public bool IsExternalCollaborator { get; set; }
        public bool MustChangePassword { get; set; }

        public int? TipKorisnikaId { get; set; }
        public string? TipKorisnikaNaziv { get; set; }

        public bool IsPending { get; set; }

        public string DetailsController { get; set; } = string.Empty;
        public string DetailsAction { get; set; } = "Details";
        public string DetailsRouteType { get; set; } = "userId"; // "userId" ili "id"
    }
}