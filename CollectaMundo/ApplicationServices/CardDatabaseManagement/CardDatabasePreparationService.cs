using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.DownloadResourceFiles;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.ApplicationServices.Utilities.Progress;
using CollectaMundo.Data;
using CollectaMundo.Data.CardDatabaseManagement;
using CollectaMundo.Data.RemoteLookups;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;

namespace CollectaMundo.ApplicationServices.CardDatabaseManagement
{
    public class CardDatabasePreparationService(IAppSettings settings, IDbConnectionFactory dbFactory, ProgressSinks progressSinks, ICardDatabasePreparationRepo dbSchemaRepo, ICardPriceService priceService, IGenerateMissingPngService missingPngService, IDownloadService downloadService, IRemoteLookups remoteLookups) : ICardDatabasePreparationService
    {
        private readonly IAppSettings _settings = settings;
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly ProgressSinks _progressSinks = progressSinks ?? ProgressSinks.NoOp;
        private readonly ICardDatabasePreparationRepo _dbSchemaRepo = dbSchemaRepo;
        private readonly ICardPriceService _priceService = priceService;
        private readonly IGenerateMissingPngService _missingPngService = missingPngService;
        private readonly IDownloadService _downloadService = downloadService;
        private readonly IRemoteLookups _remoteLookups = remoteLookups;

        // Paths (precomputed)
        private readonly string _dbPath = Path.Combine(settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");
        private readonly string _pricesPath = Path.Combine(settings.UserDownloadsPath, "prices.json");
        private readonly string _tempDbPath = Path.Combine(settings.UserDownloadsPath, "AllPrintings.sqlite");

        // Use case: orchestrates the first-time database preparation steps
        public async Task<OperationResult> FirstTimeDbPrepOrchetrator(int defaultDelay = 3000)
        {
            // ---------------------------
            // Step 0. Online check
            // ---------------------------
            if (!await _remoteLookups.IsInternetAvailableAsync())
            {
                return new OperationResult(OperationResultCode.NoInternet, "Internet not available");
            }

            _progressSinks.Headline.Report("Performing first-time setup of card database - please wait ...");
            _progressSinks.ProgressBarVisible.Report(true);

            // Always start from a clean slate
            try { CleanupPartialDatabaseFiles(_dbPath, _settings.UserDownloadsPath); }
            catch (Exception ex) { Debug.WriteLine($"[Cleanup] {ex.Message}"); }

            try
            {
                // ---------------------------
                // Step 1. Download resources
                // ---------------------------

                var step1Name = "Step 1. Downloading card database and prices...";
                var downloadResult = await _downloadService.DownloadParallelAsync(
                    _settings.CardDatabaseUrl, _dbPath, "Card database",
                    _settings.CardPricesUrl, _pricesPath, "Price File",
                    retryDelayInMs: defaultDelay,
                    stepName: step1Name,
                    stepNameAndNumberProgress: _progressSinks.Step,
                    stepDetailAndErrorProgress: _progressSinks.Detail,
                    percentProgress: _progressSinks.Percent);

                if (downloadResult.Code != OperationResultCode.Success)
                {
                    Debug.WriteLine($"[FirstTimeDbPrepOrchetrator] Download failed: {downloadResult.Message}");
                    return new OperationResult(OperationResultCode.DownloadFailed, downloadResult.Message);
                }


                // ---------------------------
                // Steps 2–9. Prepare database
                // ---------------------------
                var prepResult = await PrepareDatabaseAsync(defaultDelay, stepNumberStart: 2);
                if (prepResult.Code != OperationResultCode.Success)
                {
                    return new OperationResult(OperationResultCode.Error, prepResult.Message);
                }

                // Success: clean up transient price file

                try { File.Delete(_pricesPath); }
                catch (IOException ex)
                {
                    Debug.WriteLine($"Couldn't delete prices.json: {ex.Message}");
                    return new OperationResult(OperationResultCode.Error, ex.Message);
                }

                return new OperationResult(OperationResultCode.Success);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FirstTimeDbPrepOrchetrator] Fatal error: {ex.Message}");
                return new OperationResult(OperationResultCode.Error, ex.Message);
            }
        }

