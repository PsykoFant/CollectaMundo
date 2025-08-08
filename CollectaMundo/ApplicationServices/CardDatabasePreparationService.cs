using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.DownloadResourceFiles;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.ApplicationServices.Utilities.InternetCheck;
using CollectaMundo.Data;
using CollectaMundo.ViewModels;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace CollectaMundo.ApplicationServices
{
    public class CardDatabasePreparationService(IAppSettings settings, IDatabaseSchemaRepository dbSchemaRepo, ICardPriceService priceService, IGenerateMissingPngService missingPngService, IDownloadService downloadService, IInternetConnectivityService internetConnectivityService, StatusViewModel statusVM, Action? shutdownApp = null) : ICardDatabasePreparationService
    {
        private static IDbConnectionFactory DbFactory => AppGlobals.DbFactory ?? throw new InvalidOperationException("AppContext.DbFactory is not initialized.");
        private readonly IAppSettings _settings = settings;
        private readonly IDatabaseSchemaRepository _dbSchemaRepo = dbSchemaRepo;
        private readonly ICardPriceService _priceService = priceService;
        private readonly IGenerateMissingPngService _missingPngService = missingPngService;
        private readonly IDownloadService _downloadService = downloadService;
        private readonly IInternetConnectivityService _internetConnectivityService = internetConnectivityService;
        private readonly StatusViewModel _statusVM = statusVM;
        private readonly Action? _shutdownApp = shutdownApp ?? (() => Application.Current?.Shutdown()); // Optional action to shutdown the app after setup failure

        // Paths (precomputed)
        private readonly string _dbPath = Path.Combine(settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");
        private readonly string _pricesPath = Path.Combine(settings.UserDownloadsPath, "prices.json");

        // Progress reporters (precomputed)
        private readonly IProgress<string> _statusLabel2Progress = new Progress<string>(msg => statusVM.StatusLabel2 = msg); // details/errors
        private readonly IProgress<string> _statusLabel3Progress = new Progress<string>(msg => statusVM.StatusLabel3 = msg); // step + attempt
        private readonly IProgress<int> _percentProgress = new Progress<int>(p => statusVM.ProgressValue = p);

        // Use case: orchestrates the first-time database preparation steps
        public async Task FirstTimeDbPrepOrchetrator(int defaultDelay = 3000)
        {
            // 1) Internet precheck
            if (!await _internetConnectivityService.IsInternetAvailableAsync())
            {
                await DbSetupFailed(
                    statusAboveBar: "No internet connection!",
                    statusBelowBar: "Unfortunately, first time setup cannot continue without internet connection",
                    statusLabelMain: "Please check your connection. CollectaMundo will close down shortly...",
                    defaultDelay);
                return;
            }

            _statusVM.StatusLabel1 = "Performing first-time setup of card database - please wait ...";
            _statusVM.ProgressVisibility = Visibility.Visible;

            // Always start from a clean slate on a single run
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
                    stepNameAndNumberProgress: _statusLabel3Progress,
                    stepDetailAndErrorProgress: _statusLabel2Progress,
                    percentProgress: _percentProgress);

                if (downloadResult.Code != OperationResultCode.Success)
                {
                    Debug.WriteLine($"[FirstTimeDbPrepOrchetrator] Download failed: {downloadResult.Message}");
                    await DbSetupFailed(
                        statusAboveBar: "Could not download required files.",
                        statusBelowBar: "CollectaMundo will automatically shutdown shortly ...",
                        statusLabelMain: downloadResult.Message,
                        defaultDelay);
                    return;
                }

                // ---------------------------
                // Steps 2–9
                // ---------------------------
                await PrepareDatabase(defaultDelay);

                // Success: clean up transient price file
                try { File.Delete(_pricesPath); }
                catch (IOException ex) { Debug.WriteLine($"Couldn't delete prices.json: {ex.Message}"); }

                // Clear UI on success
                _statusVM.ProgressValue = 0;
                _statusVM.StatusLabel1 = string.Empty;
                _statusVM.StatusLabel2 = string.Empty;
                _statusVM.StatusLabel3 = string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FirstTimeDbPrepOrchetrator] Fatal error: {ex.Message}");
                await DbSetupFailed(
                    statusAboveBar: "Setup failed.",
                    statusBelowBar: "CollectaMundo will automatically shutdown shortly ...",
                    statusLabelMain: ex.Message,
                    defaultDelay);
            }
        }

        private async Task PrepareDatabase(int defaultDelay)
        {
            int stepNumber = 2;

            var steps = new List<(string Label, Func<Task> Work, bool ShowProgress)>
            {
                ($"Step {stepNumber++}. Creating custom tables...",() => Task.Run(() => ExecuteWithUnitOfWorkAsync(conn => _dbSchemaRepo.CreateTablesAsync(conn)) ),false),
                ($"Step {stepNumber++}. Generating mana symbols...",() => ExecuteWithUnitOfWorkAsync(conn => _missingPngService.GenerateMissingManaSymbolImagesAsync(conn, _percentProgress)),true),
                ($"Step {stepNumber++}. Generating mana cost images...",() => ExecuteWithUnitOfWorkAsync(conn => _missingPngService.GenerateMissingManaCostImagesAsync(conn, _percentProgress)),true),
                ($"Step {stepNumber++}. Generating set icon images...",() => ExecuteWithUnitOfWorkAsync(conn => _missingPngService.GenerateMissingKeyRuneImagesAsync(conn, _percentProgress)),true),
                ($"Step {stepNumber++}. Processing card prices...",() => ExecuteWithUnitOfWorkAsync(conn => _priceService.ImportPricesFromJsonAsync(_pricesPath, conn, _statusLabel2Progress, _percentProgress)),true),
                ($"Step {stepNumber++}. Creating views...",() => Task.Run(() => ExecuteWithUnitOfWorkAsync(conn => _dbSchemaRepo.CreateViewsAsync(conn, "cardmarket"))),false),
                ($"Step {stepNumber++}. Creating indices...",() => Task.Run(() => ExecuteWithUnitOfWorkAsync(conn => _dbSchemaRepo.CreateIndicesAsync(conn))),false),
                ($"Step {stepNumber++}. Optimizing database...",() => Task.Run(() => ExecuteWithConnectionAsync(conn => _dbSchemaRepo.OptimizeAsync(conn))),false),
            };

            foreach (var (label, work, showProgress) in steps)
            {
                _statusVM.ProgressVisibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
                _statusVM.StatusLabel2 = string.Empty;

                var result = await RetryHelper.RetryLoopAsync(
                    async () =>
                    {
                        await work();
                        return new OperationResult(OperationResultCode.Success, $"{label} completed.");
                    },
                    retryDelayInMs: defaultDelay,
                    maxRetries: 3,
                    stepName: label,
                    stepNameAndNumberProgress: _statusLabel3Progress,
                    stepDetailAndErrorProgress: _statusLabel2Progress);

                if (result.Code != OperationResultCode.Success)
                {
                    // Fail-fast: any step failing its own retries triggers DB setup failure
                    await DbSetupFailed(
                        statusAboveBar: "Setup failed.",
                        statusBelowBar: "A setup step failed repeatedly.",
                        statusLabelMain: result.Message,
                        defaultDelay);
                    throw new Exception($"Step '{label}' failed after retries. {result.Message}");
                }
            }
        }

        // Retry logic for executing database actions
        private static async Task ExecuteWithUnitOfWorkAsync(Func<SQLiteConnection, Task> action)
        {
            await using var uow = new UnitOfWork();
            await uow.BeginAsync();
            await action(uow.CurrentConnection);
            await uow.CommitAsync();
        }
        private static async Task ExecuteWithConnectionAsync(Func<SQLiteConnection, Task> action)
        {
            await using var conn = await DbFactory.OpenConnectionAsync();
            await action(conn);
        }
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
        private async Task DbSetupFailed(string statusAboveBar, string statusBelowBar, string statusLabelMain, int defaultDelay)
        {
            //  If we reach here, all attempts have failed
            _statusVM.ProgressVisibility = Visibility.Collapsed;
            _statusVM.LogoVisibility = Visibility.Collapsed;
            _statusVM.SetupFailVisibility = Visibility.Visible;
            _statusVM.StatusLabel1 = statusAboveBar;
            _statusVM.StatusLabel2 = statusBelowBar;
            _statusVM.StatusLabel3 = statusLabelMain;

            await Task.Delay(defaultDelay * 3);
            _shutdownApp?.Invoke();
        }
    }
}
