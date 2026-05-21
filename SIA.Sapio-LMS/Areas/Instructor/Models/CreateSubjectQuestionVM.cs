using Microsoft.AspNetCore.Mvc.Rendering;

namespace SIA.Sapio_LMS.Areas.Instructor.Models
{
    public class CreateSubjectQuestionVM
    {
        public int PredmetId { get; set; }
        public string PredmetNaziv { get; set; } = "";

        public int TipPitanjaId { get; set; }
        public string? TipPitanjaSifra { get; set; }

        public int TipTezineId { get; set; }

        public string TekstPitanja { get; set; } = "";

        public decimal Bodovi { get; set; } = 1;
        public decimal? NegativniBodovi { get; set; }

        public string ParcijalnoBodovanje { get; set; } = "N";

        public List<CreateSubjectQuestionAnswerVM> Odgovori { get; set; } = new();

        public List<SelectListItem> TipoviPitanja { get; set; } = new();
        public List<SelectListItem> TipoviTezine { get; set; } = new();
    }
}