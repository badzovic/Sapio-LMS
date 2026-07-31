namespace SIA.Sapio_LMS.Services.InternalUsersSync
{
    public class InternalUsersSyncResult
    {
        public int Updated { get; set; }

        public int Unchanged { get; set; }

        public int Missing { get; set; }

        public int Failed { get; set; }

        public List<string> ErrorMessages { get; set; } = new();

        public string Message =>
            $"Sync završen. " +
            $"Ažurirani: {Updated}, " +
            $"Bez promjena: {Unchanged}, " +
            $"Nedostaju lokalno: {Missing}, " +
            $"Greške: {Failed}";
    }
}