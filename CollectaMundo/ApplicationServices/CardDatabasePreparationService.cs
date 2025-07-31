using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.DownloadResourceFiles;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.Data;
using CollectaMundo.ViewModels;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace CollectaMundo.ApplicationServices
{
    public class CardDatabasePreparationService(IAppSettings settings, IDatabaseSchemaRepository dbSchemaRepo, ICardPriceService priceService, IGenerateMissingPngService missingPngService, IDownloadService downloadService, StatusViewModel statusVM) : ICardDatabasePreparationService
    {
        private static IDbConnectionFactory DbFactory => AppGlobals.DbFactory ?? throw new InvalidOperationException("AppContext.DbFactory is not initialized.");
        private readonly IAppSettings _settings = settings;
        private readonly IDatabaseSchemaRepository _dbSchemaRepo = dbSchemaRepo;
        private readonly ICardPriceService _priceService = priceService;
        private readonly IGenerateMissingPngService _missingPngService = missingPngService;
        private readonly IDownloadService _downloadService = downloadService;
        private readonly StatusViewModel _statusVM = statusVM;
        public async Task FirstTimeDbPrepOrchetrator()
        {
            string dbPath = Path.Combine(_settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");
            string pricesPath = Path.Combine(_settings.UserDownloadsPath, "prices.json");

            const int maxTotalAttempts = 3;
            bool downloadsSucceeded = false;

            if (!await IsInternetAvailableAsync())
            {
                await DbSetupFailed(
                    statusAboveBar: "No internet connection!",
                    statusBelowBar: "Unfortunately, first time setup cannot continue without internet connection",
                    statusLabelMain: "Please check your connection. CollectaMundo will close down shortly...");
                return;
            }

            _statusVM.StatusLabel1 = "Performing first-time setup of card database - please wait ...";
            _statusVM.ProgressVisibility = Visibility.Visible;

            IProgress<string> stepDetailProgress = new Progress<string>(msg => _statusVM.StatusLabel2 = msg);
            IProgress<string> stepLabelProgress = new Progress<string>(msg => _statusVM.StatusLabel3 = msg);
            IProgress<int> percentProgress = new Progress<int>(p => _statusVM.ProgressValue = p);

            for (int overallAttempt = 1; overallAttempt <= maxTotalAttempts; overallAttempt++)
            {
                Debug.WriteLine($"[FirstTimeDbPrepOrchetrator] Overall attempt {overallAttempt} of {maxTotalAttempts}");

                if (overallAttempt > 1)
                {
                    _statusVM.StatusLabel1 = $"Setup failed, retrying attempt {overallAttempt}...";
                }

                try { CleanupPartialDatabaseFiles(dbPath, _settings.UserDownloadsPath); }
                catch (Exception ex) { Debug.WriteLine($"[Cleanup] {ex.Message}"); }

                try
                {
                    //Step 1: Downloads
                    (downloadsSucceeded, errorMessage) = await _downloadService.DownloadParallelAsync(
    _settings.CardDatabaseUrl, dbPath, "Card database",
    _settings.CardPricesUrl, pricesPath, "Price File",
    stepLabelProgress, percentProgress
);

                    if (!downloadsSucceeded)
                    {
                        continue;
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

                        // Reset status detail between steps
                        _statusVM.StatusLabel2 = string.Empty;

                        bool success = await RetryHelper.RetryLoopAsync(stepWork: work, maxRetries: 3, stepNameProgress: stepLabelProgress, detailProgress: stepDetailProgress, stepName: label);

                        if (!success)
                        {
                            throw new Exception($"Step '{label}' failed after retries.");
                        }
                    }


                    // If setup fully succeeded
                    if (downloadsSucceeded)
                    {
                        try { File.Delete(pricesPath); }
                        catch (IOException ex) { Debug.WriteLine($"Couldn't delete prices.json: {ex.Message}"); }
                    }

                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FirstTimeDbPrepOrchetrator] Overall attempt {overallAttempt} failed: {ex.Message}");
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
                statusLabelMain: "CollectaMundo will close down shortly...");
        }

        // Retry logic for downloading files and executing database actions
        private static async Task<bool> ExecuteDualDownloadAsync(
            Func<CancellationToken, Task<(bool success, string? error)>> downloadA,
            Func<CancellationToken, Task<(bool success, string? error)>> downloadB)
        {
            using var innerCts = new CancellationTokenSource();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(innerCts.Token);
            var token = linkedCts.Token;

            var taskA = downloadA(token);
            var taskB = downloadB(token);

            var firstCompleted = await Task.WhenAny(taskA, taskB);
            var firstResult = await firstCompleted;

            if (!firstResult.success)
            {
                innerCts.Cancel();
                await Task.WhenAll(taskA, taskB);

                if (!string.IsNullOrWhiteSpace(firstResult.error))
                {
                    throw new Exception(firstResult.error);
                }

                return false;
            }

            var finalA = await taskA;
            var finalB = await taskB;

            if (!finalA.success || !finalB.success)
            {
                var error = finalA.error ?? finalB.error ?? "Unknown download error.";
                throw new Exception(error);
            }

            return true;
        }
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
        private static async Task<bool> IsInternetAvailableAsync()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var result = await client.GetAsync("https://www.google.com");
                return result.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
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
        private async Task DbSetupFailed(string statusAboveBar, string statusBelowBar, string statusLabelMain)
        {
            //  If we reach here, all attempts have failed
            _statusVM.ProgressVisibility = Visibility.Collapsed;
            _statusVM.LogoVisibility = Visibility.Collapsed;
            _statusVM.SetupFailVisibility = Visibility.Visible;
            _statusVM.StatusLabel1 = statusAboveBar;
            _statusVM.StatusLabel2 = statusBelowBar;
            _statusVM.StatusLabel3 = statusLabelMain;

            await Task.Delay(10000);
            Application.Current.Shutdown();
        }
    }
}
