using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.DownloadResourceFiles;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.ApplicationServices.Utilities.InternetCheck;
using CollectaMundo.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;

namespace CollectaMundo.ApplicationServices
{
    public sealed record SetupProgress(
    string? Headline = null,
    string? Detail = null,
    string? Step = null,
    int? Percent = null,
    bool? IsProgressVisible = null);
    public interface IProgressContext
    {
        IProgress<string> Headline { get; }
        IProgress<string> Detail { get; }
        IProgress<string> Step { get; }
        IProgress<int> Percent { get; }
        IProgress<bool> ProgressBarVisible { get; }
    }
    public sealed class ProgressContext(IProgress<SetupProgress> p) : IProgressContext
    {
        public static readonly IProgressContext NoOp = new ProgressContext(new Progress<SetupProgress>(_ => { }));
        public IProgress<string> Headline { get; } = new Progress<string>(s => p.Report(new SetupProgress(Headline: s)));
        public IProgress<string> Detail { get; } = new Progress<string>(s => p.Report(new SetupProgress(Detail: s)));
        public IProgress<string> Step { get; } = new Progress<string>(s => p.Report(new SetupProgress(Step: s)));
        public IProgress<int> Percent { get; } = new Progress<int>(v => p.Report(new SetupProgress(Percent: v)));
        public IProgress<bool> ProgressBarVisible { get; } = new Progress<bool>(v => p.Report(new SetupProgress(IsProgressVisible: v)));
    }
    public class CardDatabasePreparationService(IAppSettings settings, IDbConnectionFactory dbFactory, IDatabaseSchemaRepository dbSchemaRepo, ICardPriceService priceService, IGenerateMissingPngService missingPngService, IDownloadService downloadService, IInternetConnectivityService internetConnectivityService) : ICardDatabasePreparationService
    {
        private readonly IAppSettings _settings = settings;
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly IDatabaseSchemaRepository _dbSchemaRepo = dbSchemaRepo;
        private readonly ICardPriceService _priceService = priceService;
        private readonly IGenerateMissingPngService _missingPngService = missingPngService;
        private readonly IDownloadService _downloadService = downloadService;
        private readonly IInternetConnectivityService _internetConnectivityService = internetConnectivityService;

        // Paths (precomputed)
        private readonly string _dbPath = Path.Combine(settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");
        private readonly string _pricesPath = Path.Combine(settings.UserDownloadsPath, "prices.json");

        private IProgressContext _progress = ProgressContext.NoOp;
        public void SetProgress(IProgress<SetupProgress>? progress) => _progress = progress is null ? ProgressContext.NoOp : new ProgressContext(progress);


        // Use case: orchestrates the first-time database preparation steps
        public async Task<OperationResult> FirstTimeDbPrepOrchetrator(int defaultDelay = 3000)
        {
            // 1) Internet precheck
            if (!await _internetConnectivityService.IsInternetAvailableAsync())
            {
                return new OperationResult(OperationResultCode.NoInternet, "Internet not available");
            }

            _progress.Headline.Report("Performing first-time setup of card database - please wait ...");
            _progress.ProgressBarVisible.Report(true);

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
                    stepNameAndNumberProgress: _progress.Step,
                    stepDetailAndErrorProgress: _progress.Detail,
                    percentProgress: _progress.Percent);

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
                ($"Step {stepNumberStart++}. Generating mana symbols...",() => ExecuteWithUnitOfWorkAsync(conn => _missingPngService.GenerateMissingManaSymbolImagesAsync(conn, _progress.Percent)),true),
                ($"Step {stepNumberStart++}. Generating mana cost images...",() => ExecuteWithUnitOfWorkAsync(conn => _missingPngService.GenerateMissingManaCostImagesAsync(conn, _progress.Percent)),true),
                ($"Step {stepNumberStart++}. Generating set icon images...",() => ExecuteWithUnitOfWorkAsync(conn => _missingPngService.GenerateMissingKeyRuneImagesAsync(conn, _progress.Percent)),true),
                ($"Step {stepNumberStart++}. Processing card prices...",() => ExecuteWithUnitOfWorkAsync(conn => _priceService.ImportPricesFromJsonAsync(_pricesPath, conn, _progress.Detail, _progress.Percent)),true),
                ($"Step {stepNumberStart++}. Creating views...",() => Task.Run(() => ExecuteWithUnitOfWorkAsync(conn => _dbSchemaRepo.CreateViewsAsync(conn, "cardmarket"))),false),
                ($"Step {stepNumberStart++}. Creating indices...",() => Task.Run(() => ExecuteWithUnitOfWorkAsync(conn => _dbSchemaRepo.CreateIndicesAsync(conn))),false),
                ($"Step {stepNumberStart++}. Optimizing database...",() => Task.Run(() => ExecuteWithConnectionAsync(conn => _dbSchemaRepo.OptimizeAsync(conn))),false),
            };

            foreach (var (label, work, showProgress) in steps)
            {
                if (!showProgress)
                {
                    _progress.ProgressBarVisible.Report(false);
                }

                // Reset detail label for each step
                _progress.Detail.Report(string.Empty);

                var result = await RetryHelper.RetryLoopAsync(
                    async () =>
                    {
                        await work();
                        return new OperationResult(OperationResultCode.Success, $"{label} completed.");
                    },
                    retryDelayInMs: defaultDelay,
                    maxRetries: 3,
                    stepName: label,
                    stepNameAndNumberProgress: _progress.Step,
                    stepDetailAndErrorProgress: _progress.Detail);

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
    }
}
