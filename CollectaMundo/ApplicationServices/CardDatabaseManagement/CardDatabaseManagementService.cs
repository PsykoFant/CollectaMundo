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
        IGzipFileDecompressor gzipFileDecompressor) : ICardDatabaseManagementService
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

        // Materialized application files. Remote CardDatabaseUrl / CardPricesUrl point to .gz artifacts.
        private readonly string _dbPath = Path.Combine(settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");
        private readonly string _pricesPath = Path.Combine(settings.UserDownloadsPath, "prices.json");
        private readonly string _tempDbPath = Path.Combine(settings.UserDownloadsPath, "AllPrintings.sqlite");

        public string BackupFolderPath => _settings.BackupFolderPath;


        #region USE CASE: FIRST-TIME DATABASE PREPARATION

        public Task<OperationResult> FirstTimeDbPrepOrchestrator(int defaultDelay = 3000)
        {
            return ExecuteOnlineOperationAsync(headline: "Performing first-time setup of card database - please wait ...",

                operation: async ct =>
                {
                    // A first-time build must start from a known clean state.
                    CleanupFirstTimeArtifacts();

                    var acquireResult = await AcquireDatabaseAndPricesAsync(databaseDestinationPath: _dbPath, retryDelayInMs: defaultDelay, cancellationToken: ct);

                    if (acquireResult.Code != OperationResultCode.Success)
                    {
                        return acquireResult;
                    }

                    var prepResult = await PrepareDatabaseAsync(defaultDelay, displayStepStart: 2, stepsToRun: FirstTimePrepSteps);

                    if (prepResult.Code != OperationResultCode.Success)
                    {
                        return prepResult;
                    }

                    // Price JSON is only an intermediate import artifact.
                    TryDeleteTransientFile(_pricesPath);

                    return new OperationResult(OperationResultCode.Success);
                },

                cancellationToken: CancellationToken.None);
        }

        private static readonly IReadOnlyList<DbPrepStep> FirstTimePrepSteps =
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

        #endregion

        #region USE CASE: CHECK FOR DATABASE UPDATE        
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

        #endregion

        #region USE CASE: FULL DATABASE UPDATE
        public Task<OperationResult> UpdateDbPrepOrchetrator(int defaultDelay = 3000, CancellationToken ct = default)
        {
            return ExecuteOnlineOperationAsync(headline: "Updating card database - please wait ...",

                operation: async cancellationToken =>
                {
                    // Never let a previous interrupted update become input to the new update.
                    CleanupUpdateAcquisitionArtifacts();

                    var acquireResult = await AcquireDatabaseAndPricesAsync(databaseDestinationPath: _tempDbPath, retryDelayInMs: defaultDelay, cancellationToken);

                    if (acquireResult.Code != OperationResultCode.Success)
                    {
                        return acquireResult;
                    }

                    // The destructive DB phase starts here. Cancellation is intentionally disabled from this point.
                    _progressSinks.CancelEnabled?.Report(false);

                    var copyResult = await CopyUpdatedTablesAsync();

                    if (copyResult.Code != OperationResultCode.Success)
                    {
                        return copyResult;
                    }

                    var prepResult = await PrepareDatabaseAsync(defaultDelay, displayStepStart: 3, stepsToRun: UpdateDbSteps);

                    if (prepResult.Code != OperationResultCode.Success)
                    {
                        return prepResult;
                    }

                    TryDeleteTransientFile(_pricesPath);
                    TryDeleteTransientFile(_tempDbPath);

                    return new OperationResult(OperationResultCode.Success);
                },

                cancellationToken: ct);
        }
        private static readonly IReadOnlyList<DbPrepStep> UpdateDbSteps =
        [
            DbPrepStep.CreateIndices,
            DbPrepStep.BuildCanonicalOracleFaces,
            DbPrepStep.GenerateManaSymbols,
            DbPrepStep.GenerateManaCostImages,
            DbPrepStep.GenerateSetIcons,
            DbPrepStep.ImportPrices,
            DbPrepStep.OptimizeDatabase
        ];

        #endregion

        #region USE CASE: PRICE-ONLY UPDATE        
        public Task<OperationResult> UpdateCardPricesOrchetrator(int defaultDelay = 3000, CancellationToken ct = default)
        {
            return ExecuteOnlineOperationAsync(headline: "Updating card prices - please wait ...",

                operation: async cancellationToken =>
                {
                    CleanupPriceAcquisitionArtifacts();

                    var acquireResult = await AcquirePricesAsync(retryDelayInMs: defaultDelay, cancellationToken);

                    if (acquireResult.Code != OperationResultCode.Success)
                    {
                        return acquireResult;
                    }

                    _progressSinks.CancelEnabled?.Report(false);

                    var prepResult = await PrepareDatabaseAsync(defaultDelay, displayStepStart: 2, stepsToRun: UpdatePricesSteps);

                    if (prepResult.Code != OperationResultCode.Success)
                    {
                        return prepResult;
                    }

                    TryDeleteTransientFile(_pricesPath);

                    return new OperationResult(OperationResultCode.Success);
                },

                cancellationToken: ct);
        }
        private static readonly IReadOnlyList<DbPrepStep> UpdatePricesSteps =
        [
            DbPrepStep.ImportPrices,
            DbPrepStep.OptimizeDatabase
        ];

        #endregion

        #region SHARED ONLINE OPERATION SHELL

        private async Task<OperationResult> ExecuteOnlineOperationAsync(string headline, Func<CancellationToken, Task<OperationResult>> operation, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!await _remoteLookups.IsInternetAvailableAsync(cancellationToken))
                {
                    return new OperationResult(OperationResultCode.NoInternet, "Internet not available");
                }

                _progressSinks.Headline.Report(headline);

                return await operation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return new OperationResult(OperationResultCode.CancelledByUser, "Operation was cancelled by user.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CardDatabaseManagement] Operation failed: {ex}");

                return new OperationResult(OperationResultCode.Error, ex.Message);
            }
            finally
            {
                _progressSinks.ProgressBarIndeterminate.Report(false);
            }
        }

        #endregion

        #region RESOURCE ACQUISITION        
        private Task<OperationResult> AcquireDatabaseAndPricesAsync(string databaseDestinationPath, int retryDelayInMs, CancellationToken cancellationToken)
        {
            return AcquireArtifactsAsync(
                artifacts: [
                    new GzipArtifactRequest(SourceUrl: _settings.CardDatabaseUrl, DestinationPath: databaseDestinationPath, Label: "card database", ReportProgress: true, IsSqlite: true),
                    new GzipArtifactRequest(SourceUrl: _settings.CardPricesUrl, DestinationPath: _pricesPath, Label: "price file", ReportProgress: false, IsSqlite: false)],
                stepName: "Step 1. Downloading card database and prices...",
                retryDelayInMs: retryDelayInMs,
                cancellationToken: cancellationToken);
        }
        private Task<OperationResult> AcquirePricesAsync(int retryDelayInMs, CancellationToken cancellationToken)
        {
            return AcquireArtifactsAsync(
                artifacts:
                [
                    new GzipArtifactRequest( SourceUrl: _settings.CardPricesUrl, DestinationPath: _pricesPath, Label: "price file", ReportProgress: true, IsSqlite: false)
                ],
                stepName: "Step 1. Downloading price file...",
                retryDelayInMs: retryDelayInMs,
                cancellationToken: cancellationToken);
        }
        private async Task<OperationResult> AcquireArtifactsAsync(IReadOnlyList<GzipArtifactRequest> artifacts, string stepName, int retryDelayInMs, CancellationToken cancellationToken)
        {
            if (artifacts.Count == 0)
            {
                return new OperationResult(OperationResultCode.Success);
            }

            _progressSinks.ProgressBarVisible.Report(true);
            _progressSinks.ProgressBarIndeterminate.Report(false);
            _progressSinks.Percent.Report(0);
            _progressSinks.Step.Report(stepName);
            _progressSinks.Detail.Report(string.Empty);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            async Task<OperationResult> RunArtifactAsync(GzipArtifactRequest artifact)
            {
                var result = await AcquireArtifactWithRetryAsync(artifact, stepName, retryDelayInMs, linkedCts.Token);

                // Every artifact in this batch is required. A terminal failure makes the complete acquisition invalid.
                if (result.Code != OperationResultCode.Success)
                {
                    linkedCts.Cancel();
                }

                return result;
            }

            var tasks = artifacts.Select(RunArtifactAsync).ToArray();

            OperationResult[] results;

            try
            {
                results = await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                TryCleanupAcquiredArtifacts(artifacts);

                return new OperationResult(OperationResultCode.CancelledByUser, "Resource acquisition was cancelled.");
            }
            catch (Exception ex)
            {
                linkedCts.Cancel();

                TryCleanupAcquiredArtifacts(artifacts);

                Debug.WriteLine($"[ResourceAcquisition] Unexpected failure: {ex}");

                return new OperationResult(OperationResultCode.DownloadFailed, ex.Message);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                TryCleanupAcquiredArtifacts(artifacts);

                return new OperationResult(OperationResultCode.CancelledByUser, "Resource acquisition was cancelled.");
            }

            // Prefer the actual failure over a sibling task that was merely cancelled because another artifact failed.
            foreach (var result in results)
            {
                if (result.Code != OperationResultCode.Success && result.Code != OperationResultCode.CancelledByUser)
                {
                    TryCleanupAcquiredArtifacts(artifacts);

                    return new OperationResult(OperationResultCode.DownloadFailed, result.Message);
                }
            }

            if (results.Any(result => result.Code == OperationResultCode.CancelledByUser))
            {
                TryCleanupAcquiredArtifacts(artifacts);

                return new OperationResult(OperationResultCode.CancelledByUser, "Resource acquisition was cancelled.");
            }

            return new OperationResult(OperationResultCode.Success, "Required resources acquired successfully.");
        }
        private async Task<OperationResult> AcquireArtifactWithRetryAsync(GzipArtifactRequest artifact, string stepName, int retryDelayInMs, CancellationToken cancellationToken)
        {
            var stepProgress = artifact.ReportProgress ? _progressSinks.Step : ProgressSinks.NoOp.Step;
            var detailProgress = artifact.ReportProgress ? _progressSinks.Detail : ProgressSinks.NoOp.Detail;

            try
            {
                return await RetryHelper.RetryLoopAsync(async () =>
                    {
                        await AcquireGzipArtifactAsync(artifact, cancellationToken);

                        return new OperationResult(OperationResultCode.Success, $"{artifact.Label} acquired successfully.");
                    },
                    retryDelayInMs: retryDelayInMs,
                    maxRetries: 3,
                    stepName: stepName,
                    stepNameAndNumberProgress: stepProgress,
                    stepDetailAndErrorProgress: detailProgress,
                    cancelToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return new OperationResult(OperationResultCode.CancelledByUser, $"{artifact.Label} acquisition was cancelled.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResourceAcquisition] {artifact.Label} failed: {ex}");

                return new OperationResult(OperationResultCode.Error, $"{artifact.Label} failed: {ex.Message}");
            }
        }
        private async Task AcquireGzipArtifactAsync(GzipArtifactRequest artifact, CancellationToken cancellationToken)
        {
            var gzipPath = artifact.DestinationPath + ".download.gz";

            try
            {
                IProgress<FileTransferProgress>? progress = artifact.ReportProgress ? CreateTransferProgress() : null;

                // ---------------------------
                // Download compressed artifact
                // ---------------------------

                if (artifact.ReportProgress)
                {
                    BeginTransferPhase($"Downloading {artifact.Label}...");
                }

                await _remoteFileDownloader.DownloadAsync(artifact.SourceUrl, gzipPath, progress, cancellationToken);

                // ---------------------------
                // Decompress to materialized file
                // ---------------------------

                if (artifact.ReportProgress)
                {
                    BeginTransferPhase($"Decompressing {artifact.Label}...");
                }

                await _gzipFileDecompressor.DecompressAsync(gzipPath, artifact.DestinationPath, progress, cancellationToken);

                if (artifact.ReportProgress)
                {
                    _progressSinks.Percent.Report(100);
                    _progressSinks.ProgressBarIndeterminate.Report(false);
                }
            }

            finally
            {
                // Both infrastructure primitives use staging files, but this is intentionally defensive at the use-case boundary.
                TryDeleteTransientFile(gzipPath);
                TryDeleteTransientFile(gzipPath + ".part");
                TryDeleteTransientFile(artifact.DestinationPath + ".decompressing");

                if (artifact.ReportProgress)
                {
                    _progressSinks.ProgressBarIndeterminate.Report(false);
                }
            }
        }
        private void BeginTransferPhase(string detail)
        {
            _progressSinks.Detail.Report(detail);
            _progressSinks.Percent.Report(0);
            _progressSinks.ProgressBarIndeterminate.Report(false);
        }
        private IProgress<FileTransferProgress> CreateTransferProgress()
        {
            return new InlineProgress<FileTransferProgress>(progress =>
            {
                if (progress.Percent is int percent)
                {
                    _progressSinks.ProgressBarIndeterminate.Report(false);
                    _progressSinks.Percent.Report(percent);
                }
                else
                {
                    _progressSinks.ProgressBarIndeterminate.Report(true);
                }
            });
        }
        private sealed class InlineProgress<T>(Action<T> onReport) : IProgress<T>
        {
            public void Report(T value)
            {
                onReport(value);
            }
        }

        #endregion

        #region DATABASE TABLE COPY
        private async Task<OperationResult> CopyUpdatedTablesAsync()
        {
            _progressSinks.ProgressBarVisible.Report(false);
            _progressSinks.ProgressBarIndeterminate.Report(false);
            _progressSinks.Step.Report("Step 2. Copying new tables...");

            try
            {
                // Preserve the existing non-cancellable behavior once the destructive database update phase has started.
                await Task.Run(
                    async () =>
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
                    },
                    CancellationToken.None);

                return new OperationResult(OperationResultCode.Success, "New database tables copied successfully.");
            }
            catch (Exception ex)
            {
                _progressSinks.Detail.Report($"Table copy failed: {ex.Message}");

                return new OperationResult(OperationResultCode.Error, $"Table copy failed: {ex.Message}");
            }
        }

        #endregion

        #region DATABASE PREPARATION PIPELINE
        private async Task<OperationResult> PrepareDatabaseAsync(int defaultDelay, int displayStepStart, IReadOnlyList<DbPrepStep> stepsToRun)
        {
            var stepMap = GetPrepSteps().ToDictionary(x => x.Key);

            foreach (var stepKey in stepsToRun)
            {
                var (_, label, work, showProgress) = stepMap[stepKey];
                var stepLabel = $"Step {displayStepStart++}. {label}";

                Debug.WriteLine($"Starting: {stepLabel}");

                _progressSinks.ProgressBarVisible.Report(showProgress);
                _progressSinks.ProgressBarIndeterminate.Report(false);
                _progressSinks.Detail.Report(string.Empty);

                var result = await RetryHelper.RetryLoopAsync(async () =>
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
                (DbPrepStep.CreateTables, "Creating custom tables...", () => _uowRunner.ExecuteWriteAsync(async (conn, tx) =>  {await _dbMgmtRepo.CreateTablesAsync(conn,  tx); return (Result: true, Commit: true);}), false),
                (DbPrepStep.CreateIndices, "Creating indices...", () => _uowRunner.ExecuteWriteAsync(async (conn, tx) => {await _dbMgmtRepo.CreateIndicesAsync(conn, tx); return (Result: true, Commit: true);}), false),
                (DbPrepStep.BuildCanonicalOracleFaces, "Preparing canonical card data...", () => _uowRunner.ExecuteWriteAsync(async (conn, tx) => {await BuildCanonicalOracleFacesAsync( conn, tx); return (Result: true, Commit: true);}), false),
                (DbPrepStep.GenerateManaSymbols, "Generating mana symbols...", () => _missingPngService.GenerateMissingManaSymbolImagesAsync(_progressSinks.Percent), true),
                (DbPrepStep.GenerateManaCostImages, "Generating mana cost images...", () => _missingPngService.GenerateMissingManaCostImagesAsync(_progressSinks.Percent), true),
                (DbPrepStep.GenerateSetIcons, "Generating set icon images...", () => _missingPngService.GenerateMissingKeyRuneImagesAsync( _progressSinks.Percent), true),
                (DbPrepStep.ImportPrices, "Processing card prices...", async () => {var priceImportResult = await _uowRunner.ExecuteWriteAsync(async (conn, tx) => {var result = await _priceService.ImportPricesFromJsonAsync(_pricesPath, conn, tx, _progressSinks.Detail, _progressSinks.Percent); return (Result: result, Commit: result is not null);});
                    if (priceImportResult is not null)
                        {
                            if (_settings.PriceInfo is null)
                            {
                                throw new InvalidOperationException("PriceInfo must be initialized before importing prices.");
                            }

                            var retailer = _settings.PriceInfo.Retailer;

                            _settings.PersistPriceInfo(priceImportResult.JsonDate, retailer);
                        }
                    },
                    true
                ),
                (DbPrepStep.CreateViews, "Creating views...", () => _uowRunner.ExecuteWriteAsync(async (conn, tx) => {await _dbMgmtRepo.CreateViewsAsync(conn, tx); return (Result: true, Commit: true);}), false),
                (DbPrepStep.OptimizeDatabase, "Optimizing database...", () => Task.Run(() => ExecuteWithConnectionAsync(conn => _dbMgmtRepo.OptimizeAsync(conn))), false)
            ];

            async Task ExecuteWithConnectionAsync(Func<SQLiteConnection, Task> action)
            {
                await using var conn = await _dbFactory.OpenConnectionAsync();

                await action(conn);
            }
        }

        #endregion

        #region CANONICAL ORACLE FACE BUILD
        private async Task BuildCanonicalOracleFacesAsync(SQLiteConnection conn, SQLiteTransaction tx)
        {
            var candidates = await _cardDataRepo.GetOracleFaceCandidatesAsync(conn, tx);
            var canonicalFaces = OracleFaceCanonicalizer.Canonicalize(candidates);
            if (canonicalFaces.Count == 0)
            {
                throw new InvalidOperationException("Canonical Oracle face generation produced no rows.");
            }

            var inserted = await _cardDataRepo.RebuildCanonicalOracleFacesAsync(conn, tx, canonicalFaces);

            if (inserted != canonicalFaces.Count)
            {
                throw new InvalidOperationException($"Canonical Oracle face rebuild inserted {inserted:N0} " + $"rows, but {canonicalFaces.Count:N0} were expected.");
            }

            var conflicts = canonicalFaces.Count(face => face.HasConflict);
            var ambiguous = canonicalFaces.Count(face => face.IsAmbiguous);

            Debug.WriteLine($"[CardDatabasePrep] Canonical Oracle faces: {canonicalFaces.Count:N0}, conflicts={conflicts:N0}, ambiguous={ambiguous:N0}");
        }

        #endregion

        #region USE CASE: COLLECTION EXPORT
        public async Task<OperationResult> ExportCollectionAsync(CancellationToken ct = default)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                var filePath = await _uowRunner.ExecuteReadOnlyAsync(conn => _dbMgmtRepo.ExportCollectionAsync(conn, _settings.BackupFolderPath, ct));

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

        #endregion

        #region USE CASE: CHANGE BACKUP FOLDER PATH
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

        #endregion

        #region ACQUISITION CLEANUP
        private void CleanupFirstTimeArtifacts()
        {
            DeleteArtifactFamily(_dbPath, includeSqliteSidecars: true);

            // Also remove leftovers from an interrupted previous update.
            DeleteArtifactFamily(_tempDbPath, includeSqliteSidecars: true);

            DeleteArtifactFamily(_pricesPath, includeSqliteSidecars: false);

            Debug.WriteLine("[CardDatabasePrep] Deleted corrupt or partial database artifacts.");
        }
        private void CleanupUpdateAcquisitionArtifacts()
        {
            DeleteArtifactFamily(_tempDbPath, includeSqliteSidecars: true);
            DeleteArtifactFamily(_pricesPath, includeSqliteSidecars: false);
        }
        private void CleanupPriceAcquisitionArtifacts()
        {
            DeleteArtifactFamily(_pricesPath, includeSqliteSidecars: false);
        }
        private static void DeleteArtifactFamily(string destinationPath, bool includeSqliteSidecars)
        {
            foreach (var path in GetArtifactFamilyPaths(destinationPath, includeSqliteSidecars))
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
        private static void TryCleanupArtifactFamily(string destinationPath, bool includeSqliteSidecars)
        {
            foreach (var path in GetArtifactFamilyPaths(destinationPath, includeSqliteSidecars))
            {
                TryDeleteTransientFile(path);
            }
        }
        private static IEnumerable<string> GetArtifactFamilyPaths(string destinationPath, bool includeSqliteSidecars)
        {
            // Materialized artifact
            yield return destinationPath;

            // Compressed download
            yield return destinationPath + ".download.gz";

            // RemoteFileDownloader staging file
            yield return destinationPath + ".download.gz.part";

            // GzipFileDecompressor staging file
            yield return destinationPath + ".decompressing";

            if (includeSqliteSidecars)
            {
                yield return destinationPath + "-shm";
                yield return destinationPath + "-wal";
            }
        }
        private static void TryCleanupAcquiredArtifacts(IEnumerable<GzipArtifactRequest> artifacts)
        {
            foreach (var artifact in artifacts)
            {
                TryCleanupArtifactFamily(artifact.DestinationPath, artifact.IsSqlite);
            }
        }
        private static void TryDeleteTransientFile(string filePath)
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
                // Cleanup failure should not hide the actual operation result.
                Debug.WriteLine($"[Cleanup] Unable to delete '{filePath}': {ex.Message}");
            }
        }

        #endregion

        #region INTERNAL MODELS        
        private sealed record GzipArtifactRequest(string SourceUrl, string DestinationPath, string Label, bool ReportProgress, bool IsSqlite);
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

        #endregion
    }
}
