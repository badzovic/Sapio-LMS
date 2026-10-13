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

            var apiUsers = (await _internalUsersApiService.GetUsersAsync())
                .ToList();

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

                    var postojiUKorisnici = await _dbContext.KORISNICI
                        .AnyAsync(x => x.HRM_ID == apiUser.HrmId);

                    if (postojiUKorisnici)
                    {
                        result.Skipped++;

                        await transaction.RollbackAsync();

                        continue;
                    }

                    var apiImeNormalized =
                        NormalizeName(apiUser.Ime);

                    var apiPrezimeNormalized =
                        NormalizeName(apiUser.Prezime);

                    var apiImeOcaNormalized =
                        NormalizeName(apiUser.ImeOca);

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
                     * ============================================================
                     * POSTOJEĆI EKSTERNI / SEZONSKI KORISNIK
                     * ============================================================
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

                        var stariTipKorisnikaId =
                            postojeciKorisnik.TIP_KORISNIKA_ID;

                        var staroIme = postojeciKorisnik.IME;
                        var staroPrezime = postojeciKorisnik.PREZIME;
                        var staroImeOca = postojeciKorisnik.IME_OCA;

                        var novoIme = NormalizeValue(apiUser.Ime);
                        var novoPrezime = NormalizeValue(apiUser.Prezime);
                        var novoImeOca = NormalizeValue(apiUser.ImeOca);
                        var noviEmail = NormalizeValue(apiUser.Email);

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

                        postojeciIdentityUser.FirstName =
                            novoIme ?? string.Empty;

                        postojeciIdentityUser.LastName =
                            novoPrezime ?? string.Empty;

                        postojeciIdentityUser.Email = noviEmail;
                        postojeciIdentityUser.IsExternalCollaborator = false;
                        postojeciIdentityUser.IsActive = true;
                        postojeciIdentityUser.LockoutEnabled = true;
                        postojeciIdentityUser.LockoutEnd = null;

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
                                    "automatski je pretvoren u internog korisnika.",

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

                        continue;
                    }

                    /*
                     * ============================================================
                     * VIŠE MOGUĆIH KANDIDATA
                     * ============================================================
                     */

                    if (kandidati.Count > 1)
                    {
                        result.Ambiguous++;

                        _logger.LogWarning(
                            "NEJASNO PODUDARANJE: Za HRM_ID={HrmId}, " +
                            "{Ime} {Prezime}, pronađeno je " +
                            "{BrojKandidata} kandidata.",
                            apiUser.HrmId,
                            apiUser.Ime,
                            apiUser.Prezime,
                            kandidati.Count);

                        await transaction.RollbackAsync();

                        continue;
                    }

                    /*
                     * ============================================================
                     * POTPUNO NOVI KORISNIK
                     * ============================================================
                     */

                    var cleanEmail =
                        NormalizeValue(apiUser.Email);

                    var userName =
                        !string.IsNullOrWhiteSpace(cleanEmail)
                            ? cleanEmail
                            : $"hrm_{apiUser.HrmId}";

                    var identityUser =
                        await _userManager.FindByNameAsync(userName);

                    if (identityUser == null)
                    {
                        identityUser = new ApplicationUser
                        {
                            FirstName =
                                apiUser.Ime?.Trim() ?? string.Empty,

                            LastName =
                                apiUser.Prezime?.Trim() ?? string.Empty,

                            IsExternalCollaborator = false,
                            IsActive = true,
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

                        /*
                         * CreateAsync odmah upisuje korisnika u AspNetUsers.
                         * Nakon uspješnog CreateAsync identityUser.Id je dostupan.
                         */
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
                         * STUDENT ROLA
                         * ========================================================
                         *
                         * Svaki potpuno novi LMS korisnik koji se kreira
                         * kroz HRM import automatski dobija Student rolu.
                         *
                         * UserManager će napraviti zapis u AspNetUserRoles.
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

                        _logger.LogInformation(
                            "Korisniku USER_ID={UserId}, HRM_ID={HrmId} " +
                            "dodijeljena Student rola.",
                            identityUser.Id,
                            apiUser.HrmId);
                    }

                    /*
                     * ============================================================
                     * KORISNICI
                     * ============================================================
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
                // API vraća samo trenutno aktivne i neobrisane HRM korisnike.
                var apiUsers =
                    await _internalUsersApiService.GetUsersAsync();

                foreach (var apiUser in apiUsers)
                {
                    await using var transaction =
                        await _dbContext.Database.BeginTransactionAsync();

                    try
                    {
                        var postojeci = await _dbContext.KORISNICI
                            .FirstOrDefaultAsync(
                                x => x.HRM_ID == apiUser.HrmId);

                        if (postojeci == null)
                        {
                            result.Missing++;

                            await transaction.RollbackAsync();

                            continue;
                        }

                        var novoIme =
                            NormalizeValue(apiUser.Ime);

                        var novoPrezime =
                            NormalizeValue(apiUser.Prezime);

                        var novoImeOca =
                            NormalizeValue(apiUser.ImeOca);

                        var noviEmail =
                            NormalizeValue(apiUser.Email);

                        // Čuvamo staro stanje za audit.
                        var staroIme = postojeci.IME;
                        var staroPrezime = postojeci.PREZIME;
                        var staroImeOca = postojeci.IME_OCA;
                        var stariEmail = postojeci.EMAIL;
                        var stariAccessKey = postojeci.ACCESS_KEY;
                        var stariTipKorisnikaId = postojeci.TIP_KORISNIKA_ID;

                        var promijenjenaPolja = new List<string>();

                        bool korisnikChanged = false;
                        bool identityChanged = false;

                        /*
                         * ============================================================
                         * KORISNICI
                         * ============================================================
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
                         * API GetUsersAsync() vraća samo aktivne i neobrisane
                         * HRM korisnike.
                         *
                         * Zato svaki korisnik koji se nalazi u ovom API odgovoru
                         * mora biti aktivan i u LMS tabeli KORISNICI.
                         *
                         * Ovo također automatski reaktivira korisnika ako je
                         * ranije iz bilo kojeg razloga ostao AKTIVAN = "N".
                         */
                        if (postojeci.AKTIVAN != "Y")
                        {
                            postojeci.AKTIVAN = "Y";

                            promijenjenaPolja.Add("AKTIVAN");
                            korisnikChanged = true;
                        }

                        /*
                         * ============================================================
                         * ASP.NET IDENTITY
                         * ============================================================
                         */

                        if (!string.IsNullOrWhiteSpace(postojeci.USER_ID))
                        {
                            var identityUser =
                                await _userManager.FindByIdAsync(
                                    postojeci.USER_ID);

                            if (identityUser == null)
                            {
                                throw new Exception(
                                    $"Identity korisnik nije pronađen. " +
                                    $"USER_ID={postojeci.USER_ID}");
                            }

                            if (!AreEqual(
                                    identityUser.FirstName,
                                    novoIme))
                            {
                                identityUser.FirstName =
                                    novoIme ?? string.Empty;

                                identityChanged = true;
                            }

                            if (!AreEqual(
                                    identityUser.LastName,
                                    novoPrezime))
                            {
                                identityUser.LastName =
                                    novoPrezime ?? string.Empty;

                                identityChanged = true;
                            }

                            if (!AreEqual(
                                    identityUser.Email,
                                    noviEmail))
                            {
                                identityUser.Email = noviEmail;

                                identityChanged = true;
                            }

                            /*
                             * Ako je korisnik vraćen iz HRM API-ja,
                             * HRM ga smatra aktivnim.
                             *
                             * Osiguravamo da je aktivan i u AspNetUsers.
                             */
                            if (!identityUser.IsActive)
                            {
                                identityUser.IsActive = true;
                                identityChanged = true;
                            }

                            /*
                             * LockoutEnabled ostavljamo uključen jer Identity
                             * koristi ovaj mehanizam i za standardni lockout.
                             */
                            if (!identityUser.LockoutEnabled)
                            {
                                identityUser.LockoutEnabled = true;
                                identityChanged = true;
                            }

                            /*
                             * Deaktivacija korisnika postavlja LockoutEnd na MaxValue.
                             * Ako je korisnik ponovo aktivan u HRM-u,
                             * uklanjamo taj lockout.
                             */
                            if (identityUser.LockoutEnd != null)
                            {
                                identityUser.LockoutEnd = null;
                                identityChanged = true;
                            }

                            if (identityChanged)
                            {
                                var updateResult =
                                    await _userManager.UpdateAsync(
                                        identityUser);

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
                        }

                        bool hasChanges =
                            korisnikChanged || identityChanged;

                        if (!hasChanges)
                        {
                            result.Unchanged++;

                            await transaction.RollbackAsync();

                            continue;
                        }

                        var promijenjenaPoljaText =
                            promijenjenaPolja.Any()
                                ? string.Join(
                                    ", ",
                                    promijenjenaPolja)
                                : "IDENTITY_PODACI";

                        /*
                         * ============================================================
                         * AUDIT
                         * ============================================================
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

                                PROMIJENJENA_POLJA =
                                    promijenjenaPoljaText,

                                OPIS =
                                    $"Sinhronizovani podaci internog " +
                                    $"korisnika iz HRM sistema. " +
                                    $"Promijenjena polja: " +
                                    $"{promijenjenaPoljaText}.",

                                DATUM_KREIRANJA = DateTime.Now,

                                PREGLEDANO = false,
                                PREGLEDAO_USER_ID = null,
                                DATUM_PREGLEDA = null
                            };

                        _dbContext
                            .EVIDENCIJA_SINHRONIZACIJE_KORISNIKA
                            .Add(auditZapis);

                        await _dbContext.SaveChangesAsync();

                        await transaction.CommitAsync();

                        result.Updated++;
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
                    "Opšta greška tokom sinhronizacije " +
                    "internih korisnika.");

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
                 * API treba vratiti trenutno aktivne interne radnike.
                 */
                var apiUsers =
                    (await _internalUsersApiService.GetUsersAsync())
                    .ToList();

                /*
                 * Zaštita od slučajne masovne deaktivacije ako API
                 * zbog greške vrati praznu listu.
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
                 * Uzimamo sve lokalne interne korisnike koji imaju HRM_ID.
                 */
                var localInternalUsers = await _dbContext.KORISNICI
                    .Where(x =>
                        x.TIP_KORISNIKA_ID == 1 &&
                        x.HRM_ID != null)
                    .ToListAsync();

                foreach (var korisnik in localInternalUsers)
                {
                    /*
                     * Ako se HRM_ID još uvijek nalazi u API odgovoru,
                     * korisnik je aktivan i ne mijenjamo ga.
                     */
                    if (activeHrmIds.Contains(korisnik.HRM_ID!.Value))
                    {
                        continue;
                    }

                    /*
                     * Ako je već neaktivan, nema potrebe ponavljati izmjenu
                     * niti kreirati novi audit zapis.
                     */
                    if (korisnik.AKTIVAN == "N")
                    {
                        result.AlreadyInactive++;
                        continue;
                    }

                    await using var transaction =
                        await _dbContext.Database.BeginTransactionAsync();

                    try
                    {
                        /*
                         * Čuvamo staro stanje prije deaktivacije.
                         */
                        bool? stariLmsStatus = korisnik.AKTIVAN switch
                        {
                            "Y" => true,
                            "N" => false,
                            _ => null
                        };

                        bool? stariIdentityStatus = null;
                        bool? noviIdentityStatus = null;

                        string auditOpis;

                        /*
                         * Deaktivacija u poslovnoj tabeli.
                         */
                        korisnik.AKTIVAN = "N";

                        /*
                         * Deaktivacija povezanog Identity korisnika.
                         */
                        if (string.IsNullOrWhiteSpace(korisnik.USER_ID))
                        {
                            result.SkippedWithoutIdentity++;

                            auditOpis =
                                "Korisnik više nije pronađen među aktivnim " +
                                "internim radnicima u HRM sistemu. " +
                                "Deaktiviran je u tabeli KORISNICI, " +
                                "ali nema povezan Identity USER_ID.";

                            _logger.LogWarning(
                                "Korisnik KORISNIK_ID={KorisnikId}, " +
                                "HRM_ID={HrmId} nema USER_ID. " +
                                "Deaktiviran je samo u tabeli KORISNICI.",
                                korisnik.ID,
                                korisnik.HRM_ID);
                        }
                        else
                        {
                            var identityUser =
                                await _userManager.FindByIdAsync(
                                    korisnik.USER_ID);

                            if (identityUser == null)
                            {
                                result.SkippedWithoutIdentity++;

                                auditOpis =
                                    "Korisnik više nije pronađen među " +
                                    "aktivnim internim radnicima u HRM sistemu. " +
                                    "Deaktiviran je u tabeli KORISNICI, " +
                                    "ali povezani Identity korisnik nije pronađen.";

                                _logger.LogWarning(
                                    "Identity korisnik nije pronađen za " +
                                    "KORISNIK_ID={KorisnikId}, " +
                                    "USER_ID={UserId}, HRM_ID={HrmId}. " +
                                    "Deaktiviran je samo u tabeli KORISNICI.",
                                    korisnik.ID,
                                    korisnik.USER_ID,
                                    korisnik.HRM_ID);
                            }
                            else
                            {
                                /*
                                 * Čuvamo Identity stanje prije izmjene.
                                 */
                                stariIdentityStatus =
                                    identityUser.IsActive;

                                identityUser.IsActive = false;
                                identityUser.LockoutEnabled = true;
                                identityUser.LockoutEnd =
                                    DateTimeOffset.MaxValue;

                                var updateResult =
                                    await _userManager.UpdateAsync(
                                        identityUser);

                                if (!updateResult.Succeeded)
                                {
                                    throw new Exception(
                                        "Identity deaktivacija nije uspjela: " +
                                        string.Join(
                                            " | ",
                                            updateResult.Errors.Select(
                                                x => x.Description)));
                                }

                                noviIdentityStatus = false;

                                auditOpis =
                                    "Korisnik više nije pronađen među " +
                                    "aktivnim internim radnicima u HRM sistemu. " +
                                    "Deaktiviran je u tabeli KORISNICI " +
                                    "i onemogućena mu je prijava kroz Identity.";
                            }
                        }

                        /*
                         * Audit zapis deaktivacije.
                         */
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

                        /*
                         * Promjena korisnika i audit zapis čuvaju se zajedno.
                         */
                        await _dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();

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
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();

                        result.Failed++;

                        var message =
                            $"KORISNIK_ID={korisnik.ID}, " +
                            $"HRM_ID={korisnik.HRM_ID}: {ex.Message}";

                        result.ErrorMessages.Add(message);

                        _logger.LogError(
                            ex,
                            "Greška pri deaktivaciji korisnika " +
                            "KORISNIK_ID={KorisnikId}, HRM_ID={HrmId}",
                            korisnik.ID,
                            korisnik.HRM_ID);
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