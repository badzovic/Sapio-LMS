
namespace SIA.Sapio_LMS.Models
{
    public class UserTrainingMatrixItemVM
    {
        // Postojeći podaci o predmetu
        public int PredmetId { get; set; }
        public int? V3IdObuke { get; set; }

        public string? Oznaka { get; set; }
        public string? Naziv { get; set; }
        public string? NazivEng { get; set; }
        public string? Aktivan { get; set; }

        // V3 - status obuke korisnika
        public bool? V3ImaDozvolu { get; set; }
        public bool? V3Isteklo { get; set; }

        public DateTime? V3DatumVaziDo { get; set; }
        public int? V3DanaDoIsteka { get; set; }

        public string V3Status { get; set; } = "Nepoznato";

        // LMS Sapio - status testiranja
        public int? LmsDodjelaId { get; set; }
        public int? LmsTestId { get; set; }

        public string LmsStatus { get; set; } = "Nema dodijeljenog testa";

        public DateTime? LmsDatumDodjele { get; set; }
        public DateTime? LmsRokZaPolaganje { get; set; }

        public int LmsBrojPokusaja { get; set; }
        public decimal? LmsNajboljiProcenat { get; set; }
    }
}
