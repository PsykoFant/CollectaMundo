using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Operation;
using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.DomainLogic.CardData;
using CollectaMundo.Infrastructure.CardDatabaseManagement;
using CollectaMundo.Infrastructure.CardDatabaseManagement.CardData;
using CollectaMundo.Infrastructure.RemoteLookups;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.Infrastructure.Shared.IO;
using CollectaMundo.Infrastructure.Shared.RemoteFiles;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;

namespace CollectaMundo.ApplicationServices.CardDatabaseManagement
{
    public class CardDatabaseManagementService(
        IAppSettings settings,
        IDbConnectionFactory dbFactory,
        IUnitOfWorkRunner uowRunner,
        ProgressSinks progressSinks,
        ICardDatabaseManagementRepo dbMgmtRepo,
        ICardDataRepo cardDataRepo,
        ICardPriceService priceService,
        IGenerateMissingPngService missingPngService,
        IRemoteLookups remoteLookups,
        IRemoteFileDownloader remoteFileDownloader,
        IGzipFileDecompressor gzipFileDecompressor)
        : ICardDatabaseManagementService
    {
        private readonly IAppSettings _settings = settings;
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly ProgressSinks _progressSinks = progressSinks ?? ProgressSinks.NoOp;
        private readonly ICardDatabaseManagementRepo _dbMgmtRepo = dbMgmtRepo;
        private readonly ICardDataRepo _cardDataRepo = cardDataRepo;
        private readonly ICardPriceService _priceService = priceService;
        private readonly IGenerateMissingPngService _missingPngService = missingPngService;
        private readonly IRemoteLookups _remoteLookups = remoteLookups;
        private readonly IRemoteFileDownloader _remoteFileDownloader = remoteFileDownloader;
        private readonly IGzipFileDecompressor _gzipFileDecompressor = gzipFileDecompressor;

        // Materialized application files. The configured remote URLs now point to their .gz artifacts.
        private readonly string _dbPath = Path.Combine(settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");
        private readonly string _pricesPath = Path.Combine(settings.UserDownloadsPath, "prices.json");
        private readonly string _tempDbPath = Path.Combine(settings.UserDownloadsPath, "AllPrintings.sqlite");
        public string BackupFolderPath => _settings.BackupFolderPath;

        // ============================================================
        // FIRST-TIME DATABASE PREPARATION
        // ============================================================
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

            // Always begin first-time preparation from a clean slate.
            try
            {
                CleanupPartialDatabaseFiles();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Cleanup] {ex.Message}");
            }

            try
            {
                // ---------------------------
                // Step 1. Acquire resources
                // ---------------------------

                var step1Name = "Step 1. Downloading card database and prices...";
                var acquireResult = await AcquireGzipArtifactsInParallelAsync(primarySourceUrl: _settings.CardDatabaseUrl, primaryDestinationPath: _dbPath, primaryLabel: "card database",
                        secondarySourceUrl: _settings.CardPricesUrl,
                        secondaryDestinationPath: _pricesPath,
                        secondaryLabel: "price file",
                        retryDelayInMs: defaultDelay,
                        stepName: step1Name,
                        cancellationToken: CancellationToken.None);

                if (acquireResult.Code != OperationResultCode.Success)
                {
                    Debug.WriteLine("[FirstTimeDbPrepOrchestrator] " + $"Resource acquisition failed: " + $"{acquireResult.Message}");

                    return new OperationResult(OperationResultCode.DownloadFailed, acquireResult.Message);
                }

                // ---------------------------
                // Steps 2–10. Prepare database
                // ---------------------------

                var prepResult = await PrepareDatabaseAsync(defaultDelay, displayStepStart: 2, stepsToRun: FullPrepSteps);

                if (prepResult.Code != OperationResultCode.Success)
                {
                    return new OperationResult(OperationResultCode.Error, prepResult.Message);
                }

                // prices.json is transient after successful import.
                TryDeleteTransientFile(_pricesPath);

                return new OperationResult(OperationResultCode.Success);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[FirstTimeDbPrepOrchestrator] " + $"Fatal error: {ex.Message}");

                return new OperationResult(OperationResultCode.Error, ex.Message);
            }
        }

