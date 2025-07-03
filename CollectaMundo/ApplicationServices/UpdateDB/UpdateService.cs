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
        public async Task<OperationResult> UpdateDbAsync()
        {
            // STEP 1: Download the new card database and price file
            // Same as step 1 in FirstTimeDbPrepOrchetrator, new card database and price file
            // new card database should be downloaded to current users "Downloads" folder: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); as "AllPrintings.sqlite"
            // The price file should be downloaded to the same location as "CardPrices.json"

            // STEP 2: Copy the tables from the new card database to the existing card database
            // We will implement this later...
            return new OperationResult(OperationResultCode.Error, "UpdateDbAsync is not implemented yet. Please try again later.");
        }
    }
}
