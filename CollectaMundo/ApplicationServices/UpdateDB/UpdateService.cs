using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.Data;
using CollectaMundo.Data.UpdateDB;
using System.Diagnostics;
using System.IO;

namespace CollectaMundo.ApplicationServices.UpdateDB
{
    public class UpdateService(IAppSettings settings, IDbConnectionFactory dbFactory, IUpdateDbRepo updateDBRepo, IUpdateDbRemoteData remoteData) : IUpdateService
    {
        private readonly IAppSettings _settings = settings;
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
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
        public async Task<OperationResult> UpdateDbAsync(IProgress<string> statusLabel2Progress, IProgress<string> statusLabel3Progress, IProgress<int> percentProgress)
        {
            string dbPath = Path.Combine(_settings.UserDownloadsPath, "AllPrintings.sqlite");
            string pricesPath = Path.Combine(_settings.UserDownloadsPath, "AllPricesToday.json");
            string cardDbUrl = _settings.CardDatabaseUrl;
            string pricesUrl = _settings.CardPricesUrl;

            ////Step 1 - download files
            //bool downloadsSucceeded = await RetryHelper.RetryLoopAsync(
            //    async attempt =>
            //    {
            //        using var innerCts = new CancellationTokenSource();
            //        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(innerCts.Token);
            //        var token = linkedCts.Token;

            //        var taskA = DownloadResourceHelper.DownloadResourceAsync(cardDbUrl, dbPath, taskLabel: "Card DB", onStart: size => statusLabel2Progress.Report($"Card database size: {size}"), onProgress: percentProgress.Report, token: token);
            //        var taskB = DownloadResourceHelper.DownloadResourceAsync(pricesUrl, pricesPath, taskLabel: "Price file", null, null, token: token);

            //        var firstCompleted = await Task.WhenAny(taskA, taskB);

            //        if (!firstCompleted.Result.success)
            //        {
            //            innerCts.Cancel();
            //            await Task.WhenAll(taskA, taskB);
            //            throw new Exception(firstCompleted.Result.errorMessage ?? "Unknown download error");
            //        }

            //        var finalA = await taskA;
            //        var finalB = await taskB;

            //        if (!finalA.success || !finalB.success)
            //            throw new Exception(finalA.errorMessage ?? finalB.errorMessage ?? "Unknown download error");

            //        return true;

            //    },
            //    stepName: "Downloading DB + Prices",
            //    maxRetries: 3,
            //    progress: status2Progress
            //);

            //if (!downloadsSucceeded)
            //{
            //    return new OperationResult(OperationResultCode.Error, "Downloads failed after multiple retries.");
            //}


            Debug.WriteLine("Testing - assume files are already downloaded");

            // Step 2 - Copy tables from new DB
            statusLabel3Progress.Report("Step 2 / 4 - Copying new tables...");

            try
            {
                await using var conn = await _dbFactory.OpenConnectionAsync();

                await Task.Run(async () =>
                {
                    await _updateDBRepo.AttachTempDbAsync(conn, dbPath, statusLabel2Progress);
                    await _updateDBRepo.DropTablesAsync(conn, statusLabel2Progress);
                    await _updateDBRepo.CopyTablesAsync(conn, statusLabel2Progress);
                    await _updateDBRepo.DetachTempDbAsync(conn, statusLabel2Progress);
                });

            }
            catch (Exception ex)
            {
                statusLabel2Progress.Report($"Table copy failed: {ex.Message}");
                return new OperationResult(OperationResultCode.Error, $"Table copy failed: {ex.Message}");
            }

            return new OperationResult(OperationResultCode.Success, "Update complete!");
        }

    }
}
