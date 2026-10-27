using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio.LMS.Data.Identity;
using SIA.Sapio_LMS.Areas.Admin.Models;
using SIA.Sapio_LMS.Areas.Admin.Models.Users;

namespace SIA.Sapio_LMS.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UsersController : BaseAdminController
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;

        private const string DefaultPassword = "Sarajevo2026!";

        public UsersController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            _dbContext = dbContext;
            _userManager = userManager;
        }

        //public IActionResult Index(string status = "all")
        //{
        //    var usersQuery = _userManager.Users
        //        .Where(x => !x.IsDeleted)
        //        .AsQueryable();

        //    if (status == "pending")
        //    {
        //        usersQuery = usersQuery.Where(x => !x.IsActive);
        //    }
        //    else if (status == "active")
        //    {
        //        usersQuery = usersQuery.Where(x => x.IsActive);
        //    }

        //    var users = usersQuery.ToList();

        //    ViewBag.Status = status;

        //    return View(users);
        //}

        public async Task<IActionResult> Index(
     string status = "all",
     string type = "all")
        {
            status = (status ?? "all").ToLowerInvariant();
            type = (type ?? "all").ToLowerInvariant();

            /*
             * ============================================================
             * OSNOVNI UPIT
             * ============================================================
             *
             * Ne filtriramo IsDeleted na početku.
             * Administrator mora moći vidjeti i deaktivirane korisnike.
             */

            var query =
                from u in _dbContext.Users
                join k in _dbContext.KORISNICI
                    on u.Id equals k.USER_ID into korisniciJoin
                from k in korisniciJoin.DefaultIfEmpty()

                join tk in _dbContext.TIPOVI_KORISNIKA
                    on k.TIP_KORISNIKA_ID equals tk.ID into tipoviJoin
                from tk in tipoviJoin.DefaultIfEmpty()

                select new UserIndexRowVM
                {
                    UserId = u.Id,

                    KorisnikId = k != null ? k.ID : (int?)null,

                    Email = u.Email,
                    FirstName = u.FirstName,
                    LastName = u.LastName,

                    ImeOca = k != null ? k.IME_OCA : null,

                    IsActiveIdentity =
                        u.IsActive && !u.IsDeleted,

                    AktivanLocal =
                        k != null ? k.AKTIVAN : "N",

                    TipKorisnikaId =
                        k != null ? k.TIP_KORISNIKA_ID : null,

                    TipKorisnikaNaziv =
                        tk != null ? tk.Naziv : null,

                    /*
                     * Pending korisnici nisu još povezani
                     * s odgovarajućim tipom korisnika.
                     */
                    IsPending =
                        !u.IsActive &&
                        !u.IsDeleted &&
                        (k == null || k.TIP_KORISNIKA_ID == null),

                    DetailsController =
                        tk != null && tk.Naziv == "Interni"
                            ? "InternalUsersManagement" :
                        tk != null && tk.Naziv == "Eksterni"
                            ? "ExternalUsersManagement" :
                        tk != null && tk.Naziv == "Sezonski radnik"
                            ? "SeasonalUsersManagement" :
                        "Users",

                    DetailsAction = "Details",

                    DetailsRouteType =
                        tk != null && tk.Naziv == "Interni"
                            ? "userId"
                            : "id"
                };

            /*
             * ============================================================
             * FILTER STATUSA
             * ============================================================
             */

            if (status == "pending")
            {
                query = query.Where(x => x.IsPending);
            }
            else if (status == "active")
            {
                query = query.Where(x =>
                    x.IsActiveIdentity &&
                    x.AktivanLocal == "Y");
            }
            else if (status == "inactive")
            {
                query = query.Where(x =>
                    !x.IsActiveIdentity &&
                    !x.IsPending);
            }

            /*
             * ============================================================
             * FILTER TIPA KORISNIKA
             * ============================================================
             */

            if (type == "internal")
            {
                query = query.Where(x =>
                    x.TipKorisnikaNaziv == "Interni");
            }
            else if (type == "external")
            {
                query = query.Where(x =>
                    x.TipKorisnikaNaziv == "Eksterni");
            }
            else if (type == "seasonal")
            {
                query = query.Where(x =>
                    x.TipKorisnikaNaziv == "Sezonski radnik");
            }
            else if (type == "pending")
            {
                query = query.Where(x => x.IsPending);
            }

            /*
             * ============================================================
             * REZULTAT
             * ============================================================
             */

            var model = new UserIndexVM
            {
                Users = await query
                    .OrderBy(x => x.LastName)
                    .ThenBy(x => x.FirstName)
                    .ToListAsync(),

                Status = status,
                TypeFilter = type
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PopulateDropdownsAsync();
            ViewBag.DefaultPassword = DefaultPassword;
            return View(new CreateUserVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserVM model)
        {
            await PopulateDropdownsAsync();
            ViewBag.DefaultPassword = DefaultPassword;

            if (!ModelState.IsValid)
                return View(model);

            if (model.TipKorisnikaId == null)
            {
                ModelState.AddModelError(nameof(model.TipKorisnikaId), "Tip korisnika je obavezan.");
                return View(model);
            }

            var selectedTip = await _dbContext.TIPOVI_KORISNIKA
                .FirstOrDefaultAsync(x => x.ID == model.TipKorisnikaId.Value);

            if (selectedTip == null)
            {
                ModelState.AddModelError(nameof(model.TipKorisnikaId), "Odabrani tip korisnika ne postoji.");
                return View(model);
            }

            var allowedTipNames = new[] { "Eksterni", "Sezonski radnik" };
            if (!allowedTipNames.Contains(selectedTip.Naziv))
            {
                ModelState.AddModelError(nameof(model.TipKorisnikaId), "Dozvoljeno je kreirati samo eksterne i sezonske korisnike.");
                return View(model);
            }

            var roleExists = await _dbContext.Roles
                .AnyAsync(x => x.Name != null && x.Name == model.Role);

            if (!roleExists)
            {
                ModelState.AddModelError(nameof(model.Role), "Odabrana rola ne postoji.");
                return View(model);
            }

            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                var normalizedEmail = model.Email.Trim();

                var existingIdentityUser = await _userManager.FindByEmailAsync(normalizedEmail);
                if (existingIdentityUser != null)
                {
                    ModelState.AddModelError(nameof(model.Email), "Korisnik sa ovim emailom već postoji.");
                    return View(model);
                }

                var existingLocalUserWithEmail = await _dbContext.KORISNICI
                    .AnyAsync(x => x.EMAIL != null && x.EMAIL == normalizedEmail);

                if (existingLocalUserWithEmail)
                {
                    ModelState.AddModelError(nameof(model.Email), "Email već postoji u lokalnoj tabeli korisnika.");
                    return View(model);
                }
            }

            var isExternalCollaborator =
                selectedTip.Naziv == "Eksterni" ||
                selectedTip.Naziv == "Sezonski radnik";

            var generatedUserName = !string.IsNullOrWhiteSpace(model.Email)
                ? model.Email.Trim()
                : $"manual_{Guid.NewGuid():N}";

            var existingUserName = await _userManager.FindByNameAsync(generatedUserName);
            if (existingUserName != null)
            {
                ModelState.AddModelError("", "Generisani username već postoji. Pokušajte ponovo.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = generatedUserName,
                Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                IsExternalCollaborator = isExternalCollaborator,
                IsActive = true,
                EmailConfirmed = !string.IsNullOrWhiteSpace(model.Email),
                CreatedAt = DateTime.UtcNow,
                MustChangePassword = true
            };

            var createResult = await _userManager.CreateAsync(user, DefaultPassword);

            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                return View(model);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, model.Role);

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);

                foreach (var error in roleResult.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                return View(model);
            }

            try
            {
                var noviKorisnik = new Korisnik
                {
                    IME = model.FirstName.Trim(),
                    PREZIME = model.LastName.Trim(),
                    IME_OCA = model.ImeOca.Trim(),
                    MJESTO_RODJENJA = null,
                    EMAIL = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
                    USER_ID = user.Id,
                    ACCESS_KEY = null,
                    HRM_ID = null,
                    AKTIVAN = "Y",
                    KOMPANIJA = null,
                    KOMPANIJA_ID = null,
                    TIP_KORISNIKA_ID = selectedTip.ID,
                    EXTERNI_V3_ID = null                    
                };

                _dbContext.KORISNICI.Add(noviKorisnik);
                await _dbContext.SaveChangesAsync();
            }
            catch
            {
                await _userManager.DeleteAsync(user);
                ModelState.AddModelError("", "Korisnik je kreiran u Identity sistemu, ali upis u lokalnu tabelu nije uspio.");
                return View(model);
            }

            TempData["ImportMessage"] = $"Korisnik je uspješno kreiran. Dodijeljen je defaultni password: {DefaultPassword}";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdownsAsync()
        {
            ViewBag.Roles = await _dbContext.Roles
                .Where(x => x.Name != null)
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem
                {
                    Value = x.Name!,
                    Text = x.Name!
                })
                .ToListAsync();

            ViewBag.TipoviKorisnika = await _dbContext.TIPOVI_KORISNIKA
                .Where(x => x.Naziv == "Eksterni" || x.Naziv == "Sezonski radnik" || x.Naziv == "Interni")
                .OrderBy(x => x.Naziv)
                .Select(x => new SelectListItem
                {
                    Value = x.ID.ToString(),
                    Text = x.Naziv
                })
                .ToListAsync();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveAsExternal(string userId)
        {
            return await ApproveUser(userId, "Eksterni");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveAsSeasonal(string userId)
        {
            return await ApproveUser(userId, "Sezonski radnik");
        }

        private async Task<IActionResult> ApproveUser(string userId, string tipNaziv)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["ImportMessage"] = "User ID nije validan.";
                return RedirectToAction(nameof(Index));
            }

            var identityUser = await _userManager.FindByIdAsync(userId);

            if (identityUser == null)
            {
                TempData["ImportMessage"] = "Korisnik nije pronađen.";
                return RedirectToAction(nameof(Index));
            }

            var lokalniUser = await _dbContext.KORISNICI
                .FirstOrDefaultAsync(x => x.USER_ID == userId);

            if (lokalniUser == null)
            {
                TempData["ImportMessage"] = "Lokalni korisnik nije pronađen.";
                return RedirectToAction(nameof(Index));
            }

            /*
             * Internim korisnicima upravlja HRM sinhronizacija.
             * Ne dozvoljavamo njihovu konverziju ovom metodom.
             */
            if (lokalniUser.TIP_KORISNIKA_ID == 1)
            {
                TempData["ImportMessage"] =
                    "Korisnik je interni radnik. Njegovim statusom upravlja HRM sinhronizacija.";

                return RedirectToAction(nameof(Index));
            }

            var tip = await _dbContext.TIPOVI_KORISNIKA
                .FirstOrDefaultAsync(x => x.Naziv == tipNaziv);

            if (tip == null)
            {
                TempData["ImportMessage"] =
                    $"Tip korisnika '{tipNaziv}' ne postoji.";

                return RedirectToAction(nameof(Index));
            }

            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync();

            try
            {
                // Poslovna tabela
                lokalniUser.TIP_KORISNIKA_ID = tip.ID;
                lokalniUser.AKTIVAN = "Y";

                // Identity
                identityUser.IsActive = true;
                identityUser.IsDeleted = false;
                identityUser.IsExternalCollaborator = true;
                identityUser.LockoutEnabled = true;

                // Uklanjamo trajnu blokadu ako postoji.
                if (identityUser.LockoutEnd == DateTimeOffset.MaxValue)
                {
                    identityUser.LockoutEnd = null;
                }

                var result = await _userManager.UpdateAsync(identityUser);

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Greška prilikom aktivacije korisnika: " +
                        string.Join(" | ",
                            result.Errors.Select(e => e.Description)));
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["ImportMessage"] =
                    $"Korisnik je aktiviran kao '{tipNaziv}'.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                TempData["ImportMessage"] =
                    "Greška prilikom aktivacije korisnika.";

                TempData["ImportErrors"] = ex.Message;
            }
            finally
            {
                _dbContext.ChangeTracker.Clear();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}