        private static readonly IReadOnlyList<DbPrepStep>
            FullPrepSteps =
            [
                DbPrepStep.CreateTables,
                DbPrepStep.CreateIndices,
                DbPrepStep.BuildCanonicalOracleFaces,
                DbPrepStep.GenerateManaSymbols,
                DbPrepStep.GenerateManaCostImages,
                DbPrepStep.GenerateSetIcons,
                DbPrepStep.ImportPrices,
                DbPrepStep.CreateViews,
                DbPrepStep.OptimizeDatabase
            ];

        // ============================================================
        // CHECK FOR DATABASE UPDATE
        // ============================================================

        public async Task<OperationResult> CheckForDbUpdatesAsync(CancellationToken ct = default)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                // Step 1: Check internet connectivity
                var internetAvailable = await _remoteLookups.IsInternetAvailableAsync(ct);

                if (!internetAvailable)
                {
                    return new OperationResult(OperationResultCode.Error, "Internet not available - unable to check server...");
                }

                // Step 2: Query local DB
                int numberOfSetsInDb;

                try
                {
                    numberOfSetsInDb = await _uowRunner.ExecuteReadOnlyAsync(conn => _dbMgmtRepo.GetNumberOfSetsAsync(conn, ct));
                }
                catch (Exception ex)
                {
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
                    return new OperationResult(OperationResultCode.NeedsUpdate, $"Number of sets on server: {numberOfSetsOnServer}, " + $"number of sets in database: {numberOfSetsInDb}. " + "Update available!");
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

        // ============================================================
        // FULL DATABASE UPDATE
        // ============================================================

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
            // Step 1. Acquire resources
            // ---------------------------

            var step1Name = "Step 1. Downloading card database and prices...";

            var acquireResult = await AcquireGzipArtifactsInParallelAsync(primarySourceUrl: _settings.CardDatabaseUrl, primaryDestinationPath: _tempDbPath, primaryLabel: "card database",
                    secondarySourceUrl: _settings.CardPricesUrl,
                    secondaryDestinationPath: _pricesPath,
                    secondaryLabel: "price file",
                    retryDelayInMs: defaultDelay,
                    stepName: step1Name,
                    cancellationToken: ct);

            if (ct.IsCancellationRequested || acquireResult.Code == OperationResultCode.CancelledByUser)
            {
                return new OperationResult(OperationResultCode.CancelledByUser, "Update was cancelled by user during download.");
            }

            if (acquireResult.Code != OperationResultCode.Success)
            {
                Debug.WriteLine("[UpdateDbPrepOrchetrator] Resource acquisition failed: " + $"{acquireResult.Message}");

                return new OperationResult(OperationResultCode.DownloadFailed, acquireResult.Message);
            }

            // ---------------------------
            // Step 2. Copy tables from new DB
            // ---------------------------

            _progressSinks.ProgressBarVisible.Report(false);
            _progressSinks.CancelEnabled?.Report(false);
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

                            Debug.WriteLine("[CardDatabasePrep] " + "Copied new tables.");

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
            // Steps 3+. Prepare database
            // ---------------------------

            var prepResult =
                await PrepareDatabaseAsync(
                    defaultDelay,
                    displayStepStart: 3,
                    stepsToRun: UpdateDbSteps);

            if (prepResult.Code !=
                OperationResultCode.Success)
            {
                return new OperationResult(
                    OperationResultCode.Error,
                    prepResult.Message);
            }

            // Both are temporary update artifacts.
            TryDeleteTransientFile(_pricesPath);
            TryDeleteTransientFile(_tempDbPath);

            return new OperationResult(
                OperationResultCode.Success);
        }

