using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.Infrastructure.CardDatabaseManagement;
using CollectaMundo.Infrastructure.RemoteLookups;
using CollectaMundo.Infrastructure.Shared;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;

namespace CollectaMundo.ApplicationServices.CardDatabaseManagement
{
    public class CardDatabaseManagementService(IAppSettings settings, IDbConnectionFactory dbFactory, ProgressSinks progressSinks, ICardDatabaseManagementRepo dbMgmtRepo, ICardPriceService priceService, IGenerateMissingPngService missingPngService, IRemoteLookups remoteLookups, ICardDatabaseDownloader? downloader = null) : ICardDatabaseManagementService
    {
        private readonly IAppSettings _settings = settings;
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly ProgressSinks _progressSinks = progressSinks ?? ProgressSinks.NoOp;
        private readonly ICardDatabaseManagementRepo _dbMgmtRepo = dbMgmtRepo;
        private readonly ICardPriceService _priceService = priceService;
        private readonly IGenerateMissingPngService _missingPngService = missingPngService;
        private readonly ICardDatabaseDownloader _downloader = downloader ?? new CardDatabaseDownloader(); // default create new, allow mock to be injected for unit test
        private readonly IRemoteLookups _remoteLookups = remoteLookups;

        // Paths (precomputed)
        private readonly string _dbPath = Path.Combine(settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");
        private readonly string _pricesPath = Path.Combine(settings.UserDownloadsPath, "prices.json");
        private readonly string _tempDbPath = Path.Combine(settings.UserDownloadsPath, "AllPrintings.sqlite");

        public string BackupFolderPath => _settings.BackupFolderPath; // Expose current backup folder path from settings to viewmodel

        // Use case: orchestrates the first-time database preparation steps
        public async Task<OperationResult> FirstTimeDbPrepOrchestrator(int defaultDelay = 3000)
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
                var downloadResult = await _downloader.DownloadParallelAsync(
                    _settings.CardDatabaseUrl, _dbPath, "Card database",
                    _settings.CardPricesUrl, _pricesPath, "Price File",
                    retryDelayInMs: defaultDelay,
                    stepName: step1Name,
                    stepNameAndNumberProgress: _progressSinks.Step,
                    stepDetailAndErrorProgress: _progressSinks.Detail,
                    percentProgress: _progressSinks.Percent);

                if (downloadResult.Code != OperationResultCode.Success)
                {
                    Debug.WriteLine($"[FirstTimeDbPrepOrchestrator] Download failed: {downloadResult.Message}");
                    return new OperationResult(OperationResultCode.DownloadFailed, downloadResult.Message);
                }


                // ---------------------------
                // Steps 2–9. Prepare database
                // ---------------------------
                var prepResult = await PrepareDatabaseAsync(defaultDelay, displayStepStart: 2, stepsToRun: FullPrepSteps);
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
                Debug.WriteLine($"[FirstTimeDbPrepOrchestrator] Fatal error: {ex.Message}");
                return new OperationResult(OperationResultCode.Error, ex.Message);
            }
        }

        private static readonly IReadOnlyList<DbPrepStep> FullPrepSteps =
            [
                DbPrepStep.CreateTables,
                DbPrepStep.GenerateManaSymbols,
                DbPrepStep.GenerateManaCostImages,
                DbPrepStep.GenerateSetIcons,
                DbPrepStep.ImportPrices,
                DbPrepStep.CreateViews,
                DbPrepStep.CreateIndices,
                DbPrepStep.OptimizeDatabase
            ];

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
                await using var uow = new UnitOfWork(_dbFactory);
                await uow.BeginAsync();
                try
                {
                    numberOfSetsInDb = await _dbMgmtRepo.GetNumberOfSetsAsync(uow.CurrentConnection, ct);
                    await uow.CommitAsync();
                }
                catch (Exception ex)
                {
                    await uow.RollbackAsync();
                    return new OperationResult(OperationResultCode.Error, $"Error querying your db for sets: {ex.Message}");
                }

                // Step 3: Query server
                int numberOfSetsOnServer;
                try
                {
                    numberOfSetsOnServer = await _remoteLookups.FetchSetsCountAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    return new OperationResult(OperationResultCode.CancelledByUser, "Cancelled while checking for database updates.");
                }
                catch (Exception ex)
                {
                    return new OperationResult(OperationResultCode.Error, $"Failed to fetch sets from server: {ex.Message}");
                }

                // Step 4: Compare counts
                if (numberOfSetsOnServer > numberOfSetsInDb)
                {
                    return new OperationResult(OperationResultCode.NeedsUpdate, $"Number of sets on server: {numberOfSetsOnServer}, number of sets in database: {numberOfSetsInDb}. Update available!");
                }

                return new OperationResult(OperationResultCode.UpToDate, "Your local database is up to date.");
            }
            catch (OperationCanceledException)
            {
                return new OperationResult(OperationResultCode.CancelledByUser, "Check for DB updates was cancelled.");
            }
            catch (Exception ex)
            {
                return new OperationResult(OperationResultCode.Error, $"Unexpected error: {ex.Message}");
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
            // Download resources
            // ---------------------------

            var step1Name = "Step 1. Downloading card database and prices...";
            var downloadResult = await _downloader.DownloadParallelAsync(
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
                return new OperationResult(OperationResultCode.CancelledByUser, "Update was cancelled by user during download.");
            }

            if (downloadResult.Code != OperationResultCode.Success)
            {
                Debug.WriteLine($"[FirstTimeDbPrepOrchestrator] Download failed: {downloadResult.Message}");
                return new OperationResult(OperationResultCode.DownloadFailed, downloadResult.Message);
            }

            // ---------------------------
            // Copy tables from new DB
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
                        await _dbMgmtRepo.AttachTempDbAsync(conn, _tempDbPath, _progressSinks.Detail);
                        await _dbMgmtRepo.DropTablesAsync(conn, _progressSinks.Detail);
                        Debug.WriteLine("[CardDatabasePrep] Dropped old tables.");
                        await _dbMgmtRepo.CopyTablesAsync(conn, _progressSinks.Detail);
                        Debug.WriteLine("[CardDatabasePrep] Copied new tables.");

                        tx.Commit();
                    }

                    await _dbMgmtRepo.DetachTempDbAsync(conn, _progressSinks.Detail);
                }, CancellationToken.None);

            }
            catch (Exception ex)
            {
                _progressSinks.Detail.Report($"Table copy failed: {ex.Message}");
                return new OperationResult(OperationResultCode.Error, $"Table copy failed: {ex.Message}");
            }

            // ---------------------------
            // Prepare database
            // ---------------------------
            var prepResult = await PrepareDatabaseAsync(defaultDelay, displayStepStart: 3, stepsToRun: UpdateDbSteps);
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

        private static readonly IReadOnlyList<DbPrepStep> UpdateDbSteps =
            [
                DbPrepStep.GenerateManaSymbols,
                DbPrepStep.GenerateManaCostImages,
                DbPrepStep.GenerateSetIcons,
                DbPrepStep.ImportPrices,
                DbPrepStep.OptimizeDatabase
            ];

        // Use case: orchestrates card database update
        public async Task<OperationResult> UpdateCardPricesOrchetrator(int defaultDelay = 3000, CancellationToken ct = default)
        {

            // ---------------------------
            // Step 0. Online check
            // ---------------------------
            if (!await _remoteLookups.IsInternetAvailableAsync(ct))
            {
                return new OperationResult(OperationResultCode.NoInternet, "Internet not available");
            }

            _progressSinks.Headline.Report("Updating card prices - please wait ...");
            _progressSinks.ProgressBarVisible.Report(true);

            // ---------------------------
            // Step 1. Download resources
            // ---------------------------

            var step1Name = "Step 1. Downloading price file...";

            var downloadResult = await _downloader.DownloadAsync(
                url: _settings.CardPricesUrl,
                targetPath: _pricesPath,
                label: step1Name,
                retryDelayInMs: defaultDelay,
                stepNameAndNumberProgress: _progressSinks.Step,
                stepDetailAndErrorProgress: progressSinks.Detail,
                percentProgress: _progressSinks.Percent,
                cancelToken: ct);

            if (ct.IsCancellationRequested)
            {
                return new OperationResult(OperationResultCode.CancelledByUser, "Update was cancelled by user during download.");
            }

            if (downloadResult.Code != OperationResultCode.Success)
            {
                Debug.WriteLine($"[FirstTimeDbPrepOrchestrator] Download failed: {downloadResult.Message}");
                return new OperationResult(OperationResultCode.DownloadFailed, downloadResult.Message);
            }

            Debug.WriteLine("Downloaded prices.json successfully. Now for update stuff...");

            // ---------------------------
            // Step 2 - Copy tables from new DB
            // ---------------------------
            _progressSinks.ProgressBarVisible.Report(false);
            _progressSinks.CancelEnabled?.Report(false); // Disable cancel button after download phase
            _progressSinks.Step.Report("Step 2. Importing prices...");

            // ---------------------------
            // Steps 3–10. Prepare database
            // ---------------------------
            var prepResult = await PrepareDatabaseAsync(defaultDelay, displayStepStart: 2, stepsToRun: UpdatePricesSteps);

            if (prepResult.Code != OperationResultCode.Success)
            {
                return new OperationResult(OperationResultCode.Error, prepResult.Message);
            }

            // Success: clean up temporary db and price file
            try
            {
                File.Delete(_pricesPath);
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"Cleanup failed: {ex.Message}");
                return new OperationResult(OperationResultCode.Error, ex.Message);
            }
            return new OperationResult(OperationResultCode.Success);
        }

        private static readonly IReadOnlyList<DbPrepStep> UpdatePricesSteps =
            [
                DbPrepStep.ImportPrices,
                DbPrepStep.OptimizeDatabase
            ];

        // Shared method for orchestrating the various database preparation steps, with retry logic and progress reporting
        private async Task<OperationResult> PrepareDatabaseAsync(int defaultDelay, int displayStepStart, IReadOnlyList<DbPrepStep> stepsToRun)
        {
            var stepMap = GetPrepSteps().ToDictionary(x => x.Key);

            foreach (var stepKey in stepsToRun)
            {
                var (_, label, work, showProgress) = stepMap[stepKey];
                var stepLabel = $"Step {displayStepStart++}. {label}";

                Debug.WriteLine($"Starting: {stepLabel}");

                _progressSinks.ProgressBarVisible.Report(showProgress);
                _progressSinks.Detail.Report(string.Empty);

                var result = await RetryHelper.RetryLoopAsync(
                    async () =>
                    {
                        await work();
                        return new OperationResult(OperationResultCode.Success, $"{stepLabel} completed.");
                    },
                    retryDelayInMs: defaultDelay,
                    maxRetries: 3,
                    stepName: stepLabel,
                    stepNameAndNumberProgress: _progressSinks.Step,
                    stepDetailAndErrorProgress: _progressSinks.Detail);

                if (result.Code != OperationResultCode.Success)
                {
                    return result;
                }
            }

            return new OperationResult(OperationResultCode.Success, "Database preparation completed.");
        }
        private List<(DbPrepStep Key, string Label, Func<Task> Work, bool ShowProgress)> GetPrepSteps()
        {
            return
            [
                (DbPrepStep.CreateTables, "Creating custom tables...", () => Task.Run(() => ExecuteWithUnitOfWorkAsync(conn => _dbMgmtRepo.CreateTablesAsync(conn))), false),
                (DbPrepStep.GenerateManaSymbols, "Generating mana symbols...", () => ExecuteWithUnitOfWorkAsync(conn => _missingPngService.GenerateMissingManaSymbolImagesAsync(conn, _progressSinks.Percent)), true),
                (DbPrepStep.GenerateManaCostImages, "Generating mana cost images...", () => ExecuteWithUnitOfWorkAsync(conn => _missingPngService.GenerateMissingManaCostImagesAsync(conn, _progressSinks.Percent)), true),
                (DbPrepStep.GenerateSetIcons, "Generating set icon images...", () => ExecuteWithUnitOfWorkAsync(conn => _missingPngService.GenerateMissingKeyRuneImagesAsync(conn, _progressSinks.Percent)), true),
                (DbPrepStep.ImportPrices, "Processing card prices...", () => ExecuteWithUnitOfWorkAsync(conn => _priceService.ImportPricesFromJsonAsync(_pricesPath, conn, _progressSinks.Detail, _progressSinks.Percent)), true),
                (DbPrepStep.CreateViews, "Creating views...", () => Task.Run(() => ExecuteWithUnitOfWorkAsync(conn => _dbMgmtRepo.CreateViewsAsync(conn))), false),
                (DbPrepStep.CreateIndices, "Creating indices...", () => Task.Run(() => ExecuteWithUnitOfWorkAsync(conn => _dbMgmtRepo.CreateIndicesAsync(conn))), false),
                (DbPrepStep.OptimizeDatabase, "Optimizing database...", () => Task.Run(() => ExecuteWithConnectionAsync(conn => _dbMgmtRepo.OptimizeAsync(conn))), false),
            ];
        }
        private enum DbPrepStep
        {
            CreateTables,
            GenerateManaSymbols,
            GenerateManaCostImages,
            GenerateSetIcons,
            ImportPrices,
            CreateViews,
            CreateIndices,
            OptimizeDatabase
        }

        // Use case: Backup/export collection to CSV
        public async Task<OperationResult> ExportCollectionAsync(CancellationToken ct = default)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                await using var uow = new UnitOfWork(_dbFactory);
                await uow.BeginAsync();

                var filePath = await _dbMgmtRepo.ExportCollectionAsync(uow.CurrentConnection, _settings.BackupFolderPath, ct);

                ct.ThrowIfCancellationRequested();

                if (filePath == null)
                {
                    return new OperationResult(OperationResultCode.Empty, string.Empty);
                }

                return new OperationResult(OperationResultCode.Success, filePath);
            }
            catch (OperationCanceledException)
            {
                return new OperationResult(OperationResultCode.CancelledByUser, "User cancelled backup");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating CSV backup: {ex.Message}");
                return new OperationResult(OperationResultCode.Error, $"Error creating CSV backup: {ex.Message}");
            }
        }

        // Use case: Change backup folder path
        public OperationResult ChangeBackupFolderPath(string newBackupPath)
        {

            try
            {
                _settings.PersistBackupFolderPath(newBackupPath);
                return new OperationResult(OperationResultCode.Success, "Folder path changed.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error changing backup folder path: {ex.Message}");
                return new OperationResult(OperationResultCode.Error, $"Error changing backup folder path: {ex.Message}");
            }
        }

        // Retry logic for executing database actions
        private async Task ExecuteWithUnitOfWorkAsync(Func<SQLiteConnection, Task> action)
        {
            await using var uow = new UnitOfWork(_dbFactory);
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
    }
}
