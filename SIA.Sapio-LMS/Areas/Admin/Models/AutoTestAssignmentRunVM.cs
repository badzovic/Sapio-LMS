namespace SIA.Sapio_LMS.Areas.Admin.Models
{
    public class AutoTestAssignmentRunVM
    {
        public int ID { get; set; }

        public DateTime DatumPokretanja { get; set; }
        public DateTime? DatumZavrsetka { get; set; }

        public string TipPokretanja { get; set; } = "";
        public string Status { get; set; } = "";

        public int? PredmetId { get; set; }
        public string? PredmetNaziv { get; set; }

        public int UkupnoPredmeta { get; set; }
        public int UkupnoKorisnikaUMatrici { get; set; }

        public int UkupnoDodijeljeno { get; set; }
        public int UkupnoSticanje { get; set; }
        public int UkupnoObnova { get; set; }

        public int UkupnoNemaValidnogTesta { get; set; }
        public int UkupnoVecDodijeljeno { get; set; }
        public int UkupnoGresaka { get; set; }

        public string? Napomena { get; set; }
    }
}