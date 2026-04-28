using System.Collections.Generic;

namespace SIA.Sapio_LMS.Areas.Admin.Models.Users
{
    public class UserIndexVM
    {
        public List<UserIndexRowVM> Users { get; set; } = new();

        public string Status { get; set; } = "all";
        public string TypeFilter { get; set; } = "all";
    }
}