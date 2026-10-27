using System;
using System.Collections.Generic;

namespace SIA.Sapio_LMS.Models
{
    public class UserTrainingHistoryVM
    {
        public int KorisnikId { get; set; }
        public int PredmetId { get; set; }

        public string ImePrezime { get; set; } = "";
        public string NazivPredmeta { get; set; } = "";

        public int BrojDodjela { get; set; }
        public int BrojPokusaja { get; set; }

        public List<UserTrainingHistoryAssignmentVM> Dodjele { get; set; }
            = new();
    }

    public class UserTrainingHistoryAssignmentVM
    {
        public int DodjelaId { get; set; }
        public int TestId { get; set; }

        public string NazivTesta { get; set; } = "";
        public string? NazivKlase { get; set; }

        public DateTime DatumDodjele { get; set; }
        public DateTime? RokZaPolaganje { get; set; }

        public string? Status { get; set; }
        public string Aktivno { get; set; } = "Y";
        public string AutoDodjela { get; set; } = "N";
        public string? RazlogDodjele { get; set; }
        public string? Napomena { get; set; }

        public List<UserTrainingHistoryAttemptVM> Pokusaji { get; set; }
            = new();
    }

    public class UserTrainingHistoryAttemptVM
    {
        public int PolaganjeId { get; set; }
        public int RedniBrojPokusaja { get; set; }

        public DateTime DatumPocetka { get; set; }
        public DateTime? DatumZavrsetka { get; set; }

        public string Status { get; set; } = "";

        public decimal? OstvareniBodovi { get; set; }
        public decimal? MaxBodovi { get; set; }
        public decimal? Procenat { get; set; }

        public string? IsPassed { get; set; }
        public bool AutoSubmitted { get; set; }
    }
}