        // Use case: check for updates to the card database
        public async Task<OperationResult> CheckForDbUpdatesAsync(CancellationToken ct = default)
        {
            try
            {
                ct.ThrowIfCancellationRequested(); // Fast exit if cancelled before start

                // Step 1: Check internet connectivity
                var internetAvailable = await _remoteLookups.IsInternetAvailableAsync(ct);
                if (!internetAvailable)
                {
                    return new OperationResult(OperationResultCode.Error, "Internet not available - unable to check server...");
                }

                // Step 2: Query local DB
                int numberOfSetsInDb;
                await using var uow = new UnitOfWork();
                await uow.BeginAsync();
                try
                {
                    numberOfSetsInDb = await _dbSchemaRepo.GetNumberOfSetsAsync(uow.CurrentConnection, ct);
                    await uow.CommitAsync();
                }
                catch (Exception ex)
                {
                    await uow.RollbackAsync();
                    return new OperationResult(OperationResultCode.Error, $"Error querying your db for sets: {ex.Message}");
                }

                // Step 3: Query server
                int numberOfSetsOnServer = await _remoteLookups.FetchSetsCountAsync(ct);

                // Step 4: Compare counts
                if (numberOfSetsInDb < numberOfSetsOnServer)
                {
                    return new OperationResult(OperationResultCode.NeedsUpdate,
                        $"Your local card database has {numberOfSetsInDb} sets, server has {numberOfSetsOnServer} sets — update available!");
                }

                return new OperationResult(OperationResultCode.UpToDate,
                    $"Your local card database is up to date! ({numberOfSetsInDb} sets).");
            }
            catch (OperationCanceledException)
            {
                return new OperationResult(OperationResultCode.CancelledByUser, "User cancelled update check.");
            }
        }

        // Use case: orchestrates card database update
        public async Task<OperationResult> UpdateDbPrepOrchetrator(int defaultDelay = 3000, CancellationToken ct = default)
        {

            // ---------------------------
            // Step 0. Online check
            // ---------------------------
            if (!await _remoteLookups.IsInternetAvailableAsync(ct))
            {
                return new OperationResult(OperationResultCode.NoInternet, "Internet not available");
            }

            _progressSinks.Headline.Report("Updating card database - please wait ...");
            _progressSinks.ProgressBarVisible.Report(true);

            // ---------------------------
            // Step 1. Download resources
            // ---------------------------

            var step1Name = "Step 1. Downloading card database and prices...";
            var downloadResult = await _downloadService.DownloadParallelAsync(
                _settings.CardDatabaseUrl, _tempDbPath, "Card database",
                _settings.CardPricesUrl, _pricesPath, "Price File",
                retryDelayInMs: defaultDelay,
                stepName: step1Name,
                stepNameAndNumberProgress: _progressSinks.Step,
                stepDetailAndErrorProgress: _progressSinks.Detail,
                percentProgress: _progressSinks.Percent,
                cancelToken: ct);

            if (ct.IsCancellationRequested)
            {
                CleanupPartialDownloads();
                return new OperationResult(OperationResultCode.CancelledByUser, "Update was cancelled by user during download.");
            }

            if (downloadResult.Code != OperationResultCode.Success)
            {
                Debug.WriteLine($"[FirstTimeDbPrepOrchetrator] Download failed: {downloadResult.Message}");
                return new OperationResult(OperationResultCode.DownloadFailed, downloadResult.Message);
            }

            // ---------------------------
            // Step 2 - Copy tables from new DB
            // ---------------------------
            _progressSinks.ProgressBarVisible.Report(false);
            _progressSinks.CancelEnabled?.Report(false); // Disable cancel button after download phase
            _progressSinks.Step.Report("Step 2. Copying new tables...");

            try
            {
                await Task.Run(async () =>
                {
                    await using var conn = await _dbFactory.OpenConnectionAsync().ConfigureAwait(false);

                    using (var tx = conn.BeginTransaction())
                    {
                        await _dbSchemaRepo.AttachTempDbAsync(conn, _tempDbPath, _progressSinks.Detail);
                        await _dbSchemaRepo.DropTablesAsync(conn, _progressSinks.Detail);
                        Debug.WriteLine("[CardDatabasePrep] Dropped old tables.");
                        await _dbSchemaRepo.CopyTablesAsync(conn, _progressSinks.Detail);
                        Debug.WriteLine("[CardDatabasePrep] Copied new tables.");

                        tx.Commit();
                    }

                    await _dbSchemaRepo.DetachTempDbAsync(conn, _progressSinks.Detail);
                });

            }
            catch (Exception ex)
            {
                _progressSinks.Detail.Report($"Table copy failed: {ex.Message}");
                return new OperationResult(OperationResultCode.Error, $"Table copy failed: {ex.Message}");
            }

            // ---------------------------
            // Steps 3–10. Prepare database
            // ---------------------------
            var prepResult = await PrepareDatabaseAsync(defaultDelay, stepNumberStart: 3);
            if (prepResult.Code != OperationResultCode.Success)
            {
                return new OperationResult(OperationResultCode.Error, prepResult.Message);
            }

            // Success: clean up temporary db and price file
            try
            {
                File.Delete(_pricesPath);
                File.Delete(_tempDbPath);
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"Cleanup failed: {ex.Message}");
                return new OperationResult(OperationResultCode.Error, ex.Message);
            }
            return new OperationResult(OperationResultCode.Success);

        }

