using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIA.Sapio.LMS.Data.Context;
using SIA.Sapio.LMS.Data.Entities;
using SIA.Sapio_LMS.Areas.Admin.Models;

namespace SIA.Sapio_LMS.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class CompaniesController : BaseAdminController
    {
        private readonly ApplicationDbContext _dbContext;

        public CompaniesController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index(string? status)
        {
            var query = _dbContext.KOMPANIJE.AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "active")
                    query = query.Where(x => x.AKTIVNA == "Y");
                else if (status == "inactive")
                    query = query.Where(x => x.AKTIVNA == "N");
            }

            var model = await query
                .OrderBy(x => x.NAZIV)
                .Select(x => new CompaniesVM
                {
                    ID = x.ID,
                    Naziv = x.NAZIV,
                    NazivNormalized = x.NAZIV_NORMALIZED,
                    Aktivna = x.AKTIVNA,
                    DatumKreiranja = x.DATUM_KREIRANJA,
                    Napomena = x.NAPOMENA
                })
                .ToListAsync();

            ViewBag.Status = status;

            return View(model);
        }

        public IActionResult Create()
        {
            var model = new CompaniesVM();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CompaniesVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (string.IsNullOrWhiteSpace(model.Naziv))
            {
                ModelState.AddModelError(nameof(model.Naziv), "Naziv je obavezan.");
                return View(model);
            }

            var normalized = NormalizeCompanyName(model.Naziv);

            var exists = await _dbContext.KOMPANIJE
                .AnyAsync(x => x.NAZIV_NORMALIZED == normalized);

            if (exists)
            {
                ModelState.AddModelError(nameof(model.Naziv), "Kompanija sa ovim nazivom već postoji.");
                return View(model);
            }

            var entity = new Kompanija
            {
                NAZIV = model.Naziv.Trim(),
                NAZIV_NORMALIZED = normalized,
                AKTIVNA = string.IsNullOrWhiteSpace(model.Aktivna) ? "Y" : model.Aktivna,
                DATUM_KREIRANJA = DateTime.Now,
                NAPOMENA = string.IsNullOrWhiteSpace(model.Napomena) ? null : model.Napomena.Trim()
            };

            _dbContext.KOMPANIJE.Add(entity);
            await _dbContext.SaveChangesAsync();

            TempData["ImportMessage"] = "Kompanija je uspješno kreirana.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var entity = await _dbContext.KOMPANIJE.FirstOrDefaultAsync(x => x.ID == id);

            if (entity == null)
                return NotFound();

            var model = new CompaniesVM
            {
                ID = entity.ID,
                Naziv = entity.NAZIV,
                NazivNormalized = entity.NAZIV_NORMALIZED,
                Aktivna = entity.AKTIVNA,
                DatumKreiranja = entity.DATUM_KREIRANJA,
                Napomena = entity.NAPOMENA
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CompaniesVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var entity = await _dbContext.KOMPANIJE.FirstOrDefaultAsync(x => x.ID == model.ID);

            if (entity == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(model.Naziv))
            {
                ModelState.AddModelError(nameof(model.Naziv), "Naziv je obavezan.");
                return View(model);
            }

            var normalized = NormalizeCompanyName(model.Naziv);

            var exists = await _dbContext.KOMPANIJE
                .AnyAsync(x => x.ID != model.ID && x.NAZIV_NORMALIZED == normalized);

            if (exists)
            {
                ModelState.AddModelError(nameof(model.Naziv), "Druga kompanija sa ovim nazivom već postoji.");
                return View(model);
            }

            entity.NAZIV = model.Naziv.Trim();
            entity.NAZIV_NORMALIZED = normalized;
            entity.NAPOMENA = string.IsNullOrWhiteSpace(model.Napomena) ? null : model.Napomena.Trim();

            await _dbContext.SaveChangesAsync();

            TempData["ImportMessage"] = "Kompanija je uspješno izmijenjena.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var entity = await _dbContext.KOMPANIJE.FirstOrDefaultAsync(x => x.ID == id);

            if (entity == null)
            {
                TempData["ImportMessage"] = "Kompanija nije pronađena.";
                return RedirectToAction(nameof(Index));
            }

            entity.AKTIVNA = entity.AKTIVNA == "Y" ? "N" : "Y";

            await _dbContext.SaveChangesAsync();

            TempData["ImportMessage"] = entity.AKTIVNA == "Y"
                ? "Kompanija je aktivirana."
                : "Kompanija je deaktivirana.";

            return RedirectToAction(nameof(Index));
        }

        private static string NormalizeCompanyName(string input)
        {
            var chars = input
                .Trim()
                .ToLowerInvariant()
                .Where(char.IsLetterOrDigit)
                .ToArray();

            return new string(chars);
        }
    }
}