
namespace SIA.Sapio_LMS.Areas.Models
{
    public class AssignTestStudentItemVM
    {
        public int KorisnikId { get; set; }

        public string ImePrezime { get; set; } = "";

        public string? Email { get; set; }

        public string? Kompanija { get; set; }

        // Postojeća dodjela
        public bool VecDodijeljen { get; set; }

        public bool Odabran { get; set; }

        // V3 evidencija
        public DateTime? DatumVaziDo { get; set; }

        public int? DanaDoIsteka { get; set; }

        public string StatusObuke { get; set; } = "";

        // Podaci o posljednjoj dodjeli testa
        public int? PosljednjaDodjelaId { get; set; }

        public DateTime? DatumPosljednjeDodjele { get; set; }

        public DateTime? RokPosljednjeDodjele { get; set; }

        public string StatusPosljednjeDodjele { get; set; } = "";

        // Rezultati polaganja posljednje dodjele
        public int BrojIskoristenihPokusaja { get; set; }

        public int BrojDozvoljenihPokusaja { get; set; }

        public bool ImaPolozenTest { get; set; }

        public bool CekaOcjenjivanje { get; set; }

        // Kontrola ponovne dodjele
        public bool MozeNovaDodjela { get; set; }

        public bool PotrebnaIntervencijaAdmina { get; set; }

        public string RazlogNemoguceDodjele { get; set; } = "";
    }
}
