namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class InternalUserDto
    {
        public int HrmId { get; set; }
        public string? Ime { get; set; }
        public string? Prezime { get; set; }
        public string? ImeOca { get; set; }
        public string? Jmb { get; set; }
        public string? Email { get; set; }
        public string? Kompanija { get; set; }
        public int? AccessKey { get; set; }
        public string? UserId { get; set; }
    }
}