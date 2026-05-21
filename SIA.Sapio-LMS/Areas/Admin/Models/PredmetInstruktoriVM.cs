namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class PredmetInstruktorVM
    {
        public int ID { get; set; }
        public int KorisnikId { get; set; }
        public string ImePrezime { get; set; } = "";
        public DateTime DatumOd { get; set; }
        public DateTime? DatumDo { get; set; }
        public string Aktivno { get; set; } = "Y";
    }
}