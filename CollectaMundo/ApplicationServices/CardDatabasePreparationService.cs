using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.Data;
using CollectaMundo.ViewModels;
using System.Data.SQLite;
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

            // Outer loop for overall attempts

            for (int overallAttempt = 1; overallAttempt <= maxTotalAttempts; overallAttempt++)
            {
                // Outer loop - reset Action
                _statusVM.ProgressValue = 0;
                //if (File.Exists(dbPath))
                //{
                //    try
                //    {
                //        File.Delete(dbPath);
                // also delete the other files that SQLite creates
                //AllPrintings.sqlite-shm
                //    AllPrintings.sqlite-wal
                //        Debug.WriteLine("[SetupPipeline] Deleted corrupt or partial DB file.");
                //    }
                //    catch (Exception cleanupEx)
                //    {
                //        Debug.WriteLine($"[SetupPipeline] Failed to delete DB file: {cleanupEx.Message}");
                //    }
                //}
                _statusVM.StatusLabelAboveBar = string.Empty;
                _statusVM.StatusLabelBelowBar = string.Empty;
                _statusVM.StatusLabelMain = string.Empty;

                _statusVM.StatusLabelAboveBar = "Performing first-time setup of card database - please wait ...";
                Debug.WriteLine($"[SetupPipeline] Starting first time db setup overall attempt {overallAttempt} of {maxTotalAttempts}.");

                try
                {
                    // Inner loop for download attempts
                    using var cts = new CancellationTokenSource();
                    var token = cts.Token;

                    //var cardDbTcs = new TaskCompletionSource<bool>();
                    //var priceFileTcs = new TaskCompletionSource<bool>();

                    //var cardDbExecutionTask = Task.Run(async () =>
                    //{
                    //    bool result = await ExecuteWithRetryAsync(() => DownloadResourceAsync(cardDbUrl, dbPath, onStart: size => _statusVM.Show($"Downloading Card Database ({size})", true), onProgress: percent => _statusVM.ProgressValue = percent, token), "1a - card database download", token);
                    //    cardDbTcs.TrySetResult(result);
                    //    if (!result) cts.Cancel();
                    //});

                    //var priceFileExecutionTask = Task.Run(async () =>
                    //{
                    //    bool result = await ExecuteWithRetryAsync(() => DownloadResourceAsync(pricesUrl, pricesPath, onStart: null, onProgress: null, token), "1b - price file download", token);
                    //    priceFileTcs.TrySetResult(result);
                    //    if (!result) cts.Cancel();
                    //});

                    //await Task.WhenAll(cardDbTcs.Task, priceFileTcs.Task);

                    //bool cardDbDone = cardDbTcs.Task.IsCompletedSuccessfully;
                    //bool priceFileDone = priceFileTcs.Task.IsCompletedSuccessfully;

                    //bool cardDbSuccess = cardDbDone && cardDbTcs.Task.Result;
                    //bool priceFileSuccess = priceFileDone && priceFileTcs.Task.Result;

                    //if (!cardDbSuccess || !priceFileSuccess)
                    //{
                    //    Debug.WriteLine("[SetupPipeline] One or both downloads failed. Restarting immediately.");

                    //    cts.Cancel(); // stop the other
                    //    await Task.WhenAll(cardDbExecutionTask, priceFileExecutionTask); // wait for all cleanup
                    //    _statusVM.ProgressValue = 0;
                    //    continue;
                    //}

                    Debug.WriteLine("[SetupPipeline] Both downloads succeeded.");

                    // Inner loop table creation
                    _statusVM.StatusLabelMain = "Creating custom tables...";
                    bool tableSuccess = await ExecuteWithUnitOfWorkRetryAsync(conn => _dbSchemaRepo.CreateTablesAsync(conn), "2 - custom table creation", token);
                    if (!tableSuccess) continue;

                    _statusVM.StatusLabelMain = "Generating mana symbols...";
                    bool manaSymbolCreationSuccess = await ExecuteWithUnitOfWorkRetryAsync(conn => _missingPngService.GenerateMissingManaSymbolImagesAsync(conn), "3", token);
                    if (!manaSymbolCreationSuccess) continue;

                    _statusVM.StatusLabelMain = "Generating mana cost images...";
                    bool manaCostImageCreationSuccess = await ExecuteWithUnitOfWorkRetryAsync(conn => _missingPngService.GenerateMissingManaCostImagesAsync(conn), "4", token);
                    if (!manaCostImageCreationSuccess) continue;

                    _statusVM.StatusLabelMain = "Generating set icon images...";
                    bool keyRuneCreationSuccess = await ExecuteWithUnitOfWorkRetryAsync(conn => _missingPngService.GenerateMissingKeyRuneImagesAsync(conn), "5", token);
                    if (!keyRuneCreationSuccess) continue;

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
            await RunCompleteSetupWithRetriesAsync();


            string pricesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "prices.json");
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {

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
                    //File.Delete(pricesPath);
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

        private Task<bool> ExecuteWithRetryAsync(Func<Task> action, string stepName, CancellationToken token)
            => ExecuteWithRetryCoreAsync(action, stepName, maxRetries: 3, token);

        private async Task<bool> ExecuteWithUnitOfWorkRetryAsync(Func<SQLiteConnection, Task> action, string stepName, CancellationToken token)
        {
            return await ExecuteWithRetryCoreAsync(async () =>
            {
                await using var uow = new UnitOfWork(_dbFactory);
                await uow.BeginAsync();

                await action(uow.CurrentConnection);

                await uow.CommitAsync();
                await uow.DisposeAsync(); // redundant, but safe
            }, stepName, maxRetries: 3, token);
        }
        private Task<bool> ExecuteWithRetryAsync(Func<Task<bool>> action, string stepName, CancellationToken token) => ExecuteWithRetryCoreAsync(async () => { if (!await action()) throw new Exception("Step returned false."); }, stepName, maxRetries: 3, token);


        private async Task<bool> ExecuteWithRetryCoreAsync(Func<Task> action, string stepName, int maxRetries, CancellationToken token)
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
                    await action();
                    Debug.WriteLine($"[SetupPipeline] Step '{stepName}' succeeded!");
                    return true;
                }
                catch (Exception ex)
                {
                    _statusVM.StatusLabelBelowBar = $"Step '{stepName}' threw error on attempt {attempt}: {ex.Message}";
                    Debug.WriteLine($"[SetupPipeline] Step '{stepName}' threw error on attempt {attempt}: {ex.Message}");
                }

                await Task.Delay(3000, token).ContinueWith(_ => { });
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
