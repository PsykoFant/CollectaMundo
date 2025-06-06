using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.Data;
using CollectaMundo.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace CollectaMundo.ApplicationServices
{
    public class CardDatabasePreparationService(IAppSettings settings, IDbConnectionFactory dbFactory, IDatabaseSchemaInitializer schemaInitializer, IGenerateMissingPngService missingPngService, StatusViewModel statusVM) : ICardDatabasePreparationService
    {
        private readonly IAppSettings _settings = settings;
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly IDatabaseSchemaInitializer _schemaInitializer = schemaInitializer;
        private readonly IGenerateMissingPngService _missingPngService = missingPngService;
        private readonly StatusViewModel _statusVM = statusVM ?? throw new ArgumentNullException(nameof(statusVM));
        public async Task FirstTimeDbSetup()
        {
            string url = "https://mtgjson.com/api/v5/AllPrintings.sqlite";
            string path = Path.Combine(_settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");

            bool success = await DownloadResourceAsync(url, path, "Card Database", true, _statusVM);
            if (!success)
                return;


            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            var stopwatch = new Stopwatch();

            try
            {
                string creatabletime = "";
                string generateMissingManasymbolstime = "";
                string generateManaCoststime = "";
                string generateKeyrunestime = "";


                stopwatch.Restart();
                await _schemaInitializer.CreateTablesAsync(uow.CurrentConnection);
                stopwatch.Stop();
                creatabletime = $"[Timing] CreateTablesAsync took {stopwatch.ElapsedMilliseconds} ms";

                stopwatch.Restart();
                await _missingPngService.GenerateMissingManaSymbolImagesAsync(uow.CurrentConnection, _statusVM);
                stopwatch.Stop();
                generateMissingManasymbolstime = $"[Timing] GenerateMissingManaSymbolImagesAsync took {stopwatch.ElapsedMilliseconds} ms";

                stopwatch.Restart();
                await _missingPngService.GenerateMissingManaCostImagesAsync(uow.CurrentConnection, _statusVM);
                stopwatch.Stop();
                generateManaCoststime = $"[Timing] GenerateMissingManaCostImagesAsync took {stopwatch.ElapsedMilliseconds} ms";

                stopwatch.Restart();
                await _missingPngService.GenerateMissingKeyRuneImagesAsync(uow.CurrentConnection, _statusVM);
                stopwatch.Stop();
                generateKeyrunestime = $"[Timing] GenerateMissingKeyRuneImagesAsync took {stopwatch.ElapsedMilliseconds} ms";

                Debug.WriteLine(creatabletime);
                Debug.WriteLine(generateMissingManasymbolstime);
                Debug.WriteLine(generateManaCoststime);
                Debug.WriteLine(generateKeyrunestime);

                await uow.CommitAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FirstTimeDbSetup] Error: {ex.Message}");
                await uow.RollbackAsync();
                throw;
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
