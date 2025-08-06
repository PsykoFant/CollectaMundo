using CollectaMundo.ApplicationServices.DownloadResourceFiles;
using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.ApplicationServices.Utilities.InternetCheck;
using CollectaMundo.Data;
using CollectaMundo.Data.UpdateDB;
using System.IO;

namespace CollectaMundo.ApplicationServices.UpdateDB
{
    public class UpdateService(IAppSettings settings, IDbConnectionFactory dbFactory, IDownloadService downloadService, IInternetConnectivityService internetConnectivityService, IUpdateDbRepo updateDBRepo, IUpdateDbRemoteData remoteData) : IUpdateService
    {
        private readonly IAppSettings _settings = settings;
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly IDownloadService _downloadService = downloadService;
        private readonly IInternetConnectivityService _internetConnectivityService = internetConnectivityService;
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
        public async Task<OperationResult> UpdateDbAsync(IProgress<string> stepDetailProgress, IProgress<string> stepLabelProgress, IProgress<int> percentProgress)
        {
            string dbPath = Path.Combine(_settings.UserDownloadsPath, "AllPrintings.sqlite");
            string pricesPath = Path.Combine(_settings.UserDownloadsPath, "AllPricesToday.json");
            string cardDbUrl = _settings.CardDatabaseUrl;
            string pricesUrl = _settings.CardPricesUrl;

            //Step 1: Downloads
            var downloadResult = await _downloadService.DownloadParallelAsync(
                _settings.CardDatabaseUrl, dbPath, "Card database",
                _settings.CardPricesUrl, pricesPath, "Price File",
                retryDelayInMs: 3000, stepDetailProgress, percentProgress, stepLabelProgress, stepName: "Step 1 / 4. Downloading resource files for update...");

            if (downloadResult.Code != OperationResultCode.Success)
            {
                return new OperationResult(OperationResultCode.Error, "Downloads failed after multiple retries.");
            }

            //Debug.WriteLine("Testing - assume files are already downloaded");

            // Step 2 - Copy tables from new DB
            stepLabelProgress.Report("Step 2 / 4 - Copying new tables...");

            try
            {
                await using var conn = await _dbFactory.OpenConnectionAsync();

                await Task.Run(async () =>
                {
                    await _updateDBRepo.AttachTempDbAsync(conn, dbPath, stepDetailProgress);
                    await _updateDBRepo.DropTablesAsync(conn, stepDetailProgress);
                    await _updateDBRepo.CopyTablesAsync(conn, stepDetailProgress);
                    await _updateDBRepo.DetachTempDbAsync(conn, stepDetailProgress);
                });

            }
            catch (Exception ex)
            {
                stepDetailProgress.Report($"Table copy failed: {ex.Message}");
                return new OperationResult(OperationResultCode.Error, $"Table copy failed: {ex.Message}");
            }

            return new OperationResult(OperationResultCode.Success, "Update complete!");
        }

    }
}
