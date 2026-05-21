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

            var matrixDto = await _v3MatrixApiService.GetMatrixByRadnikAsync(accessKey.Value);

            if (matrixDto == null)
                return null;

            var programIds = matrixDto.ProgramIds ?? new List<int>();

            var sviPredmeti = await _dbContext.PREDMETI
                .Where(p => p.V3_ID_OBUKE.HasValue)
                .OrderBy(p => p.NAZIV)
                .ToListAsync();

            var predmeti = sviPredmeti
                .Where(p => p.V3_ID_OBUKE.HasValue && programIds.Contains(p.V3_ID_OBUKE.Value))
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

            return new UserTrainingMatrixVM
            {
                RadnikId = matrixDto.RadnikId,
                IdZanimanja = matrixDto.IdZanimanja,
                Predmeti = predmeti,
                NazivRadnogMjesta = matrixDto.NazivRadnogMjesta,
            };
        }
    }
}