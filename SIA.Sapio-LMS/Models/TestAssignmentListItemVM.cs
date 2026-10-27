
namespace SIA.Sapio_LMS.Areas.Models
{
    public class TestAssignmentListItemVM
    {
        public int TestId { get; set; }

        public string TestNaziv { get; set; } = "";

        public string PredmetNaziv { get; set; } = "";

        public string TestKlasaNaziv { get; set; } = "";

        public DateTime? DatumOd { get; set; }

        public DateTime? DatumDo { get; set; }

        public int BrojPitanja { get; set; }

        public int BrojDodijeljenihStudenata { get; set; }

        // Novi podaci za pregled testova
        public string StatusPrikaza { get; set; } = "";

        public bool MozeDodijeliti { get; set; }

        public bool MozePregledati { get; set; }
    }
}
