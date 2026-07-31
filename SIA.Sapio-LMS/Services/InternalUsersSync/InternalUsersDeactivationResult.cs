namespace SIA.Sapio_LMS.Services.InternalUsersSync
{
    public class InternalUsersDeactivationResult
    {
        public int Deactivated { get; set; }

        public int AlreadyInactive { get; set; }

        public int SkippedWithoutIdentity { get; set; }

        public int Failed { get; set; }

        public bool ApiReturnedNoUsers { get; set; }

        public List<string> ErrorMessages { get; set; } = new();

        public string Message
        {
            get
            {
                if (ApiReturnedNoUsers)
                {
                    return
                        "Deaktivacija nije izvršena jer HRM API nije vratio nijednog korisnika.";
                }

                return
                    $"Provjera neaktivnih korisnika završena. " +
                    $"Deaktivirani: {Deactivated}, " +
                    $"Već neaktivni: {AlreadyInactive}, " +
                    $"Bez Identity naloga: {SkippedWithoutIdentity}, " +
                    $"Greške: {Failed}";
            }
        }
    }
}