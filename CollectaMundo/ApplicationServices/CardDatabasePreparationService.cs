using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.Data;
using CollectaMundo.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace CollectaMundo.ApplicationServices
{
    public class CardDatabasePreparationService : ICardDatabasePreparationService
    {
        private readonly IAppSettings _settings;
        private readonly IDbConnectionFactory _dbFactory;
        private readonly IDatabaseSchemaRepository _dbSchemaRepo;
        private readonly ICardPriceService _priceService;
        private readonly IGenerateMissingPngService _missingPngService;
        private readonly StatusViewModel _statusVM;

        private readonly string cardDbUrl = "https://mtgjson.com/api/v5/AllPrintings.sqlite";
        private readonly string pricesUrl = "https://mtgjson.com/api/v5/AllPricesToday.json";

        public CardDatabasePreparationService(IAppSettings settings, IDatabaseSchemaRepository dbSchemaRepo, ICardPriceService priceService, IGenerateMissingPngService missingPngService, StatusViewModel statusVM)
        {
            _settings = settings;
            _dbSchemaRepo = dbSchemaRepo;
            _priceService = priceService;
            _missingPngService = missingPngService;
            _statusVM = statusVM;

            _dbFactory = new DbConnectionFactory(_settings);
        }
        public async Task RunCompleteSetupWithRetriesAsync()
        {
            if (!IsInternetAvailable())
            {
                _statusVM.StatusLabelAboveBar = "No internet connection!";
                _statusVM.StatusLabelBelowBar = "Unfortunately, first time setup cannot continue without internet connection";
                _statusVM.StatusLabelMain = "Please check your connection. CollectaMundo will close down shortly...";
                await Task.Delay(10000);
                Application.Current.Shutdown();
            }

            string dbPath = Path.Combine(_settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");
            string pricesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "prices.json");

            const int maxTotalAttempts = 3;

            for (int overallAttempt = 1; overallAttempt <= maxTotalAttempts; overallAttempt++)
            {
                // Overall attempt - Reset Action
                _statusVM.ProgressValue = 0;
                if (File.Exists(dbPath))
                {
                    try
                    {
                        File.Delete(dbPath);
                        Debug.WriteLine("[SetupPipeline] Deleted corrupt or partial DB file.");
                    }
                    catch (Exception cleanupEx)
                    {
                        Debug.WriteLine($"[SetupPipeline] Failed to delete DB file: {cleanupEx.Message}");
                    }
                }
                _statusVM.StatusLabelAboveBar = string.Empty;
                _statusVM.StatusLabelBelowBar = string.Empty;
                _statusVM.StatusLabelMain = string.Empty;
                // end reset block

                _statusVM.StatusLabelAboveBar = "Performing first-time setup of card database - please wait ...";
                Debug.WriteLine($"[SetupPipeline] Starting first time db setup overall attempt {overallAttempt} of {maxTotalAttempts}.");

                try
                {
                    using var cts = new CancellationTokenSource();
                    var token = cts.Token;

                    var cardDbTcs = new TaskCompletionSource<bool>();
                    var priceFileTcs = new TaskCompletionSource<bool>();

                    var cardDbExecutionTask = Task.Run(async () =>
                    {
                        bool result = await ExecuteWithRetryAsync(() => DownloadResourceAsync(cardDbUrl, dbPath, onStart: size => _statusVM.Show($"Downloading Card Database ({size})", true), onProgress: percent => _statusVM.ProgressValue = percent, token), "1a - card database download", token);
                        cardDbTcs.TrySetResult(result);
                        if (!result) cts.Cancel();
                    });

                    var priceFileExecutionTask = Task.Run(async () =>
                    {
                        bool result = await ExecuteWithRetryAsync(() => DownloadResourceAsync(pricesUrl, pricesPath, onStart: null, onProgress: null, token), "1b - price file download", token);
                        priceFileTcs.TrySetResult(result);
                        if (!result) cts.Cancel();
                    });

                    await Task.WhenAll(cardDbTcs.Task, priceFileTcs.Task);

                    bool cardDbDone = cardDbTcs.Task.IsCompletedSuccessfully;
                    bool priceFileDone = priceFileTcs.Task.IsCompletedSuccessfully;

                    bool cardDbSuccess = cardDbDone && cardDbTcs.Task.Result;
                    bool priceFileSuccess = priceFileDone && priceFileTcs.Task.Result;

                    if (!cardDbSuccess || !priceFileSuccess)
                    {
                        Debug.WriteLine("[SetupPipeline] One or both downloads failed. Restarting immediately.");

                        cts.Cancel(); // stop the other
                        await Task.WhenAll(cardDbExecutionTask, priceFileExecutionTask); // wait for all cleanup
                        _statusVM.ProgressValue = 0;
                        continue;
                    }

                    Debug.WriteLine("[SetupPipeline] Both downloads succeeded.");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SetupPipeline] Attempt {overallAttempt} failed with exception: {ex.Message}");
                }
            }

            _statusVM.IsProgressVisible = false;
            _statusVM.StatusLabelAboveBar = "Setup failed after multiple attempts. Please restart the application or check your internet connection.";
            _statusVM.StatusLabelMain = "CollectaMundo will close down shortly...";

            await Task.Delay(10000);
            Application.Current.Shutdown();
        }

        public async Task FirstTimeDbSetup()
        {
            /*
            _statusVM.StatusLabelAboveBar = "Performing first-time setup of card database - please wait ...";

            string cardDbUrl = "https://mtgjson.com/api/v5/AllPrintings.sqlite";
            string pricesUrl = "https://mtgjson.com/api/v5/AllPricesToday.json";

            string dbPath = Path.Combine(_settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");
            string pricesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "prices.json");

            var downloadCardDbTask = DownloadResourceAsync(cardDbUrl, dbPath, onStart: size => _statusVM.Show($"Downloading Card Database ({size})", true), onProgress: percent => _statusVM.ProgressValue = percent);
            var downloadPricesTask = DownloadResourceAsync(pricesUrl, pricesPath, onStart: null, onProgress: null);

            bool[] results = await Task.WhenAll(downloadCardDbTask, downloadPricesTask);

            Retry if needed
            if (!results[0])
                {
                    _statusVM.Show("Retrying card database download...", true);
                    bool retryCardDb = await DownloadResourceAsync(
                    cardDbUrl,
                    dbPath,
                    onStart: size => _statusVM.Show($"Downloading Card Database ({size})", true),
                    onProgress: percent => _statusVM.ProgressValue = percent);
                    if (!retryCardDb)
                    {
                        Debug.WriteLine("Card database re-download failed.");
                        return;
                    }
                }

            if (!results[1])
            {
                _statusVM.Show("Retrying card prices download...", true);
                bool retryPrices = await DownloadResourceAsync(
                pricesUrl,
                pricesPath,
                onStart: size => _statusVM.Show($"Downloading price file ({size})", true),
                onProgress: percent => _statusVM.ProgressValue = percent);
                if (!retryPrices)
                {
                    Debug.WriteLine("Prices re-download failed.");
                    return;
                }
            }
            */

            await RunCompleteSetupWithRetriesAsync();


            string pricesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "prices.json");
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {
                // 1. Create tables
                await _dbSchemaRepo.CreateTablesAsync(uow.CurrentConnection);

                // 2. Generate missing PNGs for icons
                _statusVM.StatusLabelMain = "Generating mana symbols...";
                await _missingPngService.GenerateMissingManaSymbolImagesAsync(uow.CurrentConnection);

                _statusVM.StatusLabelMain = "Generating mana cost images...";
                await _missingPngService.GenerateMissingManaCostImagesAsync(uow.CurrentConnection);

                _statusVM.StatusLabelMain = "Generating set icon images...";
                await _missingPngService.GenerateMissingKeyRuneImagesAsync(uow.CurrentConnection);

                // 3. Import card prices
                _statusVM.StatusLabelMain = "Processing card prices...";
                await _priceService.ImportPricesFromJsonAsync(pricesPath, uow.CurrentConnection);

                _statusVM.StatusLabelMain = "Almost there - wrapping things up...";

                // Perform heavy work in the background
                await Task.Run(async () =>
                {
                    // 4. Create views
                    await _dbSchemaRepo.CreateViewsAsync(uow.CurrentConnection, "cardmarket");

                    // 5. Create indices
                    await _dbSchemaRepo.CreateIndicesAsync(uow.CurrentConnection);

                    // 6. Commit the unit of work
                    await uow.CommitAsync();

                    // 7. Optimize database
                    await _dbSchemaRepo.OptimizeAsync(uow.CurrentConnection);

                });

                _statusVM.StatusLabelAboveBar = "First time setup of card database completed successfully!";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FirstTimeDbSetup] Error: {ex.Message}");
                await uow.RollbackAsync();
                throw;
            }
            finally
            {
                await uow.DisposeAsync();

                try
                {
                    File.Delete(pricesPath);
                }
                catch (IOException ex)
                {
                    Debug.WriteLine($"Failed to delete temp prices file: {ex.Message}");
                }
            }
        }
        public Task UpdateDb()
        {
            return Task.Run(() =>
            {
            });
        }
        public Task UpdateCardPrices()
        {
            return Task.Run(() =>
            {
            });
        }

        private async Task<bool> ExecuteWithRetryAsync(Func<Task<bool>> action, string stepName, CancellationToken token, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                if (token.IsCancellationRequested)
                {
                    Debug.WriteLine($"[SetupPipeline] Step '{stepName}' cancelled before attempt {attempt}.");
                    return false;
                }

                try
                {
                    Debug.WriteLine($"[SetupPipeline] Step '{stepName}' attempt number {attempt}...");
                    if (await action())
                    {
                        Debug.WriteLine($"[SetupPipeline] Step '{stepName}' succeeded!");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _statusVM.StatusLabelBelowBar = $"Step '{stepName}' threw error on attempt {attempt}: {ex.Message}";
                    Debug.WriteLine($"[SetupPipeline] Step '{stepName}' threw error on attempt {attempt}: {ex.Message}");
                }

                await Task.Delay(3000, token).ContinueWith(_ => { });  // Safe delay with cancellation
                _statusVM.StatusLabelBelowBar = string.Empty;
            }

            _statusVM.StatusLabelBelowBar = $"Failed to complete '{stepName}' after {maxRetries} tries. Restarting setup.";
            await Task.Delay(3000);
            _statusVM.StatusLabelBelowBar = string.Empty;
            return false;
        }
        private static async Task<bool> DownloadResourceAsync(string url, string targetPath, Action<string>? onStart = null, Action<int>? onProgress = null, CancellationToken token = default)
        {
            using var httpClient = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var totalBytesRead = 0L;
            var buffer = new byte[8192];
            using var contentStream = await response.Content.ReadAsStreamAsync(token);
            using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            if (onStart != null && totalBytes > 0)
            {
                var megabytes = string.Format("{0:0.0} MB", totalBytes / 1_000_000.0);
                onStart.Invoke(megabytes);
            }

            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) != 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                totalBytesRead += bytesRead;

                if (onProgress != null && totalBytes > 0)
                {
                    double percent = (double)totalBytesRead / totalBytes * 100;
                    onProgress.Invoke((int)percent);
                }
            }

            Debug.WriteLine($"Download complete: {targetPath}");
            return true;
        }
        private static bool IsInternetAvailable()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                using var result = client.GetAsync("https://www.google.com").Result;
                return result.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
