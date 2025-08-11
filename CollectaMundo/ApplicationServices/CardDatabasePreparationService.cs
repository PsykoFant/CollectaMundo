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
    public class CardDatabasePreparationService(IAppSettings settings, IDbConnectionFactory dbFactory, IDatabaseSchemaRepository dbSchemaRepo, ICardPriceService priceService, IGenerateMissingPngService missingPngService, IDownloadService downloadService, IInternetConnectivityService internetConnectivityService, StatusViewModel statusVM, Action? shutdownApp = null) : ICardDatabasePreparationService
    {
        private readonly IAppSettings _settings = settings;
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly IDatabaseSchemaRepository _dbSchemaRepo = dbSchemaRepo;
        private readonly ICardPriceService _priceService = priceService;
        private readonly IGenerateMissingPngService _missingPngService = missingPngService;
        private readonly IDownloadService _downloadService = downloadService;
        private readonly IInternetConnectivityService _internetConnectivityService = internetConnectivityService;
        private readonly StatusViewModel _statusVM = statusVM;

        // Paths (precomputed)
        private readonly string _dbPath = Path.Combine(settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");
        private readonly string _pricesPath = Path.Combine(settings.UserDownloadsPath, "prices.json");

        // Progress reporters (precomputed)
        private readonly IProgress<string> _overallStepHeadlineProgress = new Progress<string>(msg => statusVM.StatusLabel1 = msg); // details/errors
        private readonly IProgress<string> _detailsAndErrorsProgress = new Progress<string>(msg => statusVM.StatusLabel2 = msg); // details/errors
        private readonly IProgress<string> _stepNameAndAttemptProgress = new Progress<string>(msg => statusVM.StatusLabel3 = msg); // step + attempt
        private readonly IProgress<int> _percentProgress = new Progress<int>(p => statusVM.ProgressValue = p);

        // Use case: orchestrates the first-time database preparation steps
        public async Task<OperationResult> FirstTimeDbPrepOrchetrator(int defaultDelay = 3000)
        {
            // 1) Internet precheck
            if (!await _internetConnectivityService.IsInternetAvailableAsync())
            {
                return new OperationResult(OperationResultCode.Error, "Internet not available");
            }

            _overallStepHeadlineProgress.Report("Performing first-time setup of card database - please wait ...");
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
                    stepNameAndNumberProgress: _stepNameAndAttemptProgress,
                    stepDetailAndErrorProgress: _detailsAndErrorsProgress,
                    percentProgress: _percentProgress);

                if (downloadResult.Code != OperationResultCode.Success)
                {
                    Debug.WriteLine($"[FirstTimeDbPrepOrchetrator] Download failed: {downloadResult.Message}");
                    return new OperationResult(OperationResultCode.DownloadFailed, downloadResult.Message);
                }

                // ---------------------------
                // Steps 2–9
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

        // Use case: orchestrates card database update
        //public async Task<OperationResult> UpdateDbPrepOrchetrator(int defaultDelay = 3000)
        //{

        //    var internetAvailable = await _internetConnectivityService.IsInternetAvailableAsync();

        //    if (!internetAvailable)
        //    {
        //        return new OperationResult(OperationResultCode.Error, "Internet not available - unable download updated resource files...");
        //    }
        //    _statusVM.ProgressVisibility = Visibility.Visible;

        //    // ---------------------------
        //    // Step 1. Download resources
        //    // ---------------------------
        //    var downloadResult = await _downloadService.DownloadParallelAsync(
        //        _settings.CardDatabaseUrl, _dbPath, "Card database",
        //        _settings.CardPricesUrl, _pricesPath, "Price File",
        //        retryDelayInMs: 3000,
        //        stepName: "Step 1 / 4. Downloading resource files for update...",
        //        _stepNameAndAttemptProgress, _detailsAndErrorsProgress, _percentProgress);

        //    if (downloadResult.Code != OperationResultCode.Success)
        //    {
        //        return new OperationResult(OperationResultCode.Error, downloadResult.Message);
        //    }

        //    // ---------------------------
        //    // Step 2 - Copy tables from new DB
        //    // ---------------------------
        //    _stepNameAndAttemptProgress.Report("Step 2 / 4 - Copying new tables...");

        //    try
        //    {
        //        await using var conn = await _dbFactory.OpenConnectionAsync();

        //        await Task.Run(async () =>
        //        {
        //            await _dbSchemaRepo.AttachTempDbAsync(conn, _dbPath, _detailsAndErrorsProgress);
        //            await _dbSchemaRepo.DropTablesAsync(conn, _detailsAndErrorsProgress);
        //            await _dbSchemaRepo.CopyTablesAsync(conn, _detailsAndErrorsProgress);
        //            await _dbSchemaRepo.DetachTempDbAsync(conn, _detailsAndErrorsProgress);
        //        });

        //    }
        //    catch (Exception ex)
        //    {
        //        _detailsAndErrorsProgress.Report($"Table copy failed: {ex.Message}");
        //        return new OperationResult(OperationResultCode.Error, $"Table copy failed: {ex.Message}");
        //    }


        //    // prepare database
        //    await PrepareDatabase(defaultDelay);

        //    // finish

        //    try
        //    {

        //        // ---------------------------
        //        // Steps 2–9
        //        // ---------------------------
        //        await PrepareDatabase(defaultDelay);

        //        // Success: clean up transient price file
        //        try { File.Delete(_pricesPath); }
        //        catch (IOException ex) { Debug.WriteLine($"Couldn't delete prices.json: {ex.Message}"); }

        //        // Clear UI on success
        //        _statusVM.ProgressValue = 0;
        //        _statusVM.StatusLabel1 = string.Empty;
        //        _statusVM.StatusLabel2 = string.Empty;
        //        _statusVM.StatusLabel3 = string.Empty;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine($"[FirstTimeDbPrepOrchetrator] Fatal error: {ex.Message}");
        //        await DbSetupFailed(
        //            statusAboveBar: "Setup failed.",
        //            statusBelowBar: "CollectaMundo will automatically shutdown shortly ...",
        //            statusLabelMain: ex.Message,
        //            defaultDelay);
        //    }
        //}
        private async Task<OperationResult> PrepareDatabaseAsync(int defaultDelay, int stepNumberStart)
        {
            var steps = new List<(string Label, Func<Task> Work, bool ShowProgress)>
            {
                ($"Step {stepNumberStart++}. Creating custom tables...",() => Task.Run(() => ExecuteWithUnitOfWorkAsync(conn => _dbSchemaRepo.CreateTablesAsync(conn)) ),false),
                ($"Step {stepNumberStart++}. Generating mana symbols...",() => ExecuteWithUnitOfWorkAsync(conn => _missingPngService.GenerateMissingManaSymbolImagesAsync(conn, _percentProgress)),true),
                ($"Step {stepNumberStart++}. Generating mana cost images...",() => ExecuteWithUnitOfWorkAsync(conn => _missingPngService.GenerateMissingManaCostImagesAsync(conn, _percentProgress)),true),
                ($"Step {stepNumberStart++}. Generating set icon images...",() => ExecuteWithUnitOfWorkAsync(conn => _missingPngService.GenerateMissingKeyRuneImagesAsync(conn, _percentProgress)),true),
                ($"Step {stepNumberStart++}. Processing card prices...",() => ExecuteWithUnitOfWorkAsync(conn => _priceService.ImportPricesFromJsonAsync(_pricesPath, conn, _detailsAndErrorsProgress, _percentProgress)),true),
                ($"Step {stepNumberStart++}. Creating views...",() => Task.Run(() => ExecuteWithUnitOfWorkAsync(conn => _dbSchemaRepo.CreateViewsAsync(conn, "cardmarket"))),false),
                ($"Step {stepNumberStart++}. Creating indices...",() => Task.Run(() => ExecuteWithUnitOfWorkAsync(conn => _dbSchemaRepo.CreateIndicesAsync(conn))),false),
                ($"Step {stepNumberStart++}. Optimizing database...",() => Task.Run(() => ExecuteWithConnectionAsync(conn => _dbSchemaRepo.OptimizeAsync(conn))),false),
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
                    stepNameAndNumberProgress: _stepNameAndAttemptProgress,
                    stepDetailAndErrorProgress: _detailsAndErrorsProgress);

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
