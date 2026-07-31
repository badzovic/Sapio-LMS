namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class UserAuditIndexVM
    {
        public string Section { get; set; } = "import";

        public int ImportUnreadCount { get; set; }

        public int SyncUnreadCount { get; set; }

        public int DeactivationUnreadCount { get; set; }

        public List<ImportUserAuditItemVM> ImportItems { get; set; } = new();
        public List<SyncAuditItemVM> SyncItems { get; set; } = new();

        public List<DeactivationAuditItemVM> DeactivationItems { get; set; } = new();
    }
}