        // Core logic for preparing the database (used by both first-time prep and update)
        private async Task<OperationResult> PrepareDatabaseAsync(int defaultDelay, int stepNumberStart)
        {
            var steps = new List<(string Label, Func<Task> Work, bool ShowProgress)>
            {
                ($"Step {stepNumberStart++}. Creating custom tables...",() => Task.Run(() => ExecuteWithUnitOfWorkAsync(conn => _dbSchemaRepo.CreateTablesAsync(conn)) ),false),
                ($"Step {stepNumberStart++}. Generating mana symbols...",() => ExecuteWithUnitOfWorkAsync(conn => _missingPngService.GenerateMissingManaSymbolImagesAsync(conn, _progressSinks.Percent)),true),
                ($"Step {stepNumberStart++}. Generating mana cost images...",() => ExecuteWithUnitOfWorkAsync(conn => _missingPngService.GenerateMissingManaCostImagesAsync(conn, _progressSinks.Percent)),true),
                ($"Step {stepNumberStart++}. Generating set icon images...",() => ExecuteWithUnitOfWorkAsync(conn => _missingPngService.GenerateMissingKeyRuneImagesAsync(conn, _progressSinks.Percent)),true),
                ($"Step {stepNumberStart++}. Processing card prices...",() => ExecuteWithUnitOfWorkAsync(conn => _priceService.ImportPricesFromJsonAsync(_pricesPath, conn, _progressSinks.Detail, _progressSinks.Percent)),true),
                ($"Step {stepNumberStart++}. Creating views...",() => Task.Run(() => ExecuteWithUnitOfWorkAsync(conn => _dbSchemaRepo.CreateViewsAsync(conn, "cardmarket"))),false),
                ($"Step {stepNumberStart++}. Creating indices...",() => Task.Run(() => ExecuteWithUnitOfWorkAsync(conn => _dbSchemaRepo.CreateIndicesAsync(conn))),false),
                ($"Step {stepNumberStart++}. Optimizing database...",() => Task.Run(() => ExecuteWithConnectionAsync(conn => _dbSchemaRepo.OptimizeAsync(conn))),false),
            };

            foreach (var (label, work, showProgress) in steps)
            {
                _progressSinks.ProgressBarVisible.Report(showProgress);

                // Reset detail label for each step
                _progressSinks.Detail.Report(string.Empty);

                var result = await RetryHelper.RetryLoopAsync(
                    async () =>
                    {
                        await work();
                        return new OperationResult(OperationResultCode.Success, $"{label} completed.");
                    },
                    retryDelayInMs: defaultDelay,
                    maxRetries: 3,
                    stepName: label,
                    stepNameAndNumberProgress: _progressSinks.Step,
                    stepDetailAndErrorProgress: _progressSinks.Detail);

                if (result.Code != OperationResultCode.Success)
                {
                    // Short-circuit on the first failing step, return the error to the caller
                    return result;
                }
            }
            return new OperationResult(OperationResultCode.Success, "Database preparation completed.");
        }

        // Retry logic for executing database actions
        private static async Task ExecuteWithUnitOfWorkAsync(Func<SQLiteConnection, Task> action)
        {
            await using var uow = new UnitOfWork();
            await uow.BeginAsync();
            await action(uow.CurrentConnection);
            await uow.CommitAsync();
        }
        private async Task ExecuteWithConnectionAsync(Func<SQLiteConnection, Task> action)
        {
            await using var conn = await _dbFactory.OpenConnectionAsync();
            await action(conn);
        }

        // Cleanup logic for partial downloads
        private static void CleanupPartialDatabaseFiles(string dbPath, string userDownloads)
        {
            var filesToDelete = new[]
            {
                dbPath,
                Path.Combine(userDownloads, "AllPrintings.sqlite - shm"),
                Path.Combine(userDownloads, "AllPrintings.sqlite - wal")
            };

            foreach (var file in filesToDelete)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }

            Debug.WriteLine("[CardDatabasePrep] Deleted corrupt or partial DB file(s).");
        }
        private void CleanupPartialDownloads()
        {
            try
            {
                CleanupPartialDatabaseFiles(_tempDbPath, _settings.UserDownloadsPath);
                if (File.Exists(_pricesPath))
                {
                    File.Delete(_pricesPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cleanup on cancel failed: {ex.Message}");
            }
        }
    }
}
