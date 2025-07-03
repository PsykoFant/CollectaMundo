using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.Data.UpdateDB;

namespace CollectaMundo.ApplicationServices.UpdateDB
{
    public class UpdateService(IUpdateDbRepo updateDBRepo, IUpdateDbRemoteData remoteData) : IUpdateService
    {
        private readonly IUpdateDbRepo _updateDBRepo = updateDBRepo;
        private readonly IUpdateDbRemoteData _remoteData = remoteData;
        public async Task<OperationResult> CheckForDbUpdatesAsync()
        {
            int numberOfSetsInDb;
            int numberOfSetsOnServer;

            // Get the number of sets in the database
            await using var uow = new UnitOfWork();
            await uow.BeginAsync();
            try
            {
                numberOfSetsInDb = await _updateDBRepo.GetNumberOfSetsAsync(uow.CurrentConnection);
                await uow.CommitAsync();
            }
            catch (Exception ex)
            {
                // Roll back on any error
                await uow.RollbackAsync();
                return new OperationResult(OperationResultCode.Error, $"Error querying your db for sets: {ex.Message}");
            }
            finally
            {
                // Tear down the connection
                await uow.DisposeAsync();
            }

            // Get the number of sets on the server
            try
            {
                numberOfSetsOnServer = await _remoteData.FetchSetsCountAsync();
            }
            catch (Exception ex)
            {
                return new OperationResult(OperationResultCode.Error, $"Error querying server for updates: {ex.Message}");
            }

            // Compare the number of sets in the database with the number of sets on the server
            if (numberOfSetsInDb < numberOfSetsOnServer)
            {
                return new OperationResult(OperationResultCode.NeedsUpdate, $"Your local card database has {numberOfSetsInDb} sets, server has {numberOfSetsOnServer} sets — update available!");
            }
            else
            {
                return new OperationResult(OperationResultCode.UpToDate, $"Your local card database is up to date! ({numberOfSetsInDb} sets).");
            }
        }
    }
}
