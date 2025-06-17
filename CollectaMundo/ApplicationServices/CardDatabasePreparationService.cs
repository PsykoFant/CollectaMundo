using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.Data;
using CollectaMundo.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

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

        public CardDatabasePreparationService(IAppSettings settings, IDatabaseSchemaRepository dbSchemaRepo, ICardPriceService priceService, IGenerateMissingPngService missingPngService, StatusViewModel statusVM)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _dbSchemaRepo = dbSchemaRepo ?? throw new ArgumentNullException(nameof(dbSchemaRepo));
            _priceService = priceService ?? throw new ArgumentNullException(nameof(priceService));
            _missingPngService = missingPngService ?? throw new ArgumentNullException(nameof(missingPngService));
            _statusVM = statusVM ?? throw new ArgumentNullException(nameof(statusVM));

            _dbFactory = new DbConnectionFactory(_settings);
        }

        public async Task FirstTimeDbSetup()
        {
            _statusVM.FirstTimeSetupText = "Performing first-time setup of card database - please wait ...";

            string cardDbUrl = "https://mtgjson.com/api/v5/AllPrintings.sqlite";
            string pricesUrl = "https://mtgjson.com/api/v5/AllPricesToday.json";

            string dbPath = Path.Combine(_settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");
            string pricesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "prices.json");

            var downloadCardDbTask = DownloadResourceAsync(cardDbUrl, dbPath, onStart: size => _statusVM.Show($"Downloading Card Database ({size})", true), onProgress: percent => _statusVM.ProgressValue = percent);
            var downloadPricesTask = DownloadResourceAsync(pricesUrl, pricesPath, onStart: null, onProgress: null);

            bool[] results = await Task.WhenAll(downloadCardDbTask, downloadPricesTask);

            // Retry if needed
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
                Debug.WriteLine($"Download error: {ex.Message}");
                return false;
            }
        }

    }

}
