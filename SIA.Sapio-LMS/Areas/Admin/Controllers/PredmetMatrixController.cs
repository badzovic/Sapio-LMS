using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio_LMS.Areas.Models;
using SIA.Sapio_LMS.Models;
using SIA.Sapio_LMS.Services;

namespace SIA.Sapio_LMS.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class PredmetMatrixController : BaseAdminController
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly V3MatrixApiService _v3MatrixApiService;

        public PredmetMatrixController(
            ApplicationDbContext dbContext,
            V3MatrixApiService v3MatrixApiService)
        {
            _dbContext = dbContext;
            _v3MatrixApiService = v3MatrixApiService;
        }

        public async Task<IActionResult> Index(
    int predmetId,
    string studentFilter = "allMatrix")
        {
            var predmet = await _dbContext.PREDMETI
                .FirstOrDefaultAsync(x => x.ID == predmetId);

            if (predmet == null)
                return NotFound();

            if (!predmet.V3_ID_OBUKE.HasValue)
            {
                TempData["ImportMessage"] =
                    "Predmet nije povezan sa V3 obukom.";

                return RedirectToAction(
                    "Details",
                    "Predmeti",
                    new
                    {
                        area = "Admin",
                        id = predmetId
                    });
            }

            var apiResponse = await _v3MatrixApiService
                .GetRadniciProgramStatusAsync(
                    predmet.V3_ID_OBUKE.Value);

            var v3Statusi =
                apiResponse?.Users ??
                new List<V3RadnikProgramStatusDto>();

            var v3StatusByRadnikId = v3Statusi
                .GroupBy(x => x.RadnikId)
                .ToDictionary(
                    x => x.Key,
                    x => x.First());

            var radnikIds =
                v3StatusByRadnikId.Keys.ToHashSet();

            /*
             * Korisničke podatke uzimamo iz KORISNICI,
             * ali email uvijek dolazi iz AspNetUsers.
             */
            var korisniciRaw = await (
                from k in _dbContext.KORISNICI

                join u in _dbContext.Users
                    on k.USER_ID equals u.Id into identityUsers

                from u in identityUsers.DefaultIfEmpty()

                where k.AKTIVAN == "Y"
                      && k.ACCESS_KEY.HasValue

                orderby k.PREZIME, k.IME

                select new
                {
                    k.ID,
                    k.IME,
                    k.PREZIME,

                    // Jedini izvor emaila za UI
                    EMAIL = u != null ? u.Email : null,

                    k.KOMPANIJA,
                    k.ACCESS_KEY
                })
                .ToListAsync();

            var studenti = korisniciRaw
                .Where(x =>
                    x.ACCESS_KEY.HasValue &&
                    radnikIds.Contains(
                        x.ACCESS_KEY.Value))
                .Select(x =>
                {
                    v3StatusByRadnikId.TryGetValue(
                        x.ACCESS_KEY!.Value,
                        out var status);

                    var statusObuke = "Van matrice";
                    DateTime? datumVaziDo = null;
                    int? danaDoIsteka = null;

                    if (status != null)
                    {
                        datumVaziDo =
                            status.DatumVaziDo;

                        danaDoIsteka =
                            status.DanaDoIsteka;

                        if (!status.DatumVaziDo.HasValue)
                        {
                            statusObuke =
                                "Nema evidenciju";
                        }
                        else if (status.Isteklo)
                        {
                            statusObuke =
                                "Isteklo";
                        }
                        else if (
                            status.DanaDoIsteka.HasValue &&
                            status.DanaDoIsteka.Value <= 60)
                        {
                            statusObuke =
                                "Ističe u 60 dana";
                        }
                        else
                        {
                            statusObuke =
                                "Važeće";
                        }
                    }

                    return new AssignTestStudentItemVM
                    {
                        KorisnikId = x.ID,

                        ImePrezime =
                            ((x.IME ?? "") +
                             " " +
                             (x.PREZIME ?? ""))
                            .Trim(),

                        Email = x.EMAIL,

                        Kompanija =
                            x.KOMPANIJA,

                        VecDodijeljen = false,
                        Odabran = false,

                        DatumVaziDo =
                            datumVaziDo,

                        DanaDoIsteka =
                            danaDoIsteka,

                        StatusObuke =
                            statusObuke
                    };
                })
                .Where(x =>
                    studentFilter == "allMatrix"
                    ||
                    studentFilter == "dueSoon" &&
                    (
                        x.StatusObuke == "Nema evidenciju"
                        || x.StatusObuke == "Isteklo"
                        || x.StatusObuke == "Ističe u 60 dana"
                    ))
                .ToList();

            var model =
                new AssignTestStudentsVM
                {
                    PredmetId =
                        predmet.ID,

                    PredmetNaziv =
                        predmet.NAZIV,

                    TestNaziv =
                        "Korisnici po matrici",

                    Studenti =
                        studenti
                };

            ViewBag.StudentFilter =
                studentFilter;

            return View(model);
        }
    }
}