public class SyncAuditItemVM
{
    public int Id { get; set; }

    public int KorisnikId { get; set; }

    public string? UserId { get; set; }

    public int HrmId { get; set; }

    public string? StaroIme { get; set; }
    public string? NovoIme { get; set; }

    public string? StaroPrezime { get; set; }
    public string? NovoPrezime { get; set; }

    public string? StaroImeOca { get; set; }
    public string? NovoImeOca { get; set; }

    public string? StariEmail { get; set; }
    public string? NoviEmail { get; set; }

    public int? StariAccessKey { get; set; }
    public int? NoviAccessKey { get; set; }

    public string PromijenjenaPolja { get; set; } = string.Empty;

    public string? Opis { get; set; }

    public DateTime DatumPromjene { get; set; }

    public bool Pregledano { get; set; }

    public string? PregledaoUserId { get; set; }

    public DateTime? DatumPregleda { get; set; }
    public string? Pregledao { get; set; }
}