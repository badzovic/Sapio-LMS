
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio_LMS.Models;

namespace SIA.Sapio_LMS.Services
{
    public class UserCustomTrainingMatrixService
    {
        private readonly ApplicationDbContext _dbContext;

        public UserCustomTrainingMatrixService(
            ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<UserCustomTrainingMatrixVM> GetForKorisnikAsync(
            int korisnikId,
            UserTrainingMatrixVM? v3Matrix)
        {
            var model = new UserCustomTrainingMatrixVM
            {
                KorisnikId = korisnikId
            };

            // 1. Podaci o korisniku
            var korisnik = await _dbContext.KORISNICI
                .AsNoTracking()
                .Where(x => x.ID == korisnikId)
                .Select(x => new
                {
                    x.IME,
                    x.PREZIME
                })
                .FirstOrDefaultAsync();

            if (korisnik == null)
                return model;

            model.ImePrezime =
                $"{korisnik.IME} {korisnik.PREZIME}".Trim();

            // 2. Aktivna Custom matrica korisnika
            var customPredmeti = await _dbContext.KORISNIK_PREDMETI
                .AsNoTracking()
                .Where(x =>
                    x.KORISNIK_ID == korisnikId &&
                    x.AKTIVNO == "Y")
                .OrderBy(x => x.PREDMET.NAZIV)
                .Select(x => new
                {
                    x.ID,
                    x.PREDMET_ID,
                    NazivPredmeta = x.PREDMET.NAZIV,
                    x.DATUM_DODJELE,
                    x.NAPOMENA
                })
                .ToListAsync();

            // 3. Predmeti koji već pripadaju V3 matrici
            // Koristimo već učitanu matricu - bez novog API poziva.
            var v3PredmetIds = v3Matrix?.Predmeti?
                .Select(x => x.PredmetId)
                .ToHashSet() ?? new HashSet<int>();

            var predmetiZaPrikaz = customPredmeti
                .Where(x => !v3PredmetIds.Contains(x.PREDMET_ID))
                .ToList();

            // 4. LMS podaci za svaki Custom predmet
            foreach (var predmet in predmetiZaPrikaz)
            {
                var item = new UserCustomTrainingMatrixItemVM
                {
                    ID = predmet.ID,
                    PredmetId = predmet.PREDMET_ID,
                    NazivPredmeta = predmet.NazivPredmeta,
                    DatumDodjele = predmet.DATUM_DODJELE,
                    Napomena = predmet.NAPOMENA
                };

                // Posljednja dodjela testa iz ovog predmeta
                var dodjela = await _dbContext.DODIJELJENI_TESTOVI
                    .AsNoTracking()
                    .Where(x =>
                        x.KORISNIK_ID == korisnikId &&
                        x.TEST.PREDMET_ID == predmet.PREDMET_ID)
                    .OrderByDescending(x => x.DATUM_DODJELE)
                    .ThenByDescending(x => x.ID)
                    .Select(x => new
                    {
                        x.ID,
                        x.TEST_ID,
                        x.DATUM_DODJELE,
                        x.ROK_ZA_POLAGANJE,
                        x.AKTIVNO
                    })
                    .FirstOrDefaultAsync();

                if (dodjela == null)
                {
                    item.LmsStatus = "Nema dodijeljenog testa";
                    model.Predmeti.Add(item);
                    continue;
                }

                item.LmsDodjelaId = dodjela.ID;
                item.LmsTestId = dodjela.TEST_ID;

                item.LmsDatumDodjele = dodjela.DATUM_DODJELE;
                item.LmsRokZaPolaganje = dodjela.ROK_ZA_POLAGANJE;

                // Pokušaji posljednje dodjele
                var pokusaji = await _dbContext.POLAGANJE_TESTA
                    .AsNoTracking()
                    .Where(x =>
                        x.KORISNIK_ID == korisnikId &&
                        x.TEST_ID == dodjela.TEST_ID &&
                        x.DATUM_POCETKA >= dodjela.DATUM_DODJELE)
                    .OrderByDescending(x => x.DATUM_POCETKA)
                    .ToListAsync();

                item.LmsBrojPokusaja = pokusaji.Count;

                item.LmsNajboljiProcenat = pokusaji
                    .Where(x => x.PROCENAT.HasValue)
                    .Select(x => x.PROCENAT)
                    .DefaultIfEmpty()
                    .Max();

                // Datum posljednjeg završenog polaganja
                item.LmsDatumPolaganja = pokusaji
                    .Where(x => x.DATUM_ZAVRSETKA.HasValue)
                    .OrderByDescending(x => x.DATUM_ZAVRSETKA)
                    .Select(x => x.DATUM_ZAVRSETKA)
                    .FirstOrDefault();

                // 5. Status LMS testiranja
                if (dodjela.AKTIVNO != "Y")
                {
                    item.LmsStatus = "Neaktivna dodjela";
                }
                else if (pokusaji.Any(x => x.IS_PASSED == "Y"))
                {
                    item.LmsStatus = "Položen u LMS-u";
                }
                else if (pokusaji.Any(x => x.STATUS == "IN_PROGRESS"))
                {
                    item.LmsStatus = "Polaganje u toku";
                }
                else if (pokusaji.Any(x => x.STATUS == "PENDING_REVIEW"))
                {
                    item.LmsStatus = "Na ocjenjivanju";
                }
                else if (dodjela.ROK_ZA_POLAGANJE.HasValue &&
                         dodjela.ROK_ZA_POLAGANJE.Value < DateTime.Now)
                {
                    item.LmsStatus = "Rok testa istekao";
                }
                else if (pokusaji.Any(x => x.IS_PASSED == "N"))
                {
                    item.LmsStatus = "Nije položen u LMS-u";
                }
                else
                {
                    item.LmsStatus = "Test dodijeljen";
                }

                model.Predmeti.Add(item);
            }

            return model;
        }
    }
}
