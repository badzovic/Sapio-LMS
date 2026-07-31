namespace SIA.Sapio_LMS.Areas.Student.Models
{
    public class StudentSubjectMaterialVM
    {
        public int Id { get; set; }

        public string? Naziv { get; set; }

        public string? Opis { get; set; }

        public string? TipMaterijala { get; set; }

        public string? PutanjaFajla { get; set; }

        public string? Url { get; set; }

        public string? NazivOriginalnogFajla { get; set; }

        public string? NazivFajla { get; set; }

        public string? ContentType { get; set; }
    }
}