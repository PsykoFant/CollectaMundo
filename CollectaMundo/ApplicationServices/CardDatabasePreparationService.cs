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
        public async Task FirstTimeDbPrepOrchetrator(int defaultDelay = 3000)
        {
            string dbPath = Path.Combine(_settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");
            string pricesPath = Path.Combine(_settings.UserDownloadsPath, "prices.json");

            const int maxTotalAttempts = 3;

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

            IProgress<string> stepDetailProgress = new Progress<string>(msg => _statusVM.StatusLabel2 = msg);
            IProgress<string> stepLabelProgress = new Progress<string>(msg => _statusVM.StatusLabel3 = msg);
            IProgress<int> percentProgress = new Progress<int>(p => _statusVM.ProgressValue = p);

            for (int outerloopAttempt = 1; outerloopAttempt <= maxTotalAttempts; outerloopAttempt++)
            {
                Debug.WriteLine($"[FirstTimeDbPrepOrchetrator] Overall outer loop attempt {outerloopAttempt} of {maxTotalAttempts}");

                if (outerloopAttempt > 1)
                {
                    _statusVM.StatusLabel1 = $"Setup failed, retrying attempt {outerloopAttempt}...";
                }

                try { CleanupPartialDatabaseFiles(dbPath, _settings.UserDownloadsPath); }
                catch (Exception ex) { Debug.WriteLine($"[Cleanup] {ex.Message}"); }

                try
                {
                    //Step 1: Downloads
                    var downloadResult = await _downloadService.DownloadParallelAsync(
                        _settings.CardDatabaseUrl, dbPath, "Card database",
                        _settings.CardPricesUrl, pricesPath, "Price File",
                        retryDelayInMs: defaultDelay,
                        stepDetailProgress, percentProgress, stepLabelProgress,
                        stepName: "Step 1. Downloading resource files...");

                    if (downloadResult.Code != OperationResultCode.Success)
                    {
                        Debug.WriteLine($"[FirstTimeDbPrepOrchetrator] Download failed: {downloadResult.Message}");
                        continue; // Restart outer attempt loop
                    }

                    // STEP 2–9: Setup pipeline
                    var setupSteps = new List<(string Label, Func<Task> Work, bool showProgressBar)>{
                        ("Step 2. Creating custom tables...", (Func<Task>)(() => Task.Run(() =>ExecuteWithUnitOfWorkAsync(conn => _dbSchemaRepo.CreateTablesAsync(conn)))), showProgressBar: false),
                        ("Step 3. Generating mana symbols...", (Func<Task>)(() => ExecuteWithUnitOfWorkAsync(conn => _missingPngService.GenerateMissingManaSymbolImagesAsync(conn,percentProgress))), showProgressBar: true),
                        ("Step 4. Generating mana cost images...", (Func<Task>)(() => ExecuteWithUnitOfWorkAsync(conn => _missingPngService.GenerateMissingManaCostImagesAsync(conn, percentProgress))), showProgressBar: true),
                        ("Step 5. Generating set icon images...", (Func<Task>)(() => ExecuteWithUnitOfWorkAsync(conn => _missingPngService.GenerateMissingKeyRuneImagesAsync(conn, percentProgress))), showProgressBar: true),
                        ("Step 6. Processing card prices...", (Func<Task>)(() => ExecuteWithUnitOfWorkAsync(conn => _priceService.ImportPricesFromJsonAsync(pricesPath, conn, stepDetailProgress, percentProgress))), showProgressBar: true),
                        ("Step 7. Creating views...",(Func<Task>)(() => Task.Run(() =>ExecuteWithUnitOfWorkAsync(conn => _dbSchemaRepo.CreateViewsAsync(conn, "cardmarket")))), showProgressBar: false),
                        ("Step 8. Creating indices...", (Func<Task>)(() => Task.Run(() =>ExecuteWithUnitOfWorkAsync(conn => _dbSchemaRepo.CreateIndicesAsync(conn)))), showProgressBar: false),
                        ("Step 9. Optimizing database...", (Func<Task>)(() => Task.Run(() =>ExecuteWithConnectionAsync(conn => _dbSchemaRepo.OptimizeAsync(conn)))), showProgressBar: false)
                    };

                    foreach (var (label, work, showProgressBar) in setupSteps)
                    {
                        _statusVM.ProgressVisibility = showProgressBar ? Visibility.Visible : Visibility.Collapsed;
                        _statusVM.StatusLabel2 = string.Empty;

                        var result = await RetryHelper.RetryLoopAsync(
                            async () =>
                            {
                                await work(); // this is still the actual unit-of-work wrapped step
                                return new OperationResult(OperationResultCode.Success, $"{label} completed.");
                            },
                            retryDelayInMs: defaultDelay,
                            maxRetries: 3,
                            stepName: label,
                            stepNameProgress: stepLabelProgress,
                            detailProgress: stepDetailProgress);

                        if (result.Code != OperationResultCode.Success)
                            throw new Exception($"Step '{label}' failed after retries. {result.Message}");
                    }

                    // If setup fully succeeded
                    try { File.Delete(pricesPath); }
                    catch (IOException ex) { Debug.WriteLine($"Couldn't delete prices.json: {ex.Message}"); }

                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FirstTimeDbPrepOrchetrator] Overall attempt {outerloopAttempt} failed: {ex.Message}");
                }
                finally
                {
                    _statusVM.ProgressValue = 0;
                    _statusVM.StatusLabel1 = string.Empty;
                    _statusVM.StatusLabel2 = string.Empty;
                    _statusVM.StatusLabel3 = string.Empty;
                }
            }

            await DbSetupFailed(
                statusAboveBar: "Setup failed after multiple attempts.",
                statusBelowBar: "Please check your internet or restart the application.",
                statusLabelMain: "CollectaMundo will close down shortly...",
                defaultDelay);
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
