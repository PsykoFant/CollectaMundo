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
                _statusVM.Show("No internet connection!", false);
                _statusVM.FirstTimeSetupText = "Unfortunately, first time setup cannot continue without internet connection";
                await Task.Delay(10000);
                Application.Current.Shutdown();
            }

            string dbPath = Path.Combine(_settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");
            string pricesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "prices.json");

            const int maxTotalAttempts = 3;

            for (int overallAttempt = 1; overallAttempt <= maxTotalAttempts; overallAttempt++)
            {
                _statusVM.FirstTimeSetupText = "Performing first-time setup of card database - please wait ...";
                Debug.WriteLine($"[SetupPipeline] Starting overall attempt {overallAttempt} of {maxTotalAttempts}.");

                try
                {
                    var cardDbTask = ExecuteWithRetryAsync(() => DownloadResourceAsync(cardDbUrl, dbPath, onStart: size => _statusVM.Show($"Downloading Card Database ({size})", true), onProgress: percent => _statusVM.ProgressValue = percent), "1a - card db download");
                    var downloadPricesTask = ExecuteWithRetryAsync(() => DownloadResourceAsync(pricesUrl, pricesPath, onStart: null, onProgress: null), "1b - price file download");

                    bool downloadsSucceeded;
                    try
                    {
                        bool[] results = await Task.WhenAll(cardDbTask, downloadPricesTask);
                        downloadsSucceeded = results.All(r => r);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[SetupPipeline] One or both download tasks threw: {ex.Message}");
                        downloadsSucceeded = false;
                    }

                    if (!downloadsSucceeded)
                    {
                        Debug.WriteLine($"[SetupPipeline] One or both downloads failed. Restarting setup.");

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

                        continue;
                    }

                    Debug.WriteLine("[SetupPipeline] Both downloads succeeded.");
                    // Proceed to next steps — tables, PNGs, prices etc...
                    return; // exit after full successful run
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SetupPipeline] Attempt {overallAttempt} failed with exception: {ex.Message}");
                }
            }

            _statusVM.Show("Setup failed after multiple attempts. Please restart the application or check your internet connection.", false);
            _statusVM.FirstTimeSetupText = "CollectaMundo will automatically close in a bit...";

            await Task.Delay(10000);
            Application.Current.Shutdown();


        }



        public async Task FirstTimeDbSetup()
        {
            /*
            _statusVM.FirstTimeSetupText = "Performing first-time setup of card database - please wait ...";

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
                _statusVM.StatusMessage = "Generating mana symbols...";
                await _missingPngService.GenerateMissingManaSymbolImagesAsync(uow.CurrentConnection);

                _statusVM.StatusMessage = "Generating mana cost images...";
                await _missingPngService.GenerateMissingManaCostImagesAsync(uow.CurrentConnection);

                _statusVM.StatusMessage = "Generating set icon images...";
                await _missingPngService.GenerateMissingKeyRuneImagesAsync(uow.CurrentConnection);

                // 3. Import card prices
                _statusVM.StatusMessage = "Processing card prices...";
                await _priceService.ImportPricesFromJsonAsync(pricesPath, uow.CurrentConnection);

                _statusVM.StatusMessage = "Almost there - wrapping things up...";

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

                _statusVM.FirstTimeSetupText = "First time setup of card database completed successfully!";
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
        private static async Task<bool> DownloadResourceAsync(string url, string targetPath, Action<string>? onStart = null, Action<int>? onProgress = null)
        {
            try
            {
                using var httpClient = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var totalBytesRead = 0L;
                var buffer = new byte[8192];
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                if (onStart != null && totalBytes > 0)
                {
                    var megabytes = string.Format("{0:0.0} MB", totalBytes / 1_000_000.0);
                    onStart.Invoke(megabytes);
                }

                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer)) != 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[DownloadResourceAsync] Download error: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ExecuteWithRetryAsync(Func<Task<bool>> action, string stepName, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (await action())
                    {
                        Debug.WriteLine($"[SetupPipeline] Step '{stepName}' succeeded.");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SetupPipeline] Step '{stepName}' threw on attempt {attempt}: {ex.Message}");
                }

                Debug.WriteLine($"[SetupPipeline] Step '{stepName}' failed on attempt {attempt}.");
                _statusVM.Show($"Retrying '{stepName}' ({attempt}/{maxRetries})...", true);
                await Task.Delay(2000);
            }

            _statusVM.Show($"Failed to complete '{stepName}' after {maxRetries} tries. Restarting setup.", true);
            return false;
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
