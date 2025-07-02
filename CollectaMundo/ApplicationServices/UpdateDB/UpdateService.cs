using CollectaMundo.Data.UpdateDB;

namespace CollectaMundo.ApplicationServices.UpdateDB
{
    public class UpdateService(IUpdateDbRepo updateDBRepo, IUpdateDbRemoteData remoteData) : IUpdateService
    {
        private readonly IUpdateDbRepo _updateDBRepo = updateDBRepo;
        private readonly IUpdateDbRemoteData _remoteData = remoteData;
        public async Task CheckForDbUpdatesAsync()
        {
            int numberOfSetsInDb;
            int numberOfSetsOnServer;
            string? errorMessage;

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
                errorMessage = ex.Message;
                // Roll back on any error
                await uow.RollbackAsync();
                throw;
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
                errorMessage = ex.Message;
                throw;
            }

            // Compare the number of sets in the database with the number of sets on the server
            if (numberOfSetsInDb < numberOfSetsOnServer)
            {
                // return something
            }
            else
            {
                // return something else
            }
        }
    }
}
