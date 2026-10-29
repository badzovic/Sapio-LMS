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


        public async Task RunAutoAssignmentsAsync(int? predmetId = null, string tipPokretanja = "MANUAL", string? pokrenutoOdUserId = null)
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
                // ============================================================
                // 1. KLASE TESTOVA
                // ============================================================

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

                // ============================================================
                // 2. AKTIVNI PREDMETI POVEZANI S V3
                // ============================================================

                var predmetiQuery = _dbContext.PREDMETI
                    .AsNoTracking()
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

                await _dbContext.SaveChangesAsync();

                // ============================================================
                // 3. OBRADA SVAKOG PREDMETA NEZAVISNO
                // ============================================================

                foreach (var predmet in predmeti)
                {
                    try
                    {
                        // ----------------------------------------------------
                        // 3.1. DOHVAT V3 PODATAKA
                        // ----------------------------------------------------
                        //
                        // API poziv izvršavamo prije SQL transakcije.
                        // Ne držimo otvorenu transakciju dok čekamo V3 API.
                        //

                        var apiResponse = await _v3MatrixApiService
                            .GetRadniciProgramStatusAsync(
                                predmet.V3_ID_OBUKE!.Value);

                        if (apiResponse == null || apiResponse.Users == null)
                        {
                            throw new InvalidOperationException(
                                $"V3 API nije vratio validan odgovor za predmet " +
                                $"'{predmet.NAZIV}' (V3 ID: {predmet.V3_ID_OBUKE}).");
                        }

                        var v3Statusi = apiResponse.Users;

                        // ----------------------------------------------------
                        // 3.2. PRAZAN V3 ODGOVOR
                        // ----------------------------------------------------
                        //
                        // Prazan spisak ne mora predstavljati grešku.
                        // Evidentiramo ga i nastavljamo s drugim predmetom.
                        //

                        if (!v3Statusi.Any())
                        {
                            _dbContext.AUTO_DODJELA_TESTOVA_LOG.Add(
                                new AutoDodjelaTestovaLog
                                {
                                    RUN_ID = run.ID,
                                    PREDMET_ID = predmet.ID,

                                    TIP_DOGADJAJA = "PRAZAN_V3_ODGOVOR",
                                    DATUM_KREIRANJA = DateTime.Now,

                                    PORUKA =
                                        $"V3 API je vratio prazan spisak radnika " +
                                        $"za predmet '{predmet.NAZIV}' " +
                                        $"(V3 ID: {predmet.V3_ID_OBUKE})."
                                });

                            await _dbContext.SaveChangesAsync();
                            continue;
                        }

                        // ----------------------------------------------------
                        // 3.3. RADNICI IZ V3 MATRICE
                        // ----------------------------------------------------

                        var radnikIds = v3Statusi
                            .Select(x => x.RadnikId)
                            .Distinct()
                            .ToList();

                        var radnikIdSet = radnikIds.ToHashSet();

                        // ----------------------------------------------------
                        // 3.4. AKTIVNI INTERNI LMS KORISNICI
                        // ----------------------------------------------------

                        var sviInterniKorisnici = await _dbContext.KORISNICI
                            .AsNoTracking()
                            .Where(x =>
                                x.AKTIVAN == "Y" &&
                                x.TIP_KORISNIKA_ID == InterniKorisnikTipId &&
                                x.ACCESS_KEY.HasValue)
                            .ToListAsync();

                        var korisnici = sviInterniKorisnici
                            .Where(x =>
                                x.ACCESS_KEY.HasValue &&
                                radnikIdSet.Contains(x.ACCESS_KEY.Value))
                            .ToList();

                        if (!korisnici.Any())
                        {
                            continue;
                        }

                        var korisniciByAccessKey = korisnici
                            .Where(x => x.ACCESS_KEY.HasValue)
                            .GroupBy(x => x.ACCESS_KEY!.Value)
                            .ToDictionary(x => x.Key, x => x.First());

                        // ----------------------------------------------------
                        // 3.5. TRANSAKCIJA ZA KONKRETAN PREDMET
                        // ----------------------------------------------------
                        //
                        // Ako obrada ovog predmeta ne uspije,
                        // poništavamo samo njegove promjene.
                        //
                        // Prethodno uspješno obrađeni predmeti ostaju.
                        //

                        await using (var predmetTransaction =
                            await _dbContext.Database.BeginTransactionAsync())
                        {
                            // Brojač se čuva u istoj transakciji.
                            run.UKUPNO_KORISNIKA_U_MATRICI += korisnici.Count;

                            var nemaValidnogTestaLog =
                                new Dictionary<string, AutoDodjelaTestovaLog>();

                            // Zaštita od duplih V3 zapisa.
                            var obradjeniKorisnici = new HashSet<int>();

                            // ====================================================
                            // 4. OBRADA KORISNIKA ZA OVAJ PREDMET
                            // ====================================================

                            foreach (var status in v3Statusi)
                            {
                                if (!korisniciByAccessKey.TryGetValue(
                                        status.RadnikId,
                                        out var korisnik))
                                {
                                    continue;
                                }

                                if (!obradjeniKorisnici.Add(korisnik.ID))
                                {
                                    continue;
                                }

                                int? testKlasaId = null;
                                string? razlogDodjele = null;
                                DateTime? rokZaPolaganje = null;

                                // ================================================
                                // 5. ODREĐIVANJE KLASE TESTA
                                // ================================================

                                // NEMA V3 EVIDENCIJE
                                if (!status.DatumVaziDo.HasValue)
                                {
                                    testKlasaId = klasaSticanjeId;
                                    razlogDodjele = "NEMA_EVIDENCIJE";
                                    rokZaPolaganje = now.AddMonths(1);
                                }
                                // POSTOJEĆA OBUKA JE ISTEKLA
                                else if (status.Isteklo)
                                {
                                    testKlasaId = klasaSticanjeId;
                                    razlogDodjele = "ISTEKLO";
                                    rokZaPolaganje = now.AddMonths(1);
                                }
                                // OBUKA ISTIČE ZA 60 DANA ILI MANJE
                                else if (
                                    status.DanaDoIsteka.HasValue &&
                                    status.DanaDoIsteka.Value <= 60)
                                {
                                    testKlasaId = klasaObnovaId;
                                    razlogDodjele = "ISTICE_60_DANA";
                                    rokZaPolaganje = status.DatumVaziDo.Value;
                                }

                                // Nije potrebna nova obuka.
                                if (!testKlasaId.HasValue ||
                                    !rokZaPolaganje.HasValue)
                                {
                                    continue;
                                }

                                // ================================================
                                // 6. ZAŠTITA OD PONOVNE AUTOMATSKE DODJELE
                                // ================================================
                                //
                                // Provjeravamo sve prethodne dodjele istog predmeta:
                                //
                                // - STICANJE i OBNOVA
                                // - ručne i automatske
                                // - aktivne i neaktivne
                                // - istekle i nepoložene
                                //
                                // Ne dozvoljavamo novu automatsku dodjelu
                                // ako postoji dodjela u trenutnom V3 periodu.
                                //

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

                                // ================================================
                                // 7. PRONALAZAK VALIDNOG TESTA
                                // ================================================

                                var dostupniTestovi = await _dbContext.TESTOVI
                                    .AsNoTracking()
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
                                                $"Nema aktivnog validnog testa " +
                                                $"za predmet '{predmet.NAZIV}' " +
                                                $"i traženu klasu testa."
                                        };

                                        nemaValidnogTestaLog.Add(logKey, log);

                                        _dbContext.AUTO_DODJELA_TESTOVA_LOG.Add(log);
                                    }
                                    else
                                    {
                                        var log = nemaValidnogTestaLog[logKey];

                                        log.POGODJENO_KORISNIKA =
                                            (log.POGODJENO_KORISNIKA ?? 0) + 1;
                                    }

                                    continue;
                                }

                                // ================================================
                                // 8. NASUMIČNI IZBOR VALIDNOG TESTA
                                // ================================================

                                var test = dostupniTestovi[
                                    Random.Shared.Next(dostupniTestovi.Count)];

                                // ================================================
                                // 9. KREIRANJE AUTOMATSKE DODJELE
                                // ================================================

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

                                // ================================================
                                // 10. LOG USPJEŠNE DODJELE
                                // ================================================

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
                                            $"Test '{test.NAZIV}' je automatski " +
                                            $"dodijeljen korisniku."
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

                                // Trenutno placeholder.
                                await SendAssignmentEmailPlaceholderAsync(
                                    korisnik.ID,
                                    test.ID);
                            }

                            // ================================================
                            // 11. SNIMANJE JEDNOG PREDMETA
                            // ================================================

                            await _dbContext.SaveChangesAsync();

                            await predmetTransaction.CommitAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        // ====================================================
                        // 12. GREŠKA NA JEDNOM PREDMETU
                        // ====================================================
                        //
                        // Transakcija prethodnog predmeta je poništena.
                        //
                        // Čistimo EF ChangeTracker da promjene koje
                        // nisu potvrđene ne završe u narednom predmetu.
                        //

                        _dbContext.ChangeTracker.Clear();

                        // Ponovo učitavamo RUN iz baze.
                        // Tako vraćamo brojače na posljednje
                        // uspješno potvrđeno stanje.

                        run = await _dbContext.AUTO_DODJELA_TESTOVA_RUN
                            .FirstAsync(x => x.ID == run.ID);

                        run.UKUPNO_GRESAKA++;

                        // Log konkretnog neuspješnog predmeta.
                        _dbContext.AUTO_DODJELA_TESTOVA_LOG.Add(
                            new AutoDodjelaTestovaLog
                            {
                                RUN_ID = run.ID,
                                PREDMET_ID = predmet.ID,

                                TIP_DOGADJAJA = "GRESKA",
                                DATUM_KREIRANJA = DateTime.Now,

                                PORUKA =
                                    $"Greška prilikom automatske dodjele " +
                                    $"za predmet '{predmet.NAZIV}' " +
                                    $"(ID: {predmet.ID}, " +
                                    $"V3 ID: {predmet.V3_ID_OBUKE}). " +
                                    $"Detalji: {ex}"
                            });

                        await _dbContext.SaveChangesAsync();

                        // Nastavljamo s narednim predmetom.
                    }
                }

                // ============================================================
                // 13. ZAVRŠETAK KOMPLETNOG POKRETANJA
                // ============================================================

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
                // ============================================================
                // 14. KRITIČNA GREŠKA KOMPLETNOG POKRETANJA
                // ============================================================
                //
                // Ovdje završavaju greške koje nisu uspješno
                // obrađene na nivou pojedinačnog predmeta.
                //

                _dbContext.ChangeTracker.Clear();

                run = await _dbContext.AUTO_DODJELA_TESTOVA_RUN
                    .FirstAsync(x => x.ID == run.ID);

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