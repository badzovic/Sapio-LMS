using System;
using System.Collections.Generic;

namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class AssignedTestAttemptsIndexVM
    {
        public List<AssignedTestAttemptItemVM> Polaganja { get; set; }
            = new();

        // Filteri
        public string? Search { get; set; }
        public string? Rezultat { get; set; }
        public DateTime? DatumOd { get; set; }
        public DateTime? DatumDo { get; set; }

        // Paginacija
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalCount { get; set; }

        public int TotalPages =>
            (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}