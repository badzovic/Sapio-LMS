namespace SIA.Sapio_LMS.Models
{
    public class V3RadnikProgramStatusDto
    {
        public int RadnikId { get; set; }
        public string? Ime { get; set; }
        public string? Prezime { get; set; }
        public string? Jmbg { get; set; }

        public int ProgramId { get; set; }

        public DateTime? DatumVaziDo { get; set; }

        public bool ImaDozvolu { get; set; }
        public bool Isteklo { get; set; }
        public int? DanaDoIsteka { get; set; }
    }
}