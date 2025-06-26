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
    public class CardDatabasePreparationService(IAppSettings settings, IDatabaseSchemaRepository dbSchemaRepo, ICardPriceService priceService, IGenerateMissingPngService missingPngService, StatusViewModel statusVM) : ICardDatabasePreparationService
    {
        private static IDbConnectionFactory DbFactory => AppGlobals.DbFactory ?? throw new InvalidOperationException("AppContext.DbFactory is not initialized.");
        private readonly IAppSettings _settings = settings;
        private readonly IDatabaseSchemaRepository _dbSchemaRepo = dbSchemaRepo;
        private readonly ICardPriceService _priceService = priceService;
        private readonly IGenerateMissingPngService _missingPngService = missingPngService;
        private readonly StatusViewModel _statusVM = statusVM;

        private readonly string cardDbUrl = "https://mtgjson.com/api/v5/AllPrintings.sqlite";
        private readonly string pricesUrl = "https://mtgjson.com/api/v5/AllPricesToday.json";

        public async Task FirstTimeDbPrepOrchetrator()
        {
            string userDownloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            string dbPath = Path.Combine(_settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");
            string pricesPath = Path.Combine(userDownloads, "prices.json");
            const int maxTotalAttempts = 3;
            bool downloadsSucceeded = false;

            if (!IsInternetAvailable())
            {
                await DbSetupFailed(
                    statusAboveBar: "No internet connection!",
                    statusBelowBar: "Unfortunately, first time setup cannot continue without internet connection",
                    statusLabelMain: "Please check your connection. CollectaMundo will close down shortly...");
            }

            _statusVM.StatusLabel1 = "Performing first-time setup of card database - please wait ...";
            _statusVM.IsProgressVisible = true;

            for (int overallAttempt = 1; overallAttempt <= maxTotalAttempts; overallAttempt++)
            {
                Debug.WriteLine($"[FirstTimeDbPrepOrchetrator][Outer loop] Starting first time db setup overall attempt {overallAttempt} of {maxTotalAttempts}.");

                if (overallAttempt != 1)
                {
                    _statusVM.StatusLabel1 = $"Setup failed, retrying overall attempt {overallAttempt} of {maxTotalAttempts}...";
                }

                // Reset cleanup after each new overall attempt
                try { CleanupPartialDatabaseFiles(dbPath, userDownloads); }
                catch (Exception ex) { Debug.WriteLine($"[Cleanup] {ex.Message}"); }

                try
                {
                    // Step 1: Downloads (handled separately)
                    downloadsSucceeded = await ExecuteDualDownloadWithRetryAsync(
                        token => DownloadResourceAsync(cardDbUrl, dbPath, "A", size => _statusVM.Show($"Downloading Card Database ({size})", true), percent => _statusVM.ProgressValue = percent, token),
                        token => DownloadResourceAsync(pricesUrl, pricesPath, "B", null, null, token));
                    if (!downloadsSucceeded)
                    {
                        continue;
                    }

                    // Steps 2–9: Sequential execution list using centralized label handling
                    var setupSteps = new List<(string Label, Func<Task<bool>> Action)>
                    {
                        ("2. Creating custom tables...", () =>ExecuteWithUnitOfWorkRetryAsync(conn => _dbSchemaRepo.CreateTablesAsync(conn), "2. Creating custom tables...")),
                        ("3. Generating mana symbols...", () =>ExecuteWithUnitOfWorkRetryAsync(conn => _missingPngService.GenerateMissingManaSymbolImagesAsync(conn), "3. Generating mana symbols...")),
                        ("4. Generating mana cost images...", () =>ExecuteWithUnitOfWorkRetryAsync(conn => _missingPngService.GenerateMissingManaCostImagesAsync(conn), "4. Generating mana cost images...")),
                        ("5. Generating set icon images...", () =>ExecuteWithUnitOfWorkRetryAsync(conn => _missingPngService.GenerateMissingKeyRuneImagesAsync(conn), "5. Generating set icon images...")),
                        ("6. Processing card prices...", () =>ExecuteWithUnitOfWorkRetryAsync(conn => _priceService.ImportPricesFromJsonAsync(pricesPath, conn), "6. Processing card prices...")),
                        ("7. Creating views...", () =>Task.Run(() => ExecuteWithUnitOfWorkRetryAsync(conn => _dbSchemaRepo.CreateViewsAsync(conn, "cardmarket"), "7. Creating views..."))),
                        ("8. Creating indices...", () =>Task.Run(() => ExecuteWithUnitOfWorkRetryAsync(conn => _dbSchemaRepo.CreateIndicesAsync(conn), "8. Creating indices..."))),
                        ("9. Optimizing database...", () =>Task.Run(() => ExecuteWithConnectionRetryAsync(conn => _dbSchemaRepo.OptimizeAsync(conn), "9. Optimizing database...")))
                    };

                    bool allStepsSucceeded = true;

                    foreach (var (label, action) in setupSteps)
                    {
                        bool stepSuccess = await action();
                        if (!stepSuccess)
                        {
                            allStepsSucceeded = false;
                            break; // Exit current outer attempt and retry the whole setup
                        }
                    }

                    if (!allStepsSucceeded)
                    {
                        continue; // Outer loop retry
                    }

                    // Clean up only if fully successful
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

                    return; // setup completed successfully
                }

                catch (Exception ex)
                {
                    Debug.WriteLine($"[FirstTimeDbPrepOrchetrator][Outer loop] Attempt {overallAttempt} failed with exception: {ex.Message}");
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
                statusAboveBar: "Despite all the best intentions and effort, setup failed after multiple attempts.",
                statusBelowBar: "Please restart the application or check your internet connection.",
                statusLabelMain: "CollectaMundo will close down shortly...");
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

        // Retry logic for downloading files and executing database actions
        private async Task<bool> ExecuteDualDownloadWithRetryAsync(Func<CancellationToken, Task<(bool success, string? error)>> downloadA, Func<CancellationToken, Task<(bool success, string? error)>> downloadB, int maxRetries = 3)
        {
            return await RetryLoopAsync(async (attempt) =>
            {
                using var innerCts = new CancellationTokenSource();
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(innerCts.Token);
                var linkedToken = linkedCts.Token;

                var taskA = downloadA(linkedToken);
                var taskB = downloadB(linkedToken);

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
                    string error = finalA.error ?? finalB.error ?? "Unknown download error.";
                    throw new Exception(error);
                }

                return true;
            }, "1 - downloading files", maxRetries);
        }
        private async Task<bool> ExecuteWithUnitOfWorkRetryAsync(Func<SQLiteConnection, Task> action, string stepName)
        {
            return await RetryLoopAsync(async attempt =>
            {
                await using var uow = new UnitOfWork();
                await uow.BeginAsync();
                await action(uow.CurrentConnection);
                await uow.CommitAsync();
                return true; // 

            }, stepName, maxRetries: 3);
        }
        private async Task<bool> ExecuteWithConnectionRetryAsync(Func<SQLiteConnection, Task> action, string stepName)
        {
            return await RetryLoopAsync(async attempt =>
            {
                try
                {
                    await using var conn = await DbFactory.OpenConnectionAsync();
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
        private async Task<bool> RetryLoopAsync(Func<int, Task<bool>> attemptFunc, string stepName, int maxRetries)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                _statusVM.StatusLabel3 = stepName;

                try
                {
                    Debug.WriteLine($"[RetryLoopAsync] Step '{stepName}' attempt {attempt}...");

                    if (await attemptFunc(attempt))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    string message = $"Step '{stepName}' failed on attempt {attempt}:";
                    _statusVM.StatusLabel2 = ex.Message;
                    _statusVM.StatusLabel3 = message;

                    Debug.WriteLine($"[RetryLoopAsync] {message}");
                    Debug.WriteLine($"[RetryLoopAsync] {ex.Message}");
                }

                await Task.Delay(3000);
                _statusVM.StatusLabel2 = string.Empty;
            }

            _statusVM.StatusLabel1 = $"Step '{stepName}' failed after {maxRetries} tries. Restarting overall setup...";
            await Task.Delay(3000);
            return false;
        }

        // Local helper and db setup methods
        public static async Task<(bool success, string? errorMessage)> DownloadResourceAsync(string url, string targetPath, string taskLabel, Action<string>? onStart = null, Action<int>? onProgress = null, CancellationToken token = default)
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
                {
                    onStart($"{totalBytes / 1_000_000.0:0.0} MB");
                }

                long totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) != 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                    totalBytesRead += bytesRead;

                    if (onProgress != null && totalBytes > 0)
                    {
                        onProgress((int)(100 * totalBytesRead / totalBytes));
                    }
                }

                Debug.WriteLine($"[DownloadResourceAsync] Download complete: {targetPath}");
                return (true, null);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"[DownloadResourceAsync] Cancelled intentionally — suppress message");
                return (false, null); // Cancelled intentionally — suppress message
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DownloadResourceAsync] Error downloading {url}: {ex.Message}");
                return (false, $"{taskLabel} failed: {ex.Message}");
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
            _statusVM.IsProgressVisible = false;
            _statusVM.IsLogoVisible = false;
            _statusVM.IsSetupFailVisible = true;
            _statusVM.StatusLabel1 = statusAboveBar;
            _statusVM.StatusLabel2 = statusBelowBar;
            _statusVM.StatusLabel3 = statusLabelMain;

            await Task.Delay(10000);
            Application.Current.Shutdown();
        }


    }
}
