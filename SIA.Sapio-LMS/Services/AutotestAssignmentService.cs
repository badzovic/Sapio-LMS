using Hangfire;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio_LMS.Models;

namespace SIA.Sapio_LMS.Services
{
    public class AutoTestAssignmentService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly V3MatrixApiService _v3MatrixApiService;

        private const int InterniKorisnikTipId = 1;

        public AutoTestAssignmentService(
            ApplicationDbContext dbContext,
            V3MatrixApiService v3MatrixApiService)
        {
            _dbContext = dbContext;
            _v3MatrixApiService = v3MatrixApiService;
        }

        [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
        public async Task RunDailyAutoAssignmentsAsync()
        {
            await RunAutoAssignmentsAsync(null, "HANGFIRE", null);
        }


        public async Task RunAutoAssignmentsAsync(
            int? predmetId = null,
            string tipPokretanja = "MANUAL",
            string? pokrenutoOdUserId = null)
        {
            var now = DateTime.Now;

            var run = new AutoDodjelaTestovaRun
            {
                DATUM_POKRETANJA = now,
                TIP_POKRETANJA = tipPokretanja,
                POKRENUTO_OD_USER_ID = pokrenutoOdUserId,
                PREDMET_ID = predmetId,
                STATUS = "U_TOKU"
            };

            _dbContext.AUTO_DODJELA_TESTOVA_RUN.Add(run);
            await _dbContext.SaveChangesAsync();

            try
            {
                // Klase testova
                var klasaSticanjeId = await _dbContext.TEST_KLASE
                    .Where(x =>
                        x.AKTIVNO == "Y" &&
                        x.SIFRA == "STICANJE")
                    .Select(x => x.ID)
                    .FirstOrDefaultAsync();

                var klasaObnovaId = await _dbContext.TEST_KLASE
                    .Where(x =>
                        x.AKTIVNO == "Y" &&
                        x.SIFRA == "OBNOVA")
                    .Select(x => x.ID)
                    .FirstOrDefaultAsync();

                if (klasaSticanjeId == 0 || klasaObnovaId == 0)
                {
                    throw new InvalidOperationException(
                        "Nisu pronađene klase testova STICANJE i/ili OBNOVA.");
                }

                // Aktivni predmeti povezani s V3 obukama
                var predmetiQuery = _dbContext.PREDMETI
                    .Where(x =>
                        x.AKTIVNO == "Y" &&
                        x.V3_ID_OBUKE.HasValue);

                if (predmetId.HasValue)
                {
                    predmetiQuery = predmetiQuery
                        .Where(x => x.ID == predmetId.Value);
                }

                var predmeti = await predmetiQuery
                    .OrderBy(x => x.NAZIV)
                    .ToListAsync();

                run.UKUPNO_PREDMETA = predmeti.Count;

                foreach (var predmet in predmeti)
                {
                    // 1. Dohvat statusa obuke iz V3
                    var apiResponse = await _v3MatrixApiService
                        .GetRadniciProgramStatusAsync(
                            predmet.V3_ID_OBUKE!.Value);

                    var v3Statusi = apiResponse?.Users
                        ?? new List<V3RadnikProgramStatusDto>();

                    if (!v3Statusi.Any())
                        continue;

                    var radnikIds = v3Statusi
                        .Select(x => x.RadnikId)
                        .Distinct()
                        .ToList();

                    // 2. Samo aktivni interni LMS korisnici
                    var sviInterniKorisnici = await _dbContext.KORISNICI
                        .Where(x =>
                            x.AKTIVAN == "Y" &&
                            x.TIP_KORISNIKA_ID == InterniKorisnikTipId &&
                            x.ACCESS_KEY.HasValue)
                        .ToListAsync();

                    var radnikIdSet = radnikIds.ToHashSet();

                    var korisnici = sviInterniKorisnici
                        .Where(x =>
                            x.ACCESS_KEY.HasValue &&
                            radnikIdSet.Contains(x.ACCESS_KEY.Value))
                        .ToList();

                    if (!korisnici.Any())
                        continue;

                    run.UKUPNO_KORISNIKA_U_MATRICI += korisnici.Count;

                    var korisniciByAccessKey = korisnici
                        .Where(x => x.ACCESS_KEY.HasValue)
                        .GroupBy(x => x.ACCESS_KEY!.Value)
                        .ToDictionary(x => x.Key, x => x.First());

                    var nemaValidnogTestaLog =
                        new Dictionary<string, AutoDodjelaTestovaLog>();

                    // Zaštita od duplih V3 zapisa u istom pokretanju
                    var obradjeniKorisnici = new HashSet<int>();

                    // 3. Obrada statusa svakog korisnika
                    foreach (var status in v3Statusi)
                    {
                        if (!korisniciByAccessKey.TryGetValue(
                                status.RadnikId,
                                out var korisnik))
                        {
                            continue;
                        }

                        // Isti korisnik za ovaj predmet obrađuje se jednom
                        if (!obradjeniKorisnici.Add(korisnik.ID))
                            continue;

                        int? testKlasaId = null;
                        string? razlogDodjele = null;
                        DateTime? rokZaPolaganje = null;

                        // 4. Određivanje potrebne klase testa

                        // Nema V3 evidencije
                        if (!status.DatumVaziDo.HasValue)
                        {
                            testKlasaId = klasaSticanjeId;
                            razlogDodjele = "NEMA_EVIDENCIJE";
                            rokZaPolaganje = now.AddMonths(1);
                        }
                        // Postojeća V3 obuka je istekla
                        else if (status.Isteklo)
                        {
                            testKlasaId = klasaSticanjeId;
                            razlogDodjele = "ISTEKLO";
                            rokZaPolaganje = now.AddMonths(1);
                        }
                        // Postojeća V3 obuka ističe za 60 dana ili manje
                        else if (
                            status.DanaDoIsteka.HasValue &&
                            status.DanaDoIsteka.Value <= 60)
                        {
                            testKlasaId = klasaObnovaId;
                            razlogDodjele = "ISTICE_60_DANA";
                            rokZaPolaganje = status.DatumVaziDo.Value;
                        }

                        // Nije potrebna nova obuka
                        if (!testKlasaId.HasValue ||
                            !rokZaPolaganje.HasValue)
                        {
                            continue;
                        }

                        // =====================================================
                        // 5. ZAŠTITA OD PONOVNE AUTOMATSKE DODJELE
                        // =====================================================
                        //
                        // Provjeravamo prethodne dodjele istog PREDMETA,
                        // bez obzira na:
                        //
                        // - klasu STICANJE ili OBNOVA
                        // - ručnu ili automatsku dodjelu
                        // - aktivan ili neaktivan status
                        // - istek roka
                        // - broj iskorištenih pokušaja
                        //
                        // Cilj:
                        // Ne dodjeljivati automatski novu obavezu ako
                        // je korisnik već dobio dodjelu za isti V3 ciklus.
                        //
                        // Ako V3 ima datum isteka, kao približnu granicu
                        // ciklusa uzimamo 60 dana prije tog isteka.
                        //
                        // Ako V3 nema evidenciju, sve prethodne dodjele
                        // tog predmeta predstavljaju zaštitu od ponavljanja.
                        //
                        // =====================================================

                        var prethodneDodjeleQuery =
                            _dbContext.DODIJELJENI_TESTOVI
                                .AsNoTracking()
                                .Where(x =>
                                    x.KORISNIK_ID == korisnik.ID &&
                                    x.TEST.PREDMET_ID == predmet.ID);

                        if (status.DatumVaziDo.HasValue)
                        {
                            var pocetakCiklusa =
                                status.DatumVaziDo.Value.AddDays(-60);

                            prethodneDodjeleQuery =
                                prethodneDodjeleQuery.Where(x =>
                                    x.DATUM_DODJELE >= pocetakCiklusa);
                        }

                        var postojiPrethodnaDodjela =
                            await prethodneDodjeleQuery.AnyAsync();

                        if (postojiPrethodnaDodjela)
                        {
                            run.UKUPNO_VEC_DODIJELJENO++;
                            continue;
                        }

                        // =====================================================
                        // 6. PRONALAZAK VALIDNOG TESTA
                        // =====================================================

                        var dostupniTestovi = await _dbContext.TESTOVI
                            .Where(x =>
                                x.PREDMET_ID == predmet.ID &&
                                x.TEST_KLASA_ID == testKlasaId.Value &&
                                x.AKTIVNO == "Y" &&
                                x.STATUS == "ACTIVE" &&
                                x.DATUM_OD <= now &&
                                x.DATUM_DO >= now)
                            .ToListAsync();

                        if (!dostupniTestovi.Any())
                        {
                            run.UKUPNO_NEMA_VALIDNOG_TESTA++;

                            var logKey =
                                $"{predmet.ID}_{testKlasaId.Value}_{razlogDodjele}";

                            if (!nemaValidnogTestaLog.ContainsKey(logKey))
                            {
                                var log = new AutoDodjelaTestovaLog
                                {
                                    RUN_ID = run.ID,
                                    PREDMET_ID = predmet.ID,
                                    TEST_KLASA_ID = testKlasaId.Value,

                                    TIP_DOGADJAJA = "NEMA_VALIDNOG_TESTA",
                                    RAZLOG = razlogDodjele,

                                    POGODJENO_KORISNIKA = 1,
                                    DATUM_KREIRANJA = DateTime.Now,

                                    PORUKA =
                                        $"Nema aktivnog validnog testa za predmet '{predmet.NAZIV}' i traženu klasu testa."
                                };

                                nemaValidnogTestaLog.Add(logKey, log);

                                _dbContext.AUTO_DODJELA_TESTOVA_LOG.Add(log);
                            }
                            else
                            {
                                nemaValidnogTestaLog[logKey]
                                    .POGODJENO_KORISNIKA =
                                    (nemaValidnogTestaLog[logKey]
                                        .POGODJENO_KORISNIKA ?? 0) + 1;
                            }

                            continue;
                        }

                        // 7. Nasumični izbor validnog testa
                        var test = dostupniTestovi[
                            Random.Shared.Next(dostupniTestovi.Count)];

                        // 8. Kreiranje automatske dodjele
                        var dodjela = new DodijeljeniTest
                        {
                            TEST_ID = test.ID,
                            KORISNIK_ID = korisnik.ID,
                            DODIJELIO_USER_ID = null,

                            DATUM_DODJELE = now,
                            ROK_ZA_POLAGANJE = rokZaPolaganje.Value,

                            STATUS = "DODIJELJEN",
                            AUTO_DODJELA = "Y",
                            RAZLOG_DODJELE = razlogDodjele,
                            TEST_KLASA_ID = testKlasaId.Value,

                            AKTIVNO = "Y",
                            NAPOMENA = "Automatska dodjela testa."
                        };

                        _dbContext.DODIJELJENI_TESTOVI.Add(dodjela);

                        // 9. Log uspješne dodjele
                        _dbContext.AUTO_DODJELA_TESTOVA_LOG.Add(
                            new AutoDodjelaTestovaLog
                            {
                                RUN_ID = run.ID,
                                PREDMET_ID = predmet.ID,
                                KORISNIK_ID = korisnik.ID,
                                TEST_ID = test.ID,
                                TEST_KLASA_ID = testKlasaId.Value,

                                TIP_DOGADJAJA = "DODIJELJENO",
                                RAZLOG = razlogDodjele,
                                POGODJENO_KORISNIKA = 1,
                                DATUM_KREIRANJA = DateTime.Now,

                                PORUKA =
                                    $"Test '{test.NAZIV}' je automatski dodijeljen korisniku."
                            });

                        run.UKUPNO_DODIJELJENO++;

                        if (testKlasaId.Value == klasaSticanjeId)
                        {
                            run.UKUPNO_STICANJE++;
                        }

                        if (testKlasaId.Value == klasaObnovaId)
                        {
                            run.UKUPNO_OBNOVA++;
                        }

                        await SendAssignmentEmailPlaceholderAsync(
                            korisnik.ID,
                            test.ID);
                    }

                    // Snimanje dodjela i logova za obrađeni predmet
                    await _dbContext.SaveChangesAsync();
                }

                // 10. Završetak pokretanja
                run.DATUM_ZAVRSETKA = DateTime.Now;

                run.STATUS =
                    run.UKUPNO_GRESAKA > 0 ||
                    run.UKUPNO_NEMA_VALIDNOG_TESTA > 0
                        ? "DJELIMICNO"
                        : "USPJESNO";

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                run.DATUM_ZAVRSETKA = DateTime.Now;
                run.STATUS = "GRESKA";
                run.UKUPNO_GRESAKA++;
                run.NAPOMENA = ex.Message;

                _dbContext.AUTO_DODJELA_TESTOVA_LOG.Add(
                    new AutoDodjelaTestovaLog
                    {
                        RUN_ID = run.ID,
                        PREDMET_ID = predmetId,
                        TIP_DOGADJAJA = "GRESKA",
                        DATUM_KREIRANJA = DateTime.Now,
                        PORUKA = ex.ToString()
                    });

                await _dbContext.SaveChangesAsync();

                throw;
            }
        }


        private async Task SendAssignmentEmailPlaceholderAsync(int korisnikId, int testId)
        {
            // TODO: Ovdje kasnije ide slanje email obavijesti korisniku.
            await Task.CompletedTask;
        }
    }
}