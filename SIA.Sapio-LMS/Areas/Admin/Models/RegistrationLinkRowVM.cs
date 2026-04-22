namespace SIA.Sapio_LMS.Areas.Admin.Models.RegistrationLinks
{
    public class RegistrationLinkRowVM
    {
        public int Id { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string IsActive { get; set; } = "N";
        public int UsedCount { get; set; }
        public string? Note { get; set; }
        public bool IsCurrentlyValid { get; set; }
    }
}