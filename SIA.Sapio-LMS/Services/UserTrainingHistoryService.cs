
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio_LMS.Models;

namespace SIA.Sapio_LMS.Services
{
    public class UserTrainingHistoryService
    {
        private readonly ApplicationDbContext _dbContext;

        public UserTrainingHistoryService(
            ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<UserTrainingHistoryVM?> GetForKorisnikAndPredmetAsync(
            int korisnikId,
            int predmetId,
            string imePrezime = "")
        {
            // Provjera postojanja predmeta.
            // Ne filtriramo po AKTIVNO jer prikazujemo historiju.

            var nazivPredmeta = await _dbContext.Set<Predmet>()
                .AsNoTracking()
                .Where(x => x.ID == predmetId)
                .Select(x => x.NAZIV)
                .FirstOrDefaultAsync();

            if (nazivPredmeta == null)
                return null;

            var model = new UserTrainingHistoryVM
            {
                KorisnikId = korisnikId,
                PredmetId = predmetId,
                NazivPredmeta = nazivPredmeta,
                ImePrezime = imePrezime
            };

            // 1. Sve dodjele korisnika za odabrani predmet.
            // Uključujemo stare, istekle i neaktivne dodjele.

            var dodjele = await _dbContext.Set<DodijeljeniTest>()
                .AsNoTracking()
                .Include(x => x.TEST)
                .Where(x =>
                    x.KORISNIK_ID == korisnikId &&
                    x.TEST.PREDMET_ID == predmetId)
                .OrderByDescending(x => x.DATUM_DODJELE)
                .ThenByDescending(x => x.ID)
                .ToListAsync();

            model.BrojDodjela = dodjele.Count;

            // Ako ne postoje dodjele, vraćamo prazan pregled.

            if (dodjele.Count == 0)
                return model;

            // 2. Učitavanje svih pokušaja koji pripadaju
            // dodjelama korisnika za konkretni predmet.
            //
            // Koristimo navigacijsku vezu prema dodjeli,
            // bez Contains() nad lokalnom listom ID-eva.

            var polaganja = await _dbContext.Set<PolaganjeTesta>()
                .AsNoTracking()
                .Where(x =>
                    x.KORISNIK_ID == korisnikId &&
                    x.DODIJELJENI_TEST_ID.HasValue &&
                    x.DodijeljeniTest != null &&
                    x.DodijeljeniTest.KORISNIK_ID == korisnikId &&
                    x.DodijeljeniTest.TEST.PREDMET_ID == predmetId)
                .OrderByDescending(x => x.DATUM_POCETKA)
                .ThenByDescending(x => x.ID)
                .ToListAsync();

            model.BrojPokusaja = polaganja.Count;

            // 3. Grupisanje pokušaja po konkretnoj dodjeli.
            // Ovo se izvršava u memoriji nakon ToListAsync().

            var polaganjaPoDodjeli = polaganja
                .GroupBy(x => x.DODIJELJENI_TEST_ID!.Value)
                .ToDictionary(
                    x => x.Key,
                    x => x.ToList());

            // 4. Formiranje historije dodjela i pokušaja.

            foreach (var dodjela in dodjele)
            {
                var dodjelaVM = new UserTrainingHistoryAssignmentVM
                {
                    DodjelaId = dodjela.ID,
                    TestId = dodjela.TEST_ID,

                    NazivTesta = dodjela.TEST.NAZIV,

                    DatumDodjele = dodjela.DATUM_DODJELE,
                    RokZaPolaganje = dodjela.ROK_ZA_POLAGANJE,

                    Status = dodjela.STATUS,
                    Aktivno = dodjela.AKTIVNO,
                    AutoDodjela = dodjela.AUTO_DODJELA,
                    RazlogDodjele = dodjela.RAZLOG_DODJELE,
                    Napomena = dodjela.NAPOMENA
                };

                if (polaganjaPoDodjeli.TryGetValue(
                    dodjela.ID,
                    out var pokusaji))
                {
                    foreach (var pokusaj in pokusaji)
                    {
                        dodjelaVM.Pokusaji.Add(
                            new UserTrainingHistoryAttemptVM
                            {
                                PolaganjeId = pokusaj.ID,

                                RedniBrojPokusaja =
                                    pokusaj.REDNI_BROJ_POKUSAJA,

                                DatumPocetka =
                                    pokusaj.DATUM_POCETKA,

                                DatumZavrsetka =
                                    pokusaj.DATUM_ZAVRSETKA,

                                Status = pokusaj.STATUS,

                                OstvareniBodovi =
                                    pokusaj.OSTVARENI_BODOVI,

                                MaxBodovi =
                                    pokusaj.MAX_BODOVI,

                                Procenat =
                                    pokusaj.PROCENAT,

                                IsPassed =
                                    pokusaj.IS_PASSED,

                                AutoSubmitted =
                                    pokusaj.AUTO_SUBMITTED == "Y"
                            });
                    }
                }

                model.Dodjele.Add(dodjelaVM);
            }

            return model;
        }
    }
}
