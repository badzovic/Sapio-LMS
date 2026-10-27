using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio.LMS.Data.Identity;
using SIA.Sapio_LMS.Services;

namespace SIA.Sapio_LMS.Services.InternalUsersSync
{
    public class InternalUsersSyncService
        : IInternalUsersSyncService
    {
        private readonly InternalUsersApiService _internalUsersApiService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<InternalUsersSyncService> _logger;

        public InternalUsersSyncService(
            InternalUsersApiService internalUsersApiService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext dbContext,
            ILogger<InternalUsersSyncService> logger)
        {
            _internalUsersApiService = internalUsersApiService;
            _userManager = userManager;
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<InternalUsersImportResult> RunImportAsync()
        {
            var result = new InternalUsersImportResult();

            /*
             * ============================================================
             * 1. PREUZIMANJE AKTIVNIH KORISNIKA IZ HRM API-JA
             * ============================================================
             */

            var apiUsers = (await _internalUsersApiService.GetUsersAsync())
                .ToList();

            if (!apiUsers.Any())
            {
                result.Failed++;

                result.ErrorMessages.Add(
                    "HRM API nije vratio nijednog aktivnog korisnika. " +
                    "Import nije izvršen.");

                _logger.LogWarning(
                    "Import internih korisnika preskočen jer HRM API " +
                    "nije vratio nijednog korisnika.");

                return result;
            }

            /*
             * Zadržavamo postojeći način kreiranja korisnika.
             * Početnu lozinku kasnije treba zamijeniti sigurnijim
             * postupkom postavljanja lozinke.
             */
            const string defaultPassword = "Sarajevo2026!";

            foreach (var apiUser in apiUsers)
            {
                await using var transaction =
                    await _dbContext.Database.BeginTransactionAsync();

                try
                {
                    _logger.LogInformation(
                        "=== START IMPORT HRM_ID={HrmId} ===",
                        apiUser.HrmId);

                    /*
                     * ========================================================
                     * 2. PROVJERA POSTOJEĆEG INTERNOG KORISNIKA
                     * ========================================================
                     *
                     * HRM_ID je ključ za povezivanje radnika.
                     *
                     * Ako korisnik već postoji, ne kreiramo novi račun.
                     * Njegove podatke i eventualnu reaktivaciju
                     * obrađuje RunSyncAsync().
                     */

                    var postojiUKorisnici = await _dbContext.KORISNICI
                        .AnyAsync(x => x.HRM_ID == apiUser.HrmId);

                    if (postojiUKorisnici)
                    {
                        result.Skipped++;

                        await transaction.RollbackAsync();

                        continue;
                    }

                    /*
                     * ========================================================
                     * 3. PRIPREMA PODATAKA ZA PODUDARANJE
                     * ========================================================
                     */

                    var apiImeNormalized =
                        NormalizeName(apiUser.Ime);

                    var apiPrezimeNormalized =
                        NormalizeName(apiUser.Prezime);

                    var apiImeOcaNormalized =
                        NormalizeName(apiUser.ImeOca);

                    /*
                     * Ne pokušavamo automatsku konverziju na osnovu
                     * praznog imena ili prezimena.
                     */

                    if (string.IsNullOrWhiteSpace(apiImeNormalized) ||
                        string.IsNullOrWhiteSpace(apiPrezimeNormalized))
                    {
                        result.Skipped++;

                        _logger.LogWarning(
                            "IMPORT PRESKOČEN: HRM_ID={HrmId} " +
                            "nema validno ime ili prezime.",
                            apiUser.HrmId);

                        await transaction.RollbackAsync();

                        continue;
                    }

                    /*
                     * ========================================================
                     * 4. PRETRAGA EKSTERNIH / SEZONSKIH KORISNIKA
                     * ========================================================
                     */

                    var moguciKandidati = await _dbContext.KORISNICI
                        .Where(x =>
                            x.TIP_KORISNIKA_ID != 1 &&
                            x.IME != null &&
                            x.PREZIME != null &&
                            x.IME.Trim().ToUpper() == apiImeNormalized &&
                            x.PREZIME.Trim().ToUpper() == apiPrezimeNormalized)
                        .ToListAsync();

                    var kandidati = moguciKandidati
                        .Where(x =>
                        {
                            if (string.IsNullOrWhiteSpace(apiUser.ImeOca))
                            {
                                return true;
                            }

                            return !string.IsNullOrWhiteSpace(x.IME_OCA) &&
                                   NormalizeName(x.IME_OCA) ==
                                   apiImeOcaNormalized;
                        })
                        .ToList();

                    /*
                     * ========================================================
                     * 5. POSTOJEĆI EKSTERNI / SEZONSKI KORISNIK
                     * ========================================================
                     */

                    if (kandidati.Count == 1)
                    {
                        var postojeciKorisnik = kandidati.Single();

                        if (string.IsNullOrWhiteSpace(
                                postojeciKorisnik.USER_ID))
                        {
                            throw new Exception(
                                $"Postojeći korisnik ID={postojeciKorisnik.ID} " +
                                "nema povezan Identity USER_ID.");
                        }

                        var postojeciIdentityUser =
                            await _userManager.FindByIdAsync(
                                postojeciKorisnik.USER_ID);

                        if (postojeciIdentityUser == null)
                        {
                            throw new Exception(
                                $"Identity korisnik USER_ID=" +
                                $"{postojeciKorisnik.USER_ID} nije pronađen.");
                        }

                        /*
                         * Zaštita od toga da isti Identity račun bude
                         * povezan s više redova u KORISNICI.
                         */

                        var identityVecPovezan = await _dbContext.KORISNICI
                            .AnyAsync(x =>
                                x.USER_ID == postojeciIdentityUser.Id &&
                                x.ID != postojeciKorisnik.ID);

                        if (identityVecPovezan)
                        {
                            throw new InvalidOperationException(
                                $"Identity USER_ID={postojeciIdentityUser.Id} " +
                                "već je povezan s drugim LMS korisnikom. " +
                                "Automatska konverzija nije dozvoljena.");
                        }

                        /*
                         * Čuvamo prethodne podatke za audit.
                         */

                        var stariTipKorisnikaId =
                            postojeciKorisnik.TIP_KORISNIKA_ID;

                        var staroIme = postojeciKorisnik.IME;
                        var staroPrezime = postojeciKorisnik.PREZIME;
                        var staroImeOca = postojeciKorisnik.IME_OCA;

                        var novoIme = NormalizeValue(apiUser.Ime);
                        var novoPrezime = NormalizeValue(apiUser.Prezime);
                        var novoImeOca = NormalizeValue(apiUser.ImeOca);
                        var noviEmail = NormalizeValue(apiUser.Email);

                        /*
                         * ====================================================
                         * 6. KONVERZIJA U INTERNOG KORISNIKA
                         * ====================================================
                         */

                        postojeciKorisnik.TIP_KORISNIKA_ID = 1;
                        postojeciKorisnik.HRM_ID = apiUser.HrmId;
                        postojeciKorisnik.ACCESS_KEY = apiUser.AccessKey;
                        postojeciKorisnik.IME = novoIme;
                        postojeciKorisnik.PREZIME = novoPrezime;
                        postojeciKorisnik.IME_OCA = novoImeOca;
                        postojeciKorisnik.EMAIL = noviEmail;
                        postojeciKorisnik.KOMPANIJA =
                            apiUser.Kompanija ?? "MAS";

                        postojeciKorisnik.AKTIVAN = "Y";

                        /*
                         * ====================================================
                         * 7. REAKTIVACIJA POSTOJEĆEG IDENTITY RAČUNA
                         * ====================================================
                         *
                         * Koristimo postojeći USER_ID.
                         * Ne kreiramo novi račun.
                         */

                        postojeciIdentityUser.FirstName =
                            novoIme ?? string.Empty;

                        postojeciIdentityUser.LastName =
                            novoPrezime ?? string.Empty;

                        postojeciIdentityUser.Email = noviEmail;

                        postojeciIdentityUser.IsExternalCollaborator = false;

                        postojeciIdentityUser.IsActive = true;
                        postojeciIdentityUser.IsDeleted = false;

                        postojeciIdentityUser.LockoutEnabled = true;

                        /*
                         * Uklanjamo samo trajni lockout koji je
                         * postavljen prilikom deaktivacije.
                         */
                        if (postojeciIdentityUser.LockoutEnd ==
                            DateTimeOffset.MaxValue)
                        {
                            postojeciIdentityUser.LockoutEnd = null;
                        }

                        var identityUpdateResult =
                            await _userManager.UpdateAsync(
                                postojeciIdentityUser);

                        if (!identityUpdateResult.Succeeded)
                        {
                            throw new Exception(
                                "Identity update nije uspio: " +
                                string.Join(
                                    " | ",
                                    identityUpdateResult.Errors
                                        .Select(x => x.Description)));
                        }

                        /*
                         * ====================================================
                         * 8. AUDIT KONVERZIJE
                         * ====================================================
                         */

                        var nacinPovezivanja =
                            string.IsNullOrWhiteSpace(apiUser.ImeOca)
                                ? "IME + PREZIME"
                                : "IME + PREZIME + IME OCA";

                        var evidencija =
                            new EvidencijaPromjeneTipaKorisnika
                            {
                                KORISNIK_ID = postojeciKorisnik.ID,
                                USER_ID = postojeciKorisnik.USER_ID,
                                HRM_ID = apiUser.HrmId,

                                STARI_TIP_KORISNIKA_ID =
                                    stariTipKorisnikaId,

                                NOVI_TIP_KORISNIKA_ID = 1,

                                NACIN_POVEZIVANJA =
                                    nacinPovezivanja,

                                STATUS = "USPJESNA_KONVERZIJA",

                                STARO_IME = staroIme,
                                STARO_PREZIME = staroPrezime,
                                STARO_IME_OCA = staroImeOca,

                                NOVO_IME = novoIme,
                                NOVO_PREZIME = novoPrezime,
                                NOVO_IME_OCA = novoImeOca,

                                OPIS =
                                    "Postojeći eksterni ili sezonski korisnik " +
                                    "automatski je pretvoren u internog korisnika. " +
                                    "Identity račun je aktiviran i soft-delete uklonjen.",

                                DATUM_KREIRANJA = DateTime.UtcNow,
                                PREGLEDANO = false,
                                PREGLEDAO_USER_ID = null,
                                DATUM_PREGLEDA = null
                            };

                        _dbContext
                            .EVIDENCIJA_PROMJENA_TIPA_KORISNIKA
                            .Add(evidencija);

                        await _dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();

                        result.Converted++;

                        _logger.LogInformation(
                            "USPJEŠNA KONVERZIJA: KORISNIK_ID={KorisnikId}, " +
                            "HRM_ID={HrmId}",
                            postojeciKorisnik.ID,
                            apiUser.HrmId);

                        continue;
                    }

                    /*
                     * ========================================================
                     * 9. VIŠE MOGUĆIH KANDIDATA
                     * ========================================================
                     */

                    if (kandidati.Count > 1)
                    {
                        result.Ambiguous++;

                        _logger.LogWarning(
                            "NEJASNO PODUDARANJE: HRM_ID={HrmId}, " +
                            "{Ime} {Prezime}, pronađeno {Broj} kandidata.",
                            apiUser.HrmId,
                            apiUser.Ime,
                            apiUser.Prezime,
                            kandidati.Count);

                        await transaction.RollbackAsync();

                        continue;
                    }

                    /*
                     * ========================================================
                     * 10. POTPUNO NOVI KORISNIK
                     * ========================================================
                     */

                    var cleanEmail =
                        NormalizeValue(apiUser.Email);

                    var userName =
                        !string.IsNullOrWhiteSpace(cleanEmail)
                            ? cleanEmail
                            : $"hrm_{apiUser.HrmId}";

                    /*
                     * Ako postoji Identity račun s istim UserName,
                     * ne smijemo ga automatski povezati na osnovu
                     * korisničkog imena ili e-mail adrese.
                     */

                    var postojeciIdentity =
                        await _userManager.FindByNameAsync(userName);

                    if (postojeciIdentity != null)
                    {
                        var vecPovezan = await _dbContext.KORISNICI
                            .AnyAsync(x =>
                                x.USER_ID == postojeciIdentity.Id);

                        if (vecPovezan)
                        {
                            throw new InvalidOperationException(
                                $"Identity račun {postojeciIdentity.Id} " +
                                $"već je povezan s drugim LMS korisnikom. " +
                                $"HRM_ID={apiUser.HrmId}.");
                        }

                        throw new InvalidOperationException(
                            $"Identity račun s UserName='{userName}' već postoji, " +
                            $"ali nema potvrđenu vezu s HRM_ID={apiUser.HrmId}. " +
                            "Potrebna je ručna provjera.");
                    }

                    /*
                     * ========================================================
                     * 11. KREIRANJE IDENTITY KORISNIKA
                     * ========================================================
                     */

                    var identityUser = new ApplicationUser
                    {
                        FirstName =
                            apiUser.Ime?.Trim() ?? string.Empty,

                        LastName =
                            apiUser.Prezime?.Trim() ?? string.Empty,

                        IsExternalCollaborator = false,

                        IsActive = true,
                        IsDeleted = false,

                        CreatedAt = DateTime.UtcNow,

                        UserName = userName,
                        Email = cleanEmail,

                        EmailConfirmed = false,
                        PhoneNumberConfirmed = false,
                        TwoFactorEnabled = false,

                        LockoutEnabled = true,
                        AccessFailedCount = 0,

                        PhoneNumber = null,
                        LockoutEnd = null
                    };

                    var createResult =
                        await _userManager.CreateAsync(
                            identityUser,
                            defaultPassword);

                    if (!createResult.Succeeded)
                    {
                        var createErrors = string.Join(
                            " | ",
                            createResult.Errors.Select(
                                e => $"{e.Code}: {e.Description}"));

                        throw new Exception(
                            $"CREATE USER FAILED: {createErrors}");
                    }

                    /*
                     * ========================================================
                     * 12. STUDENT ROLA
                     * ========================================================
                     */

                    var addRoleResult =
                        await _userManager.AddToRoleAsync(
                            identityUser,
                            "Student");

                    if (!addRoleResult.Succeeded)
                    {
                        var roleErrors = string.Join(
                            " | ",
                            addRoleResult.Errors.Select(
                                e => $"{e.Code}: {e.Description}"));

                        throw new Exception(
                            $"ADD STUDENT ROLE FAILED: {roleErrors}");
                    }

                    /*
                     * ========================================================
                     * 13. KREIRANJE KORISNIKA U POSLOVNOJ TABELI
                     * ========================================================
                     */

                    var noviKorisnik = new Korisnik
                    {
                        IME = NormalizeValue(apiUser.Ime),
                        PREZIME = NormalizeValue(apiUser.Prezime),
                        IME_OCA = NormalizeValue(apiUser.ImeOca),

                        ACCESS_KEY = apiUser.AccessKey,
                        USER_ID = identityUser.Id,
                        EMAIL = cleanEmail,

                        KOMPANIJA = apiUser.Kompanija ?? "MAS",
                        HRM_ID = apiUser.HrmId,

                        TIP_KORISNIKA_ID = 1,
                        AKTIVAN = "Y"
                    };

                    _dbContext.KORISNICI.Add(noviKorisnik);

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    result.Imported++;

                    _logger.LogInformation(
                        "=== END IMPORT SUCCESS HRM_ID={HrmId} ===",
                        apiUser.HrmId);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    result.Failed++;

                    result.ErrorMessages.Add(
                        $"HRM_ID={apiUser.HrmId}: {ex.Message}");

                    _logger.LogError(
                        ex,
                        "=== END IMPORT FAILED HRM_ID={HrmId} ===",
                        apiUser.HrmId);
                }
                finally
                {
                    _dbContext.ChangeTracker.Clear();
                }
            }

            return result;
        }

        public async Task<InternalUsersSyncResult> RunSyncAsync()
        {
            var result = new InternalUsersSyncResult();

            try
            {
                /*
                 * API vraća samo aktivne i neobrisane HRM korisnike.
                 */
                var apiUsers =
                    await _internalUsersApiService.GetUsersAsync();

                if (apiUsers == null || !apiUsers.Any())
                {
                    result.Failed++;
                    result.ErrorMessages.Add(
                        "HRM API nije vratio aktivne korisnike. Sync nije izvršen.");

                    _logger.LogWarning(
                        "Sinhronizacija preskočena jer HRM API nije vratio korisnike.");

                    return result;
                }

                foreach (var apiUser in apiUsers)
                {
                    await using var transaction =
                        await _dbContext.Database.BeginTransactionAsync();

                    try
                    {
                        /*
                         * ====================================================
                         * 1. POSTOJEĆI KORISNIK
                         * ====================================================
                         */

                        var postojeci = await _dbContext.KORISNICI
                            .FirstOrDefaultAsync(
                                x => x.HRM_ID == apiUser.HrmId);

                        if (postojeci == null)
                        {
                            result.Missing++;

                            await transaction.RollbackAsync();
                            continue;
                        }

                        /*
                         * Osiguravamo da postoji Identity račun.
                         *
                         * Ne želimo označiti korisnika kao uspješno
                         * reaktiviranog ako nema povezan Identity račun.
                         */

                        if (string.IsNullOrWhiteSpace(postojeci.USER_ID))
                        {
                            throw new Exception(
                                $"Korisnik KORISNIK_ID={postojeci.ID}, " +
                                $"HRM_ID={apiUser.HrmId} nema USER_ID.");
                        }

                        var identityUser =
                            await _userManager.FindByIdAsync(postojeci.USER_ID);

                        if (identityUser == null)
                        {
                            throw new Exception(
                                $"Identity korisnik nije pronađen. " +
                                $"USER_ID={postojeci.USER_ID}");
                        }

                        /*
                         * ====================================================
                         * 2. PODACI IZ HRM API-JA
                         * ====================================================
                         */

                        var novoIme = NormalizeValue(apiUser.Ime);
                        var novoPrezime = NormalizeValue(apiUser.Prezime);
                        var novoImeOca = NormalizeValue(apiUser.ImeOca);
                        var noviEmail = NormalizeValue(apiUser.Email);

                        /*
                         * Čuvamo prethodne podatke za audit.
                         */

                        var staroIme = postojeci.IME;
                        var staroPrezime = postojeci.PREZIME;
                        var staroImeOca = postojeci.IME_OCA;
                        var stariEmail = postojeci.EMAIL;
                        var stariAccessKey = postojeci.ACCESS_KEY;

                        var promijenjenaPolja = new List<string>();

                        bool korisnikChanged = false;
                        bool identityChanged = false;

                        /*
                         * ====================================================
                         * 3. KORISNICI - SINHRONIZACIJA PODATAKA
                         * ====================================================
                         */

                        if (!AreEqual(postojeci.IME, novoIme))
                        {
                            postojeci.IME = novoIme;
                            promijenjenaPolja.Add("IME");
                            korisnikChanged = true;
                        }

                        if (!AreEqual(postojeci.PREZIME, novoPrezime))
                        {
                            postojeci.PREZIME = novoPrezime;
                            promijenjenaPolja.Add("PREZIME");
                            korisnikChanged = true;
                        }

                        if (!AreEqual(postojeci.IME_OCA, novoImeOca))
                        {
                            postojeci.IME_OCA = novoImeOca;
                            promijenjenaPolja.Add("IME_OCA");
                            korisnikChanged = true;
                        }

                        if (!AreEqual(postojeci.EMAIL, noviEmail))
                        {
                            postojeci.EMAIL = noviEmail;
                            promijenjenaPolja.Add("EMAIL");
                            korisnikChanged = true;
                        }

                        if (postojeci.ACCESS_KEY != apiUser.AccessKey)
                        {
                            postojeci.ACCESS_KEY = apiUser.AccessKey;
                            promijenjenaPolja.Add("ACCESS_KEY");
                            korisnikChanged = true;
                        }

                        if (postojeci.TIP_KORISNIKA_ID != 1)
                        {
                            postojeci.TIP_KORISNIKA_ID = 1;
                            promijenjenaPolja.Add("TIP_KORISNIKA_ID");
                            korisnikChanged = true;
                        }

                        /*
                         * ====================================================
                         * 4. KORISNICI - REAKTIVACIJA
                         * ====================================================
                         *
                         * Ako HRM vrati radnika među aktivnima,
                         * lokalni status mora biti Y.
                         */

                        if (postojeci.AKTIVAN != "Y")
                        {
                            postojeci.AKTIVAN = "Y";
                            promijenjenaPolja.Add("AKTIVAN");
                            korisnikChanged = true;
                        }

                        /*
                         * ====================================================
                         * 5. IDENTITY - OSNOVNI PODACI
                         * ====================================================
                         */

                        if (!AreEqual(identityUser.FirstName, novoIme))
                        {
                            identityUser.FirstName = novoIme ?? string.Empty;
                            promijenjenaPolja.Add("IDENTITY_FIRSTNAME");
                            identityChanged = true;
                        }

                        if (!AreEqual(identityUser.LastName, novoPrezime))
                        {
                            identityUser.LastName = novoPrezime ?? string.Empty;
                            promijenjenaPolja.Add("IDENTITY_LASTNAME");
                            identityChanged = true;
                        }

                        if (!AreEqual(identityUser.Email, noviEmail))
                        {
                            identityUser.Email = noviEmail;
                            promijenjenaPolja.Add("IDENTITY_EMAIL");
                            identityChanged = true;
                        }

                        /*
                         * ====================================================
                         * 6. IDENTITY - REAKTIVACIJA
                         * ====================================================
                         *
                         * Zadržavamo postojeći USER_ID.
                         * Nikada ne kreiramo novi račun pri reaktivaciji.
                         */

                        if (!identityUser.IsActive)
                        {
                            identityUser.IsActive = true;
                            promijenjenaPolja.Add("IDENTITY_ISACTIVE");
                            identityChanged = true;
                        }

                        /*
                         * Soft-delete vraćamo na false.
                         * Korisnik ponovo postaje vidljiv.
                         */

                        if (identityUser.IsDeleted)
                        {
                            identityUser.IsDeleted = false;
                            promijenjenaPolja.Add("IDENTITY_ISDELETED");
                            identityChanged = true;
                        }

                        /*
                         * LockoutEnabled ostaje true.
                         */

                        if (!identityUser.LockoutEnabled)
                        {
                            identityUser.LockoutEnabled = true;
                            promijenjenaPolja.Add("IDENTITY_LOCKOUTENABLED");
                            identityChanged = true;
                        }

                        /*
                         * Uklanjamo trajni lockout postavljen
                         * prilikom prethodne deaktivacije.
                         *
                         * Ne uklanjamo obični privremeni lockout.
                         */

                        if (identityUser.LockoutEnd == DateTimeOffset.MaxValue)
                        {
                            identityUser.LockoutEnd = null;
                            promijenjenaPolja.Add("IDENTITY_LOCKOUTEND");
                            identityChanged = true;
                        }

                        /*
                         * ====================================================
                         * 7. ČUVANJE IDENTITY PROMJENA
                         * ====================================================
                         */

                        if (identityChanged)
                        {
                            var updateResult =
                                await _userManager.UpdateAsync(identityUser);

                            if (!updateResult.Succeeded)
                            {
                                var identityErrors = string.Join(
                                    " | ",
                                    updateResult.Errors.Select(
                                        x => x.Description));

                                throw new Exception(
                                    "Identity update nije uspio: " +
                                    identityErrors);
                            }
                        }

                        /*
                         * ====================================================
                         * 8. PROVJERA PROMJENA
                         * ====================================================
                         */

                        bool hasChanges = korisnikChanged || identityChanged;

                        if (!hasChanges)
                        {
                            result.Unchanged++;

                            await transaction.RollbackAsync();
                            continue;
                        }

                        var promijenjenaPoljaText =
                            string.Join(", ", promijenjenaPolja);

                        /*
                         * ====================================================
                         * 9. AUDIT SINHRONIZACIJE
                         * ====================================================
                         */

                        var auditZapis =
                            new EvidencijaSinhronizacijeKorisnika
                            {
                                KORISNIK_ID = postojeci.ID,
                                USER_ID = postojeci.USER_ID,
                                HRM_ID = apiUser.HrmId,

                                STARO_IME = staroIme,
                                NOVO_IME = postojeci.IME,

                                STARO_PREZIME = staroPrezime,
                                NOVO_PREZIME = postojeci.PREZIME,

                                STARO_IME_OCA = staroImeOca,
                                NOVO_IME_OCA = postojeci.IME_OCA,

                                STARI_EMAIL = stariEmail,
                                NOVI_EMAIL = postojeci.EMAIL,

                                STARI_ACCESS_KEY = stariAccessKey,
                                NOVI_ACCESS_KEY = postojeci.ACCESS_KEY,

                                PROMIJENJENA_POLJA = promijenjenaPoljaText,

                                OPIS =
                                    "Sinhronizovani podaci internog korisnika " +
                                    "iz HRM sistema. Promijenjena polja: " +
                                    promijenjenaPoljaText + ".",

                                DATUM_KREIRANJA = DateTime.Now,

                                PREGLEDANO = false,
                                PREGLEDAO_USER_ID = null,
                                DATUM_PREGLEDA = null
                            };

                        _dbContext
                            .EVIDENCIJA_SINHRONIZACIJE_KORISNIKA
                            .Add(auditZapis);

                        /*
                         * ====================================================
                         * 10. ČUVANJE TRANSAKCIJE
                         * ====================================================
                         */

                        await _dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();

                        result.Updated++;

                        _logger.LogInformation(
                            "SINHRONIZOVAN INTERNI KORISNIK: " +
                            "KORISNIK_ID={KorisnikId}, HRM_ID={HrmId}, " +
                            "Promjene={Promjene}",
                            postojeci.ID,
                            apiUser.HrmId,
                            promijenjenaPoljaText);
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();

                        result.Failed++;

                        result.ErrorMessages.Add(
                            $"HRM_ID={apiUser.HrmId}: {ex.Message}");

                        _logger.LogError(
                            ex,
                            "Greška pri sinhronizaciji korisnika " +
                            "HRM_ID={HrmId}",
                            apiUser.HrmId);
                    }
                    finally
                    {
                        _dbContext.ChangeTracker.Clear();
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(
                    ex,
                    "HRM API nije uspješno vratio korisnike " +
                    "tokom sinhronizacije.");

                result.Failed++;

                result.ErrorMessages.Add(
                    "HRM servis trenutno nije dostupan: " +
                    ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Opšta greška tokom sinhronizacije internih korisnika.");

                result.Failed++;

                result.ErrorMessages.Add(
                    "Opšta greška tokom sinhronizacije: " +
                    ex.Message);
            }

            return result;
        }

        public async Task<InternalUsersDeactivationResult> RunDeactivationAsync()
        {
            var result = new InternalUsersDeactivationResult();

            try
            {
                /*
                 * ============================================================
                 * 1. PREUZIMANJE AKTIVNIH KORISNIKA IZ HRM API-JA
                 * ============================================================
                 */

                var apiUsers =
                    (await _internalUsersApiService.GetUsersAsync())
                    .ToList();

                /*
                 * Zaštita od masovne deaktivacije ako API vrati
                 * praznu listu korisnika.
                 */
                if (!apiUsers.Any())
                {
                    result.ApiReturnedNoUsers = true;

                    _logger.LogWarning(
                        "Deaktivacija nije izvršena jer HRM API nije " +
                        "vratio nijednog korisnika.");

                    return result;
                }

                var activeHrmIds = apiUsers
                    .Select(x => x.HrmId)
                    .ToHashSet();

                /*
                 * ============================================================
                 * 2. LOKALNI INTERNI KORISNICI
                 * ============================================================
                 *
                 * AsNoTracking koristimo jer svakog korisnika
                 * posebno učitavamo unutar njegove transakcije.
                 */

                var localInternalUsers = await _dbContext.KORISNICI
                    .AsNoTracking()
                    .Where(x =>
                        x.TIP_KORISNIKA_ID == 1 &&
                        x.HRM_ID != null)
                    .Select(x => new
                    {
                        x.ID,
                        x.HRM_ID
                    })
                    .ToListAsync();

                foreach (var localUser in localInternalUsers)
                {
                    /*
                     * Ako je korisnik pronađen među aktivnim
                     * radnicima HRM-a, ne deaktiviramo ga.
                     */
                    if (activeHrmIds.Contains(localUser.HRM_ID!.Value))
                    {
                        continue;
                    }

                    await using var transaction =
                        await _dbContext.Database.BeginTransactionAsync();

                    try
                    {
                        /*
                         * ====================================================
                         * 3. UČITAVANJE KORISNIKA
                         * ====================================================
                         */

                        var korisnik = await _dbContext.KORISNICI
                            .FirstOrDefaultAsync(x => x.ID == localUser.ID);

                        if (korisnik == null)
                        {
                            await transaction.RollbackAsync();
                            continue;
                        }

                        /*
                         * Dodatna provjera tipa i HRM ID-a.
                         */
                        if (korisnik.TIP_KORISNIKA_ID != 1 ||
                            korisnik.HRM_ID == null ||
                            activeHrmIds.Contains(korisnik.HRM_ID.Value))
                        {
                            await transaction.RollbackAsync();
                            continue;
                        }

                        /*
                         * ====================================================
                         * 4. POSTOJEĆI STATUSI
                         * ====================================================
                         */

                        bool? stariLmsStatus = korisnik.AKTIVAN switch
                        {
                            "Y" => true,
                            "N" => false,
                            _ => null
                        };

                        bool? stariIdentityStatus = null;
                        bool? noviIdentityStatus = null;

                        bool lmsChanged = false;
                        bool identityChanged = false;

                        bool stvarnaDeaktivacija = false;

                        string auditOpis = string.Empty;

                        /*
                         * ====================================================
                         * 5. PROVJERA IDENTITY KORISNIKA
                         * ====================================================
                         */

                        ApplicationUser? identityUser = null;

                        if (!string.IsNullOrWhiteSpace(korisnik.USER_ID))
                        {
                            identityUser = await _userManager.FindByIdAsync(
                                korisnik.USER_ID);
                        }

                        if (identityUser != null)
                        {
                            stariIdentityStatus = identityUser.IsActive;
                            noviIdentityStatus = identityUser.IsActive;
                        }

                        /*
                         * ====================================================
                         * 6. UTVRĐIVANJE DEAKTIVACIJE
                         * ====================================================
                         *
                         * Audit deaktivacije pravimo samo kada
                         * korisnik prelazi iz aktivnog u neaktivno stanje.
                         *
                         * Ako je već neaktivan, a neko polje nije
                         * usklađeno, radimo samo korekciju statusa.
                         */

                        stvarnaDeaktivacija =
                            korisnik.AKTIVAN == "Y" ||
                            (identityUser != null && identityUser.IsActive);

                        /*
                         * ====================================================
                         * 7. KORISNICI - POSLOVNA TABELA
                         * ====================================================
                         */

                        if (korisnik.AKTIVAN != "N")
                        {
                            korisnik.AKTIVAN = "N";
                            lmsChanged = true;
                        }

                        /*
                         * ====================================================
                         * 8. ASP.NET IDENTITY
                         * ====================================================
                         */

                        if (identityUser == null)
                        {
                            /*
                             * Korisnik nema povezan Identity račun
                             * ili povezani račun nije pronađen.
                             *
                             * Poslovni status ipak deaktiviramo.
                             */

                            if (lmsChanged)
                            {
                                result.SkippedWithoutIdentity++;
                            }

                            auditOpis =
                                "Korisnik više nije pronađen među aktivnim " +
                                "internim radnicima u HRM sistemu. " +
                                "Deaktiviran je u tabeli KORISNICI, " +
                                "ali povezani Identity korisnik ne postoji.";

                            _logger.LogWarning(
                                "Korisnik KORISNIK_ID={KorisnikId}, " +
                                "HRM_ID={HrmId} nema povezan Identity račun.",
                                korisnik.ID,
                                korisnik.HRM_ID);
                        }
                        else
                        {
                            /*
                             * IsActive
                             */
                            if (identityUser.IsActive)
                            {
                                identityUser.IsActive = false;
                                identityChanged = true;
                            }

                            /*
                             * IsDeleted - soft delete
                             */
                            if (!identityUser.IsDeleted)
                            {
                                identityUser.IsDeleted = true;
                                identityChanged = true;
                            }

                            /*
                             * LockoutEnabled
                             */
                            if (!identityUser.LockoutEnabled)
                            {
                                identityUser.LockoutEnabled = true;
                                identityChanged = true;
                            }

                            /*
                             * Trajno zaključavanje do reaktivacije.
                             */
                            if (identityUser.LockoutEnd != DateTimeOffset.MaxValue)
                            {
                                identityUser.LockoutEnd = DateTimeOffset.MaxValue;
                                identityChanged = true;
                            }

                            noviIdentityStatus = false;

                            auditOpis =
                                "Korisnik više nije pronađen među aktivnim " +
                                "internim radnicima u HRM sistemu. " +
                                "Deaktiviran je u tabeli KORISNICI, " +
                                "Identity račun je deaktiviran, " +
                                "označen kao obrisan i zaključan.";

                            /*
                             * Identity ažuriramo samo kada postoji promjena.
                             */
                            if (identityChanged)
                            {
                                var updateResult =
                                    await _userManager.UpdateAsync(identityUser);

                                if (!updateResult.Succeeded)
                                {
                                    throw new Exception(
                                        "Identity deaktivacija nije uspjela: " +
                                        string.Join(
                                            " | ",
                                            updateResult.Errors.Select(
                                                x => x.Description)));
                                }
                            }
                        }

                        /*
                         * ====================================================
                         * 9. KORISNIK JE VEĆ POTPUNO DEAKTIVIRAN
                         * ====================================================
                         */

                        if (!lmsChanged && !identityChanged)
                        {
                            result.AlreadyInactive++;

                            await transaction.RollbackAsync();

                            continue;
                        }

                        /*
                         * ====================================================
                         * 10. AUDIT STVARNE DEAKTIVACIJE
                         * ====================================================
                         *
                         * Audit kreiramo samo kada je korisnik
                         * prethodno bio aktivan.
                         *
                         * Obično usklađivanje statusa već neaktivnog
                         * korisnika ne kreira novi audit zapis.
                         */

                        if (stvarnaDeaktivacija)
                        {
                            var audit =
                                new EvidencijaDeaktivacijeKorisnika
                                {
                                    KORISNIK_ID = korisnik.ID,
                                    USER_ID = korisnik.USER_ID,
                                    HRM_ID = korisnik.HRM_ID,

                                    IME = korisnik.IME,
                                    PREZIME = korisnik.PREZIME,
                                    EMAIL = korisnik.EMAIL,
                                    ACCESS_KEY = korisnik.ACCESS_KEY,

                                    STARI_AKTIVAN = stariLmsStatus,
                                    NOVI_AKTIVAN = false,

                                    STARI_IDENTITY_AKTIVAN =
                                        stariIdentityStatus,

                                    NOVI_IDENTITY_AKTIVAN =
                                        noviIdentityStatus,

                                    RAZLOG_DEAKTIVACIJE =
                                        "Korisnik nije pronađen među aktivnim " +
                                        "radnicima u HRM API odgovoru.",

                                    OPIS = auditOpis,

                                    DATUM_KREIRANJA = DateTime.Now,

                                    PREGLEDANO = false,
                                    PREGLEDAO_USER_ID = null,
                                    DATUM_PREGLEDA = null
                                };

                            _dbContext
                                .EVIDENCIJA_DEAKTIVACIJE_KORISNIKA
                                .Add(audit);
                        }

                        /*
                         * ====================================================
                         * 11. ČUVANJE PROMJENA
                         * ====================================================
                         */

                        await _dbContext.SaveChangesAsync();

                        await transaction.CommitAsync();

                        if (stvarnaDeaktivacija)
                        {
                            result.Deactivated++;

                            _logger.LogWarning(
                                "DEAKTIVIRAN INTERNI KORISNIK: " +
                                "KORISNIK_ID={KorisnikId}, " +
                                "HRM_ID={HrmId}, IME={Ime}, PREZIME={Prezime}",
                                korisnik.ID,
                                korisnik.HRM_ID,
                                korisnik.IME,
                                korisnik.PREZIME);
                        }
                        else
                        {
                            /*
                             * Korisnik je već bio neaktivan,
                             * ali smo uskladili nedostajuća polja.
                             */
                            result.AlreadyInactive++;

                            _logger.LogInformation(
                                "USKLAĐEN STATUS NEAKTIVNOG KORISNIKA: " +
                                "KORISNIK_ID={KorisnikId}, HRM_ID={HrmId}",
                                korisnik.ID,
                                korisnik.HRM_ID);
                        }
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();

                        result.Failed++;

                        var message =
                            $"KORISNIK_ID={localUser.ID}, " +
                            $"HRM_ID={localUser.HRM_ID}: {ex.Message}";

                        result.ErrorMessages.Add(message);

                        _logger.LogError(
                            ex,
                            "Greška pri deaktivaciji korisnika " +
                            "KORISNIK_ID={KorisnikId}, HRM_ID={HrmId}",
                            localUser.ID,
                            localUser.HRM_ID);
                    }
                    finally
                    {
                        _dbContext.ChangeTracker.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                result.Failed++;

                result.ErrorMessages.Add(
                    "Opšta greška prilikom deaktivacije: " +
                    ex.Message);

                _logger.LogError(
                    ex,
                    "Opšta greška prilikom deaktivacije " +
                    "neaktivnih HRM korisnika.");
            }

            return result;
        }








        private static string? NormalizeValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static bool AreEqual(
            string? currentValue,
            string? newValue)
        {
            return string.Equals(
                currentValue?.Trim() ?? string.Empty,
                newValue?.Trim() ?? string.Empty,
                StringComparison.Ordinal);
        }

        private static string NormalizeName(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToUpperInvariant();
        }



        [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
        [AutomaticRetry(Attempts = 2)]
        public async Task RunDailyInternalUsersSyncAsync()
        {
            await RunImportAsync();
            await RunSyncAsync();
            await RunDeactivationAsync();
        }
    }
}