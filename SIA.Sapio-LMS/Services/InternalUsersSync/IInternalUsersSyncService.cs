namespace SIA.Sapio_LMS.Services.InternalUsersSync
{
    public interface IInternalUsersSyncService
    {
        Task<InternalUsersImportResult> RunImportAsync();

        Task<InternalUsersSyncResult> RunSyncAsync();

        Task<InternalUsersDeactivationResult> RunDeactivationAsync();
    }
}