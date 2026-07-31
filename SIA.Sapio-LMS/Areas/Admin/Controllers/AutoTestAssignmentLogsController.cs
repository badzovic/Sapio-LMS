using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio_LMS.Areas.Admin.Models;

namespace SIA.Sapio_LMS.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AutoTestAssignmentLogsController : BaseAdminController
    {
        private readonly ApplicationDbContext _dbContext;

        public AutoTestAssignmentLogsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index()
        {
            var model = await _dbContext.AUTO_DODJELA_TESTOVA_RUN
                .Include(x => x.PREDMET)
                .OrderByDescending(x => x.DATUM_POKRETANJA)
                .Select(x => new AutoTestAssignmentRunVM
                {
                    ID = x.ID,
                    DatumPokretanja = x.DATUM_POKRETANJA,
                    DatumZavrsetka = x.DATUM_ZAVRSETKA,
                    TipPokretanja = x.TIP_POKRETANJA,
                    Status = x.STATUS,

                    PredmetId = x.PREDMET_ID,
                    PredmetNaziv = x.PREDMET != null ? x.PREDMET.NAZIV : "Svi predmeti",

                    UkupnoPredmeta = x.UKUPNO_PREDMETA,
                    UkupnoKorisnikaUMatrici = x.UKUPNO_KORISNIKA_U_MATRICI,

                    UkupnoDodijeljeno = x.UKUPNO_DODIJELJENO,
                    UkupnoSticanje = x.UKUPNO_STICANJE,
                    UkupnoObnova = x.UKUPNO_OBNOVA,

                    UkupnoNemaValidnogTesta = x.UKUPNO_NEMA_VALIDNOG_TESTA,
                    UkupnoVecDodijeljeno = x.UKUPNO_VEC_DODIJELJENO,
                    UkupnoGresaka = x.UKUPNO_GRESAKA,

                    Napomena = x.NAPOMENA
                })
                .ToListAsync();

            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var run = await _dbContext.AUTO_DODJELA_TESTOVA_RUN
                .Include(x => x.PREDMET)
                .FirstOrDefaultAsync(x => x.ID == id);

            if (run == null)
                return NotFound();

            var logovi = await _dbContext.AUTO_DODJELA_TESTOVA_LOG
                .Include(x => x.PREDMET)
                .Include(x => x.KORISNIK)
                .Include(x => x.TEST)
                .Include(x => x.TEST_KLASA)
                .Where(x => x.RUN_ID == id)
                .OrderByDescending(x => x.DATUM_KREIRANJA)
                .Select(x => new AutoTestAssignmentLogVM
                {
                    ID = x.ID,

                    TipDogadjaja = x.TIP_DOGADJAJA,
                    Razlog = x.RAZLOG,

                    PredmetId = x.PREDMET_ID,
                    PredmetNaziv = x.PREDMET != null ? x.PREDMET.NAZIV : null,

                    KorisnikId = x.KORISNIK_ID,
                    KorisnikImePrezime = x.KORISNIK != null
                        ? ((x.KORISNIK.IME ?? "") + " " + (x.KORISNIK.PREZIME ?? "")).Trim()
                        : null,

                    TestId = x.TEST_ID,
                    TestNaziv = x.TEST != null ? x.TEST.NAZIV : null,

                    TestKlasaId = x.TEST_KLASA_ID,
                    TestKlasaNaziv = x.TEST_KLASA != null ? x.TEST_KLASA.NAZIV : null,

                    PogodjenoKorisnika = x.POGODJENO_KORISNIKA,
                    Poruka = x.PORUKA,

                    DatumKreiranja = x.DATUM_KREIRANJA
                })
                .ToListAsync();

            var model = new AutoTestAssignmentRunDetailsVM
            {
                ID = run.ID,
                DatumPokretanja = run.DATUM_POKRETANJA,
                DatumZavrsetka = run.DATUM_ZAVRSETKA,
                TipPokretanja = run.TIP_POKRETANJA,
                Status = run.STATUS,

                PredmetId = run.PREDMET_ID,
                PredmetNaziv = run.PREDMET != null ? run.PREDMET.NAZIV : "Svi predmeti",

                UkupnoPredmeta = run.UKUPNO_PREDMETA,
                UkupnoKorisnikaUMatrici = run.UKUPNO_KORISNIKA_U_MATRICI,

                UkupnoDodijeljeno = run.UKUPNO_DODIJELJENO,
                UkupnoSticanje = run.UKUPNO_STICANJE,
                UkupnoObnova = run.UKUPNO_OBNOVA,

                UkupnoNemaValidnogTesta = run.UKUPNO_NEMA_VALIDNOG_TESTA,
                UkupnoVecDodijeljeno = run.UKUPNO_VEC_DODIJELJENO,
                UkupnoGresaka = run.UKUPNO_GRESAKA,

                Napomena = run.NAPOMENA,
                Logovi = logovi
            };

            return View(model);
        }
    }
}