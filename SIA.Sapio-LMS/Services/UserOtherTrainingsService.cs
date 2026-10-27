
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio_LMS.Models;

namespace SIA.Sapio_LMS.Services
{
    public class UserOtherTrainingsService
    {
        private readonly ApplicationDbContext _dbContext;

        public UserOtherTrainingsService(
            ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<UserOtherTrainingsVM> GetForKorisnikAsync(
            int korisnikId,
            UserTrainingMatrixVM? v3Matrix)
        {
            var model = new UserOtherTrainingsVM
            {
                KorisnikId = korisnikId
            };

            // 1. Predmeti u trenutnoj V3 matrici
            var v3PredmetIds = v3Matrix?.Predmeti?
                .Select(x => x.PredmetId)
                .ToHashSet() ?? new HashSet<int>();

            // 2. Predmeti u aktivnoj Custom matrici
            var customPredmetIds = await _dbContext.KORISNIK_PREDMETI
                .AsNoTracking()
                .Where(x =>
                    x.KORISNIK_ID == korisnikId &&
                    x.AKTIVNO == "Y")
                .Select(x => x.PREDMET_ID)
                .ToListAsync();

            var iskljuceniPredmeti = v3PredmetIds
                .Union(customPredmetIds)
                .ToHashSet();


            // 3. Sve prethodne LMS dodjele korisnika
            var dodjele = await _dbContext.DODIJELJENI_TESTOVI
                .AsNoTracking()
                .Where(x => x.KORISNIK_ID == korisnikId)
                .Select(x => new
                {
                    x.ID,
                    x.TEST_ID,
                    x.DATUM_DODJELE,

                    PredmetId = x.TEST.PREDMET_ID,

                    NazivPredmeta = _dbContext.PREDMETI
                        .Where(p => p.ID == x.TEST.PREDMET_ID)
                        .Select(p => p.NAZIV)
                        .FirstOrDefault() ?? "-"
                })
                .ToListAsync();


            // 4. Predmeti s historijom izvan obje matrice
            var ostaleDodjele = dodjele
                .Where(x =>
                    !iskljuceniPredmeti.Contains(x.PredmetId))
                .ToList();

            if (!ostaleDodjele.Any())
                return model;

            // 5. Pokušaji polaganja korisnika
            var pokusaji = await _dbContext.POLAGANJE_TESTA
                .AsNoTracking()
                .Where(x => x.KORISNIK_ID == korisnikId)
                .Select(x => new
                {
                    x.TEST_ID,
                    x.DATUM_POCETKA,
                    x.DATUM_ZAVRSETKA,
                    x.PROCENAT,
                    x.IS_PASSED
                })
                .ToListAsync();

            // 6. Grupisanje historije po predmetima
            foreach (var grupa in ostaleDodjele
                .GroupBy(x => new
                {
                    x.PredmetId,
                    x.NazivPredmeta
                })
                .OrderBy(x => x.Key.NazivPredmeta))
            {
                var testIds = grupa
                    .Select(x => x.TEST_ID)
                    .Distinct()
                    .ToHashSet();

                var pokusajiPredmeta = pokusaji
                    .Where(x => testIds.Contains(x.TEST_ID))
                    .ToList();

                var zavrseniPokusaji = pokusajiPredmeta
                    .Where(x => x.DATUM_ZAVRSETKA.HasValue)
                    .ToList();

                var najboljiProcenat = zavrseniPokusaji
                    .Where(x => x.PROCENAT.HasValue)
                    .Select(x => x.PROCENAT)
                    .DefaultIfEmpty()
                    .Max();

                var item = new UserOtherTrainingItemVM
                {
                    PredmetId = grupa.Key.PredmetId,
                    NazivPredmeta = grupa.Key.NazivPredmeta,

                    BrojDodjela = grupa.Count(),

                    BrojPokusaja = pokusajiPredmeta.Count,

                    PosljednjaDodjela = grupa
                        .Max(x => (DateTime?)x.DATUM_DODJELE),

                    PosljednjePolaganje = zavrseniPokusaji
                        .Max(x => x.DATUM_ZAVRSETKA),

                    NajboljiProcenat = najboljiProcenat,

                    ImaPolozenTest = zavrseniPokusaji
                        .Any(x => x.IS_PASSED == "Y")
                };

                model.Predmeti.Add(item);
            }

            return model;
        }
    }
}
