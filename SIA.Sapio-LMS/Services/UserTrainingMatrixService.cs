
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio_LMS.Models;

namespace SIA.Sapio_LMS.Services
{
    public class UserTrainingMatrixService
    {
        private readonly V3MatrixApiService _v3MatrixApiService;
        private readonly ApplicationDbContext _dbContext;

        public UserTrainingMatrixService(
            V3MatrixApiService v3MatrixApiService,
            ApplicationDbContext dbContext)
        {
            _v3MatrixApiService = v3MatrixApiService;
            _dbContext = dbContext;
        }

        public async Task<UserTrainingMatrixVM?> GetForRadnikAsync(int? accessKey)
        {
            if (!accessKey.HasValue || accessKey.Value <= 0)
                return null;

            // 1. Postojeći API poziv za matricu radnika
            var matrixDto = await _v3MatrixApiService
                .GetMatrixByRadnikAsync(accessKey.Value);

            if (matrixDto == null)
                return null;

            var programIds = matrixDto.ProgramIds ?? new List<int>();

            // 2. Postojeće povezivanje V3 programa i LMS predmeta
            var sviPredmeti = await _dbContext.PREDMETI
                .AsNoTracking()
                .Where(p => p.V3_ID_OBUKE.HasValue)
                .OrderBy(p => p.NAZIV)
                .ToListAsync();

            var predmeti = sviPredmeti
                .Where(p =>
                    p.V3_ID_OBUKE.HasValue &&
                    programIds.Contains(p.V3_ID_OBUKE.Value))
                .Select(p => new UserTrainingMatrixItemVM
                {
                    PredmetId = p.ID,
                    V3IdObuke = p.V3_ID_OBUKE,
                    Oznaka = p.OZNAKA,
                    Naziv = p.NAZIV,
                    NazivEng = p.NAZIV_ENG,
                    Aktivan = p.AKTIVNO
                })
                .ToList();

            // 3. Pronalazimo lokalnog LMS korisnika
            var lokalniKorisnikId = await _dbContext.KORISNICI
                .AsNoTracking()
                .Where(k => k.ACCESS_KEY == accessKey.Value)
                .Select(k => (int?)k.ID)
                .FirstOrDefaultAsync();

            var danas = DateTime.Today;
            var granicaIsteka = danas.AddDays(60);

            // 4. Popunjavanje statusa za svaki predmet
            foreach (var predmet in predmeti)
            {
                // -----------------------------------------
                // V3 STATUS OBUKE
                // -----------------------------------------

                if (predmet.V3IdObuke.HasValue)
                {
                    var v3Response = await _v3MatrixApiService
                        .GetRadniciProgramStatusAsync(
                            predmet.V3IdObuke.Value);

                    var v3Radnik = v3Response?.Users?
                        .FirstOrDefault(x =>
                            x.RadnikId == matrixDto.RadnikId);

                    if (v3Radnik == null)
                    {
                        predmet.V3Status = "Nepoznato";
                    }
                    else
                    {
                        predmet.V3ImaDozvolu = v3Radnik.ImaDozvolu;
                        predmet.V3Isteklo = v3Radnik.Isteklo;

                        predmet.V3DatumVaziDo = v3Radnik.DatumVaziDo;
                        predmet.V3DanaDoIsteka = v3Radnik.DatumVaziDo.HasValue
                            ? (v3Radnik.DatumVaziDo.Value.Date - danas).Days
                            : v3Radnik.DanaDoIsteka;

                        if (v3Radnik.Isteklo ||
                            (v3Radnik.DatumVaziDo.HasValue &&
                             v3Radnik.DatumVaziDo.Value.Date < danas))
                        {
                            predmet.V3Status = "Obuka istekla";
                        }
                        else if (!v3Radnik.ImaDozvolu)
                        {
                            predmet.V3Status = "Nema evidencije";
                        }
                        else if (!v3Radnik.DatumVaziDo.HasValue)
                        {
                            predmet.V3Status = "Nepoznato";
                        }
                        else if (v3Radnik.DatumVaziDo.Value.Date <= granicaIsteka)
                        {
                            predmet.V3Status = "Uskoro ističe";
                        }
                        else
                        {
                            predmet.V3Status = "Važeća obuka";
                        }
                    }
                }

                // -----------------------------------------
                // LMS STATUS TESTIRANJA
                // -----------------------------------------

                if (!lokalniKorisnikId.HasValue)
                {
                    predmet.LmsStatus = "Korisnik nije povezan s LMS-om";
                    continue;
                }

                // Posljednja dodjela za ovaj predmet i korisnika
                var dodjela = await _dbContext.DODIJELJENI_TESTOVI
                    .AsNoTracking()
                    .Where(x =>
                        x.KORISNIK_ID == lokalniKorisnikId.Value &&
                        x.TEST.PREDMET_ID == predmet.PredmetId)
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
                    predmet.LmsStatus = "Nema dodijeljenog testa";
                    continue;
                }

                predmet.LmsDodjelaId = dodjela.ID;
                predmet.LmsTestId = dodjela.TEST_ID;

                predmet.LmsDatumDodjele = dodjela.DATUM_DODJELE;
                predmet.LmsRokZaPolaganje = dodjela.ROK_ZA_POLAGANJE;

                // Pokušaji nastali nakon posljednje dodjele
                var pokusaji = await _dbContext.POLAGANJE_TESTA
                    .AsNoTracking()
                    .Where(x =>
                        x.KORISNIK_ID == lokalniKorisnikId.Value &&
                        x.TEST_ID == dodjela.TEST_ID &&
                        x.DATUM_POCETKA >= dodjela.DATUM_DODJELE)
                    .OrderByDescending(x => x.DATUM_POCETKA)
                    .ToListAsync();

                predmet.LmsBrojPokusaja = pokusaji.Count;

                predmet.LmsNajboljiProcenat = pokusaji
                    .Where(x => x.PROCENAT.HasValue)
                    .Select(x => x.PROCENAT)
                    .DefaultIfEmpty()
                    .Max();

                // Status određujemo prema podacima o dodjeli
                // i pokušajima polaganja.

                if (dodjela.AKTIVNO != "Y")
                {
                    predmet.LmsStatus = "Neaktivna dodjela";
                }
                else if (pokusaji.Any(x => x.IS_PASSED == "Y"))
                {
                    predmet.LmsStatus = "Položen u LMS-u";
                }
                else if (pokusaji.Any(x => x.STATUS == "IN_PROGRESS"))
                {
                    predmet.LmsStatus = "Polaganje u toku";
                }
                else if (pokusaji.Any(x => x.STATUS == "PENDING_REVIEW"))
                {
                    predmet.LmsStatus = "Na ocjenjivanju";
                }
                else if (dodjela.ROK_ZA_POLAGANJE.HasValue &&
                         dodjela.ROK_ZA_POLAGANJE.Value < DateTime.Now)
                {
                    predmet.LmsStatus = "Rok testa istekao";
                }
                else if (pokusaji.Any(x => x.IS_PASSED == "N"))
                {
                    predmet.LmsStatus = "Nije položen u LMS-u";
                }
                else
                {
                    predmet.LmsStatus = "Test dodijeljen";
                }
            }

            // 5. Vraćamo postojeći model matrice
            return new UserTrainingMatrixVM
            {
                RadnikId = matrixDto.RadnikId,
                IdZanimanja = matrixDto.IdZanimanja,
                NazivRadnogMjesta = matrixDto.NazivRadnogMjesta,
                Predmeti = predmeti
            };
        }
    }
}
