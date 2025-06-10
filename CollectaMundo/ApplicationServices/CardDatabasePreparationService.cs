using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.Data;
using CollectaMundo.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace CollectaMundo.ApplicationServices
{
    public class CardDatabasePreparationService(IAppSettings settings, IDbConnectionFactory dbFactory, IDatabaseSchemaInitializer schemaInitializer, ICardPriceImporter priceImporter, IGenerateMissingPngService missingPngService, StatusViewModel statusVM) : ICardDatabasePreparationService
    {
        private readonly IAppSettings _settings = settings;
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly IDatabaseSchemaInitializer _schemaInitializer = schemaInitializer;
        private readonly ICardPriceImporter _priceImporter = priceImporter ?? throw new ArgumentNullException(nameof(priceImporter));
        private readonly IGenerateMissingPngService _missingPngService = missingPngService;
        private readonly StatusViewModel _statusVM = statusVM ?? throw new ArgumentNullException(nameof(statusVM));
        public async Task FirstTimeDbSetup()
        {
            string cardDbUrl = "https://mtgjson.com/api/v5/AllPrintings.sqlite";
            string pricesUrl = "https://mtgjson.com/api/v5/AllPricesToday.json";

            string dbPath = Path.Combine(_settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");
            string pricesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "prices.json");

            //var downloadCardDbTask = DownloadResourceAsync(cardDbUrl, dbPath, "Card Database", true, _statusVM);
            //var downloadPricesTask = DownloadResourceAsync(pricesUrl, pricesPath, "Card Prices", false, _statusVM);

            //bool[] results = await Task.WhenAll(downloadCardDbTask, downloadPricesTask);

            //// Retry if needed
            //if (!results[0])
            //{
            //    _statusVM.Show("Retrying card database download...", true);
            //    bool retryCardDb = await DownloadResourceAsync(cardDbUrl, dbPath, "Card Database", true, _statusVM);
            //    if (!retryCardDb)
            //    {
            //        Debug.WriteLine("Card database re-download failed.");
            //        return;
            //    }
            //}

            //if (!results[1])
            //{
            //    _statusVM.Show("Retrying card prices download...", false);
            //    bool retryPrices = await DownloadResourceAsync(pricesUrl, pricesPath, "Card Prices", false, _statusVM);
            //    if (!retryPrices)
            //    {
            //        Debug.WriteLine("Prices re-download failed.");
            //        return;
            //    }
            //}

            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {
                // 1. Create tables
                await _schemaInitializer.CreateTablesAsync(uow.CurrentConnection);

                // 2. Generate missing PNGs for icons                
                await _missingPngService.GenerateMissingManaSymbolImagesAsync(uow.CurrentConnection, _statusVM);
                await _missingPngService.GenerateMissingManaCostImagesAsync(uow.CurrentConnection, _statusVM);

                statusVM.StatusMessage = "Generating keyrune images...";
                await _missingPngService.GenerateMissingKeyRuneImagesAsync(uow.CurrentConnection, _statusVM);

                // 3. Import card prices
                await _priceImporter.ImportPricesFromJsonAsync(pricesPath, uow.CurrentConnection);
                statusVM.StatusMessage = "Importing card prices...";

                statusVM.StatusMessage = "Wrapping up first-time setup...";
                // 4. Create views
                await _schemaInitializer.CreateViewsAsync(uow.CurrentConnection, "cardmarket");

                // 5. Create indices
                await _schemaInitializer.CreateIndicesAsync(uow.CurrentConnection);

                await uow.CommitAsync();

                // 6. Optimize database
                await _schemaInitializer.OptimizeAsync(uow.CurrentConnection);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FirstTimeDbSetup] Error: {ex.Message}");
                await uow.RollbackAsync();
                throw;
            }
            finally
            {
                try
                {
                    //File.Delete(pricesPath);
                    //Debug.WriteLine($"Temp price file deleted");
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
        private static async Task<bool> DownloadResourceAsync(string url, string targetPath, string description, bool showProgress, StatusViewModel statusVm)
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

                var megabytes = string.Format("{0:0.0} MB", totalBytes / 1_000_000.0);
                statusVm.Show($"Downloading {description} ({megabytes})", showProgress);

                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer)) != 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalBytesRead += bytesRead;

                    if (showProgress && totalBytes > 0)
                    {
                        double percent = (double)totalBytesRead / totalBytes * 100;
                        statusVm.ProgressValue = (int)percent;
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
