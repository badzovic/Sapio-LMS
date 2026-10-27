
namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class AssignedTestsIndexVM
    {
        // Lista dodijeljenih testova
        public List<AssignedTestItemVM> Dodjele { get; set; } = new();

        // Filteri
        public string? Search { get; set; }
        public string? Status { get; set; }
        public string? NacinDodjele { get; set; }

        public DateTime? DatumOd { get; set; }
        public DateTime? DatumDo { get; set; }

        // Paginacija
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalCount { get; set; }

        public int TotalPages =>
            PageSize > 0
                ? (int)Math.Ceiling((double)TotalCount / PageSize)
                : 0;

        // Statistika
        public int UkupnoDodjela { get; set; }
        public int CekaPolaganje { get; set; }
        public int RokIstekao { get; set; }
        public int Polozeno { get; set; }
        public int NijePolozeno { get; set; }
    }
}