        private static readonly IReadOnlyList<DbPrepStep>
            UpdateDbSteps =
            [
                DbPrepStep.CreateIndices,
                DbPrepStep.BuildCanonicalOracleFaces,
                DbPrepStep.GenerateManaSymbols,
                DbPrepStep.GenerateManaCostImages,
                DbPrepStep.GenerateSetIcons,
                DbPrepStep.ImportPrices,
                DbPrepStep.OptimizeDatabase
            ];

        // ============================================================
        // PRICE-ONLY UPDATE
        // ============================================================

        public async Task<OperationResult>
            UpdateCardPricesOrchetrator(
                int defaultDelay = 3000,
                CancellationToken ct = default)
        {
            // ---------------------------
            // Step 0. Online check
            // ---------------------------

            if (!await _remoteLookups
                    .IsInternetAvailableAsync(ct))
            {
                return new OperationResult(
                    OperationResultCode.NoInternet,
                    "Internet not available");
            }

            _progressSinks.Headline.Report(
                "Updating card prices - please wait ...");

            _progressSinks.ProgressBarVisible.Report(true);

            // ---------------------------
            // Step 1. Acquire price file
            // ---------------------------

            var step1Name =
                "Step 1. Downloading price file...";

            var acquireResult =
                await AcquireGzipArtifactWithRetryAsync(
                    sourceUrl:
                        _settings.CardPricesUrl,
                    destinationPath:
                        _pricesPath,
                    label:
                        "price file",
                    reportProgress:
                        true,
                    retryDelayInMs:
                        defaultDelay,
                    stepName:
                        step1Name,
                    cancellationToken:
                        ct);

            if (ct.IsCancellationRequested ||
                acquireResult.Code ==
                OperationResultCode.CancelledByUser)
            {
                return new OperationResult(
                    OperationResultCode.CancelledByUser,
                    "Update was cancelled by user during download.");
            }

            if (acquireResult.Code !=
                OperationResultCode.Success)
            {
                Debug.WriteLine(
                    "[UpdateCardPricesOrchetrator] " +
                    $"Resource acquisition failed: " +
                    $"{acquireResult.Message}");

                return new OperationResult(
                    OperationResultCode.DownloadFailed,
                    acquireResult.Message);
            }

            // ---------------------------
            // Step 2+. Import prices
            // ---------------------------

            _progressSinks.ProgressBarVisible.Report(false);

            _progressSinks.CancelEnabled?.Report(false);

            _progressSinks.Step.Report(
                "Step 2. Importing prices...");

            var prepResult =
                await PrepareDatabaseAsync(
                    defaultDelay,
                    displayStepStart: 2,
                    stepsToRun: UpdatePricesSteps);

            if (prepResult.Code !=
                OperationResultCode.Success)
            {
                return new OperationResult(
                    OperationResultCode.Error,
                    prepResult.Message);
            }

            TryDeleteTransientFile(_pricesPath);

            return new OperationResult(
                OperationResultCode.Success);
        }

        private static readonly IReadOnlyList<DbPrepStep>
            UpdatePricesSteps =
            [
                DbPrepStep.ImportPrices,
                DbPrepStep.OptimizeDatabase
            ];

        // ============================================================
        // DATABASE PREPARATION PIPELINE
        // ============================================================

