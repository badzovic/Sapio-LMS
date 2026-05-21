namespace SIA.Sapio_LMS.Models
{
    public class V3RadnikProgramStatusResponse
    {
        public int Count { get; set; }

        public int ProgramId { get; set; }

        public List<V3RadnikProgramStatusDto> Users { get; set; } = new();
    }
}