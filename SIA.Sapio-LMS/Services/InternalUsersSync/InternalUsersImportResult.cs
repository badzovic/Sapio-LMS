namespace SIA.Sapio_LMS.Services.InternalUsersSync
{
    public class InternalUsersImportResult
    {
        public int Imported { get; set; }

        public int Converted { get; set; }

        public int Ambiguous { get; set; }

        public int Skipped { get; set; }

        public int Failed { get; set; }

        public List<string> ErrorMessages { get; set; } = new();

        public string Message =>
            $"Import završen. " +
            $"Novi: {Imported}, " +
            $"Konvertovani: {Converted}, " +
            $"Nejasni slučajevi: {Ambiguous}, " +
            $"Preskočeni: {Skipped}, " +
            $"Greške: {Failed}";
    }
}