        private async Task<OperationResult>
            PrepareDatabaseAsync(
                int defaultDelay,
                int displayStepStart,
                IReadOnlyList<DbPrepStep> stepsToRun)
        {
            var stepMap =
                GetPrepSteps()
                    .ToDictionary(x => x.Key);

            foreach (var stepKey in stepsToRun)
            {
                var (_, label, work, showProgress) =
                    stepMap[stepKey];

                var stepLabel =
                    $"Step {displayStepStart++}. {label}";

                Debug.WriteLine(
                    $"Starting: {stepLabel}");

                _progressSinks
                    .ProgressBarVisible
                    .Report(showProgress);

                _progressSinks.Detail.Report(
                    string.Empty);

                var result =
                    await RetryHelper.RetryLoopAsync(
                        async () =>
                        {
                            await work();

                            return new OperationResult(
                                OperationResultCode.Success,
                                $"{stepLabel} completed.");
                        },
                        retryDelayInMs:
                            defaultDelay,
                        maxRetries:
                            3,
                        stepName:
                            stepLabel,
                        stepNameAndNumberProgress:
                            _progressSinks.Step,
                        stepDetailAndErrorProgress:
                            _progressSinks.Detail);

                if (result.Code !=
                    OperationResultCode.Success)
                {
                    return result;
                }
            }

            return new OperationResult(
                OperationResultCode.Success,
                "Database preparation completed.");
        }

        private List<(
            DbPrepStep Key,
            string Label,
            Func<Task> Work,
            bool ShowProgress)> GetPrepSteps()
        {
            return
            [
                (
                    DbPrepStep.CreateTables,
                    "Creating custom tables...",
                    () => _uowRunner.ExecuteWriteAsync(
                        async (conn, tx) =>
                        {
                            await _dbMgmtRepo
                                .CreateTablesAsync(
                                    conn,
                                    tx);

                            return (
                                Result: true,
                                Commit: true);
                        }),
                    false
                ),

                (
                    DbPrepStep.CreateIndices,
                    "Creating indices...",
                    () => _uowRunner.ExecuteWriteAsync(
                        async (conn, tx) =>
                        {
                            await _dbMgmtRepo
                                .CreateIndicesAsync(
                                    conn,
                                    tx);

                            return (
                                Result: true,
                                Commit: true);
                        }),
                    false
                ),

                (
                    DbPrepStep.BuildCanonicalOracleFaces,
                    "Preparing canonical card data...",
                    () => _uowRunner.ExecuteWriteAsync(
                        async (conn, tx) =>
                        {
                            await BuildCanonicalOracleFacesAsync(
                                conn,
                                tx);

                            return (
                                Result: true,
                                Commit: true);
                        }),
                    false
                ),

                (
                    DbPrepStep.GenerateManaSymbols,
                    "Generating mana symbols...",
                    () => _missingPngService
                        .GenerateMissingManaSymbolImagesAsync(
                            _progressSinks.Percent),
                    true
                ),

                (
                    DbPrepStep.GenerateManaCostImages,
                    "Generating mana cost images...",
                    () => _missingPngService
                        .GenerateMissingManaCostImagesAsync(
                            _progressSinks.Percent),
                    true
                ),

                (
                    DbPrepStep.GenerateSetIcons,
                    "Generating set icon images...",
                    () => _missingPngService
                        .GenerateMissingKeyRuneImagesAsync(
                            _progressSinks.Percent),
                    true
                ),

                (
                    DbPrepStep.ImportPrices,
                    "Processing card prices...",
                    async () =>
                    {
                        var priceImportResult =
                            await _uowRunner.ExecuteWriteAsync(
                                async (conn, tx) =>
                                {
                                    var result =
                                        await _priceService
                                            .ImportPricesFromJsonAsync(
                                                _pricesPath,
                                                conn,
                                                tx,
                                                _progressSinks.Detail,
                                                _progressSinks.Percent);

                                    return (
                                        Result: result,
                                        Commit: result is not null);
                                });

                        if (priceImportResult is not null)
                        {
                            if (_settings.PriceInfo is null)
                            {
                                throw new InvalidOperationException(
                                    "PriceInfo must be initialized " +
                                    "before importing prices.");
                            }

                            var retailer =
                                _settings.PriceInfo.Retailer;

                            _settings.PersistPriceInfo(
                                priceImportResult.JsonDate,
                                retailer);
                        }
                    },
                    true
                ),

                (
                    DbPrepStep.CreateViews,
                    "Creating views...",
                    () => _uowRunner.ExecuteWriteAsync(
                        async (conn, tx) =>
                        {
                            await _dbMgmtRepo
                                .CreateViewsAsync(
                                    conn,
                                    tx);

                            return (
                                Result: true,
                                Commit: true);
                        }),
                    false
                ),

                (
                    DbPrepStep.OptimizeDatabase,
                    "Optimizing database...",
                    () => Task.Run(
                        () => ExecuteWithConnectionAsync(
                            conn =>
                                _dbMgmtRepo
                                    .OptimizeAsync(conn))),
                    false
                )
            ];

            async Task ExecuteWithConnectionAsync(
                Func<SQLiteConnection, Task> action)
            {
                await using var conn =
                    await _dbFactory.OpenConnectionAsync();

                await action(conn);
            }
        }

        // ============================================================
        // CANONICAL ORACLE FACE BUILD
        // ============================================================

        private async Task BuildCanonicalOracleFacesAsync(
            SQLiteConnection conn,
            SQLiteTransaction tx)
        {
            var candidates =
                await _cardDataRepo
                    .GetOracleFaceCandidatesAsync(
                        conn,
                        tx);

            var canonicalFaces =
                OracleFaceCanonicalizer
                    .Canonicalize(candidates);

            if (canonicalFaces.Count == 0)
            {
                throw new InvalidOperationException(
                    "Canonical Oracle face generation " +
                    "produced no rows.");
            }

            var inserted =
                await _cardDataRepo
                    .RebuildCanonicalOracleFacesAsync(
                        conn,
                        tx,
                        canonicalFaces);

            if (inserted !=
                canonicalFaces.Count)
            {
                throw new InvalidOperationException(
                    $"Canonical Oracle face rebuild inserted " +
                    $"{inserted:N0} rows, but " +
                    $"{canonicalFaces.Count:N0} were expected.");
            }

            var conflicts =
                canonicalFaces.Count(
                    face => face.HasConflict);

            var ambiguous =
                canonicalFaces.Count(
                    face => face.IsAmbiguous);

            Debug.WriteLine(
                "[CardDatabasePrep] Canonical Oracle faces: " +
                $"{canonicalFaces.Count:N0}, " +
                $"conflicts={conflicts:N0}, " +
                $"ambiguous={ambiguous:N0}");
        }

        // ============================================================
        // COLLECTION EXPORT
        // ============================================================

        public async Task<OperationResult>
            ExportCollectionAsync(
                CancellationToken ct = default)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                var filePath =
                    await _uowRunner
                        .ExecuteReadOnlyAsync(
                            conn =>
                                _dbMgmtRepo
                                    .ExportCollectionAsync(
                                        conn,
                                        _settings.BackupFolderPath,
                                        ct));

                ct.ThrowIfCancellationRequested();

                if (filePath == null)
                {
                    return new OperationResult(
                        OperationResultCode.Empty,
                        string.Empty);
                }

                return new OperationResult(
                    OperationResultCode.Success,
                    filePath);
            }
            catch (OperationCanceledException)
            {
                return new OperationResult(
                    OperationResultCode.CancelledByUser,
                    "User cancelled backup");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Error creating CSV backup: " +
                    $"{ex.Message}");

                return new OperationResult(
                    OperationResultCode.Error,
                    $"Error creating CSV backup: " +
                    $"{ex.Message}");
            }
        }

        // ============================================================
        // BACKUP LOCATION
        // ============================================================

        public OperationResult ChangeBackupFolderPath(
            string newBackupPath)
        {
            try
            {
                _settings.PersistBackupFolderPath(
                    newBackupPath);

                return new OperationResult(
                    OperationResultCode.Success,
                    "Folder path changed.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Error changing backup folder path: " +
                    $"{ex.Message}");

                return new OperationResult(
                    OperationResultCode.Error,
                    $"Error changing backup folder path: " +
                    $"{ex.Message}");
            }
        }

        // ============================================================
        // REMOTE RESOURCE ACQUISITION
        // ============================================================

        private async Task<OperationResult>
            AcquireGzipArtifactsInParallelAsync(
                string primarySourceUrl,
                string primaryDestinationPath,
                string primaryLabel,
                string secondarySourceUrl,
                string secondaryDestinationPath,
                string secondaryLabel,
                int retryDelayInMs,
                string stepName,
                CancellationToken cancellationToken)
        {
            using var linkedCts =
                CancellationTokenSource
                    .CreateLinkedTokenSource(
                        cancellationToken);

            async Task<OperationResult> RunAsync(
                string sourceUrl,
                string destinationPath,
                string label,
                bool reportProgress)
            {
                var result =
                    await AcquireGzipArtifactWithRetryAsync(
                        sourceUrl,
                        destinationPath,
                        label,
                        reportProgress,
                        retryDelayInMs,
                        stepName,
                        linkedCts.Token);

                // Terminal failure of either required resource
                // makes the complete acquisition invalid.
                if (result.Code !=
                    OperationResultCode.Success)
                {
                    linkedCts.Cancel();
                }

                return result;
            }

            var primaryTask =
                RunAsync(
                    primarySourceUrl,
                    primaryDestinationPath,
                    primaryLabel,
                    reportProgress: true);

            var secondaryTask =
                RunAsync(
                    secondarySourceUrl,
                    secondaryDestinationPath,
                    secondaryLabel,
                    reportProgress: false);

            OperationResult[] results;

            try
            {
                results =
                    await Task.WhenAll(
                        primaryTask,
                        secondaryTask);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return new OperationResult(
                    OperationResultCode.CancelledByUser,
                    "Resource acquisition was cancelled.");
            }
            catch (Exception ex)
            {
                linkedCts.Cancel();

                Debug.WriteLine(
                    "[CardDatabasePrep] " +
                    $"Parallel acquisition failed: " +
                    $"{ex.Message}");

                return new OperationResult(
                    OperationResultCode.Error,
                    ex.Message);
            }

            // Prefer the real failure over the sibling operation
            // that was merely cancelled as a consequence.
            foreach (var result in results)
            {
                if (result.Code !=
                        OperationResultCode.Success &&
                    result.Code !=
                        OperationResultCode.CancelledByUser)
                {
                    return result;
                }
            }

            foreach (var result in results)
            {
                if (result.Code !=
                    OperationResultCode.Success)
                {
                    return result;
                }
            }

            return new OperationResult(
                OperationResultCode.Success,
                "Card database and price resources acquired.");
        }

        private async Task<OperationResult>
            AcquireGzipArtifactWithRetryAsync(
                string sourceUrl,
                string destinationPath,
                string label,
                bool reportProgress,
                int retryDelayInMs,
                string stepName,
                CancellationToken cancellationToken)
        {
            var stepProgress =
                reportProgress
                    ? _progressSinks.Step
                    : ProgressSinks.NoOp.Step;

            var detailProgress =
                reportProgress
                    ? _progressSinks.Detail
                    : ProgressSinks.NoOp.Detail;

            return await RetryHelper.RetryLoopAsync(
                async () =>
                {
                    await AcquireGzipArtifactAsync(
                        sourceUrl,
                        destinationPath,
                        label,
                        reportProgress,
                        cancellationToken);

                    return new OperationResult(
                        OperationResultCode.Success,
                        $"{label} acquired successfully.");
                },
                retryDelayInMs:
                    retryDelayInMs,
                maxRetries:
                    3,
                stepName:
                    stepName,
                stepNameAndNumberProgress:
                    stepProgress,
                stepDetailAndErrorProgress:
                    detailProgress,
                cancelToken:
                    cancellationToken);
        }

        private async Task AcquireGzipArtifactAsync(
            string sourceUrl,
            string destinationPath,
            string label,
            bool reportProgress,
            CancellationToken cancellationToken)
        {
            var gzipPath =
                destinationPath + ".download.gz";

            try
            {
                IProgress<FileTransferProgress>? progress =
                    reportProgress
                        ? CreateTransferProgress()
                        : null;

                // ---------------------------
                // Download compressed artifact
                // ---------------------------

                if (reportProgress)
                {
                    _progressSinks.Detail.Report(
                        $"Downloading {label}...");

                    _progressSinks.Percent.Report(0);

                    _progressSinks
                        .ProgressBarIndeterminate
                        .Report(false);
                }

                await _remoteFileDownloader.DownloadAsync(
                    sourceUrl,
                    gzipPath,
                    progress,
                    cancellationToken);

                // ---------------------------
                // Decompress into application file
                // ---------------------------

                if (reportProgress)
                {
                    _progressSinks.Detail.Report(
                        $"Decompressing {label}...");

                    _progressSinks.Percent.Report(0);

                    _progressSinks
                        .ProgressBarIndeterminate
                        .Report(false);
                }

                await _gzipFileDecompressor.DecompressAsync(
                    gzipPath,
                    destinationPath,
                    progress,
                    cancellationToken);

                if (reportProgress)
                {
                    _progressSinks.Percent.Report(100);

                    _progressSinks
                        .ProgressBarIndeterminate
                        .Report(false);
                }
            }
            finally
            {
                if (reportProgress)
                {
                    _progressSinks
                        .ProgressBarIndeterminate
                        .Report(false);
                }

                TryDeleteTransientFile(
                    gzipPath);
            }
        }

        private IProgress<FileTransferProgress>
            CreateTransferProgress()
        {
            return new Progress<FileTransferProgress>(
                progress =>
                {
                    if (progress.Percent is int percent)
                    {
                        _progressSinks
                            .ProgressBarIndeterminate
                            .Report(false);

                        _progressSinks
                            .Percent
                            .Report(percent);
                    }
                    else
                    {
                        _progressSinks
                            .ProgressBarIndeterminate
                            .Report(true);
                    }
                });
        }

        // ============================================================
        // FILE CLEANUP
        // ============================================================

        private void CleanupPartialDatabaseFiles()
        {
            var filesToDelete =
                new[]
                {
                    // Materialized DB + SQLite sidecars
                    _dbPath,
                    _dbPath + "-shm",
                    _dbPath + "-wal",

                    // First-time DB acquisition staging
                    _dbPath + ".download.gz",
                    _dbPath + ".download.gz.part",
                    _dbPath + ".decompressing",

                    // Update DB materialization/staging
                    _tempDbPath,
                    _tempDbPath + "-shm",
                    _tempDbPath + "-wal",
                    _tempDbPath + ".download.gz",
                    _tempDbPath + ".download.gz.part",
                    _tempDbPath + ".decompressing",

                    // Price materialization/staging
                    _pricesPath,
                    _pricesPath + ".download.gz",
                    _pricesPath + ".download.gz.part",
                    _pricesPath + ".decompressing"
                };

            foreach (var file in filesToDelete)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }

            Debug.WriteLine(
                "[CardDatabasePrep] " +
                "Deleted corrupt or partial DB file(s).");
        }

        private static void TryDeleteTransientFile(
            string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                // Cleanup of an already-successful operation should
                // not turn that operation into a functional failure.
                Debug.WriteLine(
                    $"[Cleanup] Unable to delete " +
                    $"'{filePath}': {ex.Message}");
            }
        }

        // ============================================================
        // DATABASE PREP STEPS
        // ============================================================

        private enum DbPrepStep
        {
            CreateTables,
            CreateIndices,
            BuildCanonicalOracleFaces,
            GenerateManaSymbols,
            GenerateManaCostImages,
            GenerateSetIcons,
            ImportPrices,
            CreateViews,
            OptimizeDatabase
        }
    }
}
