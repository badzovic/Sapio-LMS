using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio_LMS.Areas.Admin.Models;

namespace SIA.Sapio_LMS.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SeasonalUsersController : BaseAdminController
    {
        private readonly ApplicationDbContext _dbContext;

        public SeasonalUsersController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index(string status = "active")
        {
            status = (status ?? "active").ToLower();

            var seasonalTipId = await _dbContext.TIPOVI_KORISNIKA
                .Where(x => x.Naziv == "Sezonski radnik")
                .Select(x => (int?)x.ID)
                .FirstOrDefaultAsync();

            if (seasonalTipId == null)
            {
                TempData["ImportMessage"] = "Tip korisnika 'Sezonski radnik' nije pronađen.";
                return View(new List<ExternalUsersVM>());
            }

            var query = _dbContext.KORISNICI
                .Include(x => x.Kompanija)
                .Where(x => x.TIP_KORISNIKA_ID == seasonalTipId.Value)
                .AsQueryable();

            switch (status)
            {
                case "inactive":
                    query = query.Where(x => x.AKTIVAN == "N");
                    break;

                case "all":
                    break;

                case "active":
                default:
                    query = query.Where(x => x.AKTIVAN == "Y");
                    status = "active";
                    break;
            }

            var model = await query
                .OrderBy(x => x.PREZIME)
                .ThenBy(x => x.IME)
                .Select(x => new ExternalUsersVM
                {
                    ID = x.ID,
                    ExternalId = x.EXTERNI_V3_ID ?? 0,
                    Ime = x.IME,
                    Prezime = x.PREZIME,
                    ImeOca = x.IME_OCA,
                    MjestoRodjenja = x.MJESTO_RODJENJA,
                    Kompanija = x.Kompanija != null ? x.Kompanija.NAZIV : x.KOMPANIJA,
                    KompanijaId = x.KOMPANIJA_ID,
                    UserId = x.USER_ID
                })
                .ToListAsync();

            ViewBag.CurrentStatus = status;

            return View(model);
        }
    }
}