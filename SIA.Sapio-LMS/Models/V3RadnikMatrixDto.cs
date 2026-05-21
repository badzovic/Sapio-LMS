namespace SIA.Sapio_LMS.Models
{
    public class V3RadnikMatrixDto
    {
        public int RadnikId { get; set; }
        public int IdZanimanja { get; set; }
        public string? NazivRadnogMjesta { get; set; }
        public List<int> ProgramIds { get; set; } = new();
    }
}