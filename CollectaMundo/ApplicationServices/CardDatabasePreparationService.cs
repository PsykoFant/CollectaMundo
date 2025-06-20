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

        private readonly string cardDbUrl = "https://mtgjson.com/api/v5/AllPrintings.sqliter";
        private readonly string pricesUrl = "https://mtgjson.com/api/v5/AllPricesToday.json";
        private static string _exceptionMessageA = string.Empty;
        private static string _exceptionMessageB = string.Empty;


        public CardDatabasePreparationService(IAppSettings settings, IDatabaseSchemaRepository dbSchemaRepo, ICardPriceService priceService, IGenerateMissingPngService missingPngService, StatusViewModel statusVM)
        {
            _settings = settings;
            _dbSchemaRepo = dbSchemaRepo;
            _priceService = priceService;
            _missingPngService = missingPngService;
            _statusVM = statusVM;

            _dbFactory = new DbConnectionFactory(_settings);
        }
        public async Task FirstTimeDbPrepOrchetrator()
        {
            string userDownloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            string dbPath = Path.Combine(_settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");
            string pricesPath = Path.Combine(userDownloads, "prices.json");
            const int maxTotalAttempts = 3;
            bool downloadsSucceeded = false;

            if (!IsInternetAvailable())
            {
                _statusVM.StatusLabelAboveBar = "No internet connection!";
                _statusVM.StatusLabelBelowBar = "Unfortunately, first time setup cannot continue without internet connection";
                _statusVM.StatusLabelMain = "Please check your connection. CollectaMundo will close down shortly...";
                await Task.Delay(10000);
                Application.Current.Shutdown();
            }

            _statusVM.StatusLabelAboveBar = "Performing first-time setup of card database - please wait ...";

            for (int overallAttempt = 1; overallAttempt <= maxTotalAttempts; overallAttempt++)
            {
                Debug.WriteLine($"[SetupPipeline] Starting first time db setup overall attempt {overallAttempt} of {maxTotalAttempts}.");

                if (overallAttempt != 1)
                {
                    _statusVM.StatusLabelAboveBar = $"Setup failed, retrying overall attempt {overallAttempt} of {maxTotalAttempts}...";
                    _statusVM.StatusLabelBelowBar = string.Empty;
                    _statusVM.StatusLabelMain = string.Empty;

                }

                // Reset cleanup after each new overall attempt
                try
                {
                    // List of DB-related files to delete
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

                    Debug.WriteLine("[SetupPipeline] Deleted corrupt or partial DB file(s).");
                }
                catch (Exception cleanupEx)
                {
                    Debug.WriteLine($"[SetupPipeline] Failed to delete DB file(s): {cleanupEx.Message}");
                }

                try
                {
                    // Inner loop for download attempts
                    downloadsSucceeded = await ExecuteDualDownloadWithRetryAsync(
                        token => DownloadResourceAsync(cardDbUrl, dbPath, "A", size => _statusVM.Show($"Downloading Card Database ({size})", true), percent => _statusVM.ProgressValue = percent, token),
                        token => DownloadResourceAsync(pricesUrl, pricesPath, "B", null, null, token));
                    if (!downloadsSucceeded)
                    {
                        continue;
                    }

                    #region other setup steps
                    // Inner loop table creation
                    _statusVM.StatusLabelMain = "Creating custom tables...";
                    bool tableSuccess = await ExecuteWithUnitOfWorkRetryAsync(conn => _dbSchemaRepo.CreateTablesAsync(conn), "2 - custom table creation");
                    if (!tableSuccess)
                    {
                        continue;
                    }

                    // Inner loop for generating images
                    _statusVM.StatusLabelMain = "Generating mana symbols...";
                    bool manaSymbolCreationSuccess = await ExecuteWithUnitOfWorkRetryAsync(conn => _missingPngService.GenerateMissingManaSymbolImagesAsync(conn), "3");
                    if (!manaSymbolCreationSuccess)
                    {
                        continue;
                    }

                    // Inner loop for generating mana cost images
                    _statusVM.StatusLabelMain = "Generating mana cost images...";
                    bool manaCostImageCreationSuccess = await ExecuteWithUnitOfWorkRetryAsync(conn => _missingPngService.GenerateMissingManaCostImagesAsync(conn), "4");
                    if (!manaCostImageCreationSuccess)
                    {
                        continue;
                    }


                    _statusVM.StatusLabelMain = "Generating set icon images...";
                    bool keyRuneCreationSuccess = await ExecuteWithUnitOfWorkRetryAsync(conn => _missingPngService.GenerateMissingKeyRuneImagesAsync(conn), "5");
                    if (!keyRuneCreationSuccess)
                    {
                        continue;
                    }

                    _statusVM.StatusLabelMain = "Processing card prices...";
                    bool importCardPricesSuccess = await ExecuteWithUnitOfWorkRetryAsync(conn => _priceService.ImportPricesFromJsonAsync(pricesPath, conn), "6");
                    if (!importCardPricesSuccess)
                    {
                        continue;
                    }

                    _statusVM.StatusLabelMain = "Creating views...";
                    bool createViewsSuccess = await Task.Run(() => ExecuteWithUnitOfWorkRetryAsync(conn => _dbSchemaRepo.CreateViewsAsync(conn, "cardmarket"), "7"));
                    if (!createViewsSuccess)
                    {
                        continue;
                    }

                    _statusVM.StatusLabelMain = "Creating indices...";
                    bool createIndicesSuccess = await Task.Run(() => ExecuteWithUnitOfWorkRetryAsync(conn => _dbSchemaRepo.CreateIndicesAsync(conn), "8"));
                    if (!createIndicesSuccess)
                    {
                        continue;
                    }

                    _statusVM.StatusLabelMain = "Optimizing database...";
                    bool optimizeDbSuccess = await Task.Run(() => ExecuteWithConnectionRetryAsync(conn => _dbSchemaRepo.OptimizeAsync(conn), "9"));
                    if (!optimizeDbSuccess)
                    {
                        continue;
                    }
                    #endregion

                    // Only clean up price file if we are exiting the setup loop (e.g. on success)
                    if (downloadsSucceeded)
                    {
                        try
                        {
                            File.Delete(pricesPath);
                        }
                        catch (IOException ex)
                        {
                            Debug.WriteLine($"Failed to delete temp prices file: {ex.Message}");
                        }
                    }

                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SetupPipeline] Attempt {overallAttempt} failed with exception: {ex.Message}");
                }
                finally
                {
                    // Reset progress bar after each overall attempt
                    _statusVM.ProgressValue = 0;
                }
            }

            //  If we reach here, all attempts have failed
            _statusVM.IsProgressVisible = false;
            _statusVM.IsSetupFailVisible = true;
            _statusVM.IsLogoVisible = false;
            _statusVM.StatusLabelAboveBar = "Setup failed after multiple attempts. Please restart the application or check your internet connection.";
            _statusVM.StatusLabelMain = "CollectaMundo will close down shortly...";

            await Task.Delay(10000);
            Application.Current.Shutdown();
        }
        private async Task<bool> ExecuteDualDownloadWithRetryAsync(Func<CancellationToken, Task<bool>> downloadA, Func<CancellationToken, Task<bool>> downloadB, int maxRetries = 3)
        {
            using var outerCts = new CancellationTokenSource();
            var outerToken = outerCts.Token;

            return await RetryLoopAsync(async (attempt, token) =>
            {
                using var innerCts = new CancellationTokenSource();
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(innerCts.Token, token);
                var linkedToken = linkedCts.Token;

                var taskA = downloadA(linkedToken);
                var taskB = downloadB(linkedToken);

                var firstCompleted = await Task.WhenAny(taskA, taskB);
                var firstResult = await firstCompleted;

                if (!firstResult)
                {
                    innerCts.Cancel();
                    await Task.WhenAll(taskA, taskB);
                    return false;
                }

                var finalA = await taskA;
                var finalB = await taskB;

                if (!finalA || !finalB)
                {
                    Debug.WriteLine($"[SetupPipeline] One of the downloads failed on attempt {attempt}.");
                    return false;
                }

                return true;
            }, "1 - downloading files", maxRetries, outerToken);
        }
        private async Task<bool> RetryLoopAsync(Func<int, CancellationToken, Task<bool>> attemptFunc, string stepName, int maxRetries, CancellationToken token)
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
                    Debug.WriteLine($"[SetupPipeline] Step '{stepName}' attempt {attempt}...");

                    if (!await attemptFunc(attempt, token))
                    {
                        var err = !string.IsNullOrEmpty(_exceptionMessageA) ? _exceptionMessageA : _exceptionMessageB;
                        throw new Exception(err);
                    }
                }
                catch (Exception ex)
                {
                    string message = $"Step '{stepName}' threw on attempt {attempt}:";
                    _statusVM.StatusLabelBelowBar = ex.Message;
                    _statusVM.StatusLabelMain = message;

                    Debug.WriteLine($"[RetryLoopAsync] {message}");
                    Debug.WriteLine($"[RetryLoopAsync] {ex.Message}");
                }
                finally
                {
                    _exceptionMessageA = _exceptionMessageB = string.Empty;
                }


                await Task.Delay(3000, token).ContinueWith(_ => { });
            }

            _statusVM.StatusLabelAboveBar = $"Step '{stepName}' failed after {maxRetries} tries. Restarting overall setup...";
            await Task.Delay(3000, token);
            return false;
        }



        // Overload without cancellation token
        private Task<bool> RetryLoopAsync(Func<int, Task<bool>> attemptFunc, string stepName, int maxRetries)
        {
            return RetryLoopAsync((i, _) => attemptFunc(i), stepName, maxRetries, CancellationToken.None);
        }

        private async Task<bool> ExecuteWithUnitOfWorkRetryAsync(Func<SQLiteConnection, Task> action, string stepName)
        {
            return await RetryLoopAsync(async attempt =>
            {
                try
                {
                    await using var uow = new UnitOfWork(_dbFactory);
                    await uow.BeginAsync();
                    await action(uow.CurrentConnection);
                    await uow.CommitAsync();
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SetupPipeline] {stepName} failed: {ex.Message}");
                    return false;
                }
            }, stepName, maxRetries: 3);
        }
        private async Task<bool> ExecuteWithConnectionRetryAsync(Func<SQLiteConnection, Task> action, string stepName)
        {
            return await RetryLoopAsync(async attempt =>
            {
                try
                {
                    await using var conn = await _dbFactory.OpenConnectionAsync();
                    await action(conn);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SetupPipeline] {stepName} failed: {ex.Message}");
                    return false;
                }
            }, stepName, maxRetries: 3);
        }
        public static async Task<bool> DownloadResourceAsync(string url, string targetPath, string taskLabel, Action<string>? onStart = null, Action<int>? onProgress = null, CancellationToken token = default)
        {
            Debug.WriteLine($"[DownloadResourceAsync] Preparing to download from {url} to {targetPath}");

            try
            {
                using var httpClient = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var buffer = new byte[8192];

                using var contentStream = await response.Content.ReadAsStreamAsync(token);
                using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                if (onStart != null && totalBytes > 0)
                    onStart($"{totalBytes / 1_000_000.0:0.0} MB");

                long totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) != 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                    totalBytesRead += bytesRead;

                    if (onProgress != null && totalBytes > 0)
                        onProgress((int)(100 * totalBytesRead / totalBytes));
                }

                Debug.WriteLine($"[DownloadResourceAsync] Download complete: {targetPath}");
                return true;
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                Debug.WriteLine($"[DownloadResourceAsync] Error downloading {url}: {msg}");

                // Only assign if both are still unset
                if (string.IsNullOrEmpty(_exceptionMessageA) && string.IsNullOrEmpty(_exceptionMessageB))
                {
                    if (taskLabel == "A")
                        _exceptionMessageA = msg;
                    else if (taskLabel == "B")
                        _exceptionMessageB = msg;
                }

                return false;
            }

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
    }
}
