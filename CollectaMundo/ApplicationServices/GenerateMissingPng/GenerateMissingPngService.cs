using CollectaMundo.Data.GenerateMissingPng;
using CollectaMundo.Data.ScryfallLookups;
using CollectaMundo.DomainLogic.GenerateMissingPng;
using CollectaMundo.ViewModels;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace CollectaMundo.ApplicationServices.GenerateMissingPng
{
    public class GenerateMissingPngService(IGenerateMissingPngRepository repository, IScryfallLookups scryfallLookups, IGenerateMissingPngLogic logic) : IGenerateMissingPngService
    {
        private readonly IGenerateMissingPngRepository _repository = repository;
        private readonly IScryfallLookups _scryfallLookups = scryfallLookups;
        private readonly IGenerateMissingPngLogic _logic = logic;

        public async Task GenerateMissingManaSymbolImagesAsync(SQLiteConnection conn, StatusViewModel statusVM)
        {
            // Reset progress status
            statusVM.Show("Generating mana symbol images...", true);
            statusVM.ProgressValue = 0;

            try
            {
                // Step 1: Get unique mana cost strings from 'cards' table
                List<string> uniqueManaCosts = await _repository.GetUniqueValuesAsync(conn, "cards", "manaCost");

                // Step 2: Use logic layer to extract unique symbols from mana cost strings
                List<string> extractedSymbols = [.. _logic.ExtractSymbolsFromManaCosts(uniqueManaCosts)];

                // Step 3: Insert any new symbols into the uniqueManaSymbols table
                foreach (string symbol in extractedSymbols)
                {
                    await _repository.InsertIfNotExistsAsync(conn, "uniqueManaSymbols", "uniqueManaSymbol", symbol);
                }

                // Step 4: Get symbols where the PNG image is missing
                List<string> symbolsWithNullImage = await _repository.GetValuesWithNullAsync(conn, "uniqueManaSymbols", "uniqueManaSymbol", "manaSymbolImage");

                // Step 5: Generate PNGs for each symbol in parallel
                int maxParallelism = Environment.ProcessorCount; // Or manually: 4, 8 etc.
                var semaphore = new SemaphoreSlim(maxParallelism);
                int completed = 0;
                int total = symbolsWithNullImage.Count;
                var results = new ConcurrentBag<(string Symbol, byte[] PngData)>();

                await Task.WhenAll(symbolsWithNullImage.Select(async symbol =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        string svgUrl = $"https://svgs.scryfall.io/card-symbols/{symbol.Replace("/", "")}.svg";
                        string? svgContent = await _scryfallLookups.FetchSvgContentAsync(svgUrl);
                        byte[] pngData = string.IsNullOrWhiteSpace(svgContent)
                            ? []
                            : await _logic.ConvertSvgToPngAsync(svgContent);

                        results.Add((symbol, pngData));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing symbol {symbol}: {ex.Message}");
                    }
                    finally
                    {
                        Interlocked.Increment(ref completed);
                        statusVM.ProgressValue = (int)((double)completed / total * 100);
                        semaphore.Release();
                    }
                }));

                // Step 6: Update the DB for each result
                using var transaction = conn.BeginTransaction();

                foreach (var (symbol, pngData) in results)
                {
                    if (pngData.Length > 0)
                    {
                        bool updated = await _repository.UpdateImageAsync(
                            conn,
                            tableName: "uniqueManaSymbols",
                            imageColumn: "manaSymbolImage",
                            referenceColumn: "uniqueManaSymbol",
                            referenceValue: symbol,
                            imageData: pngData);

                        if (!updated)
                        {
                            Debug.WriteLine($"[PNGService] No update performed for: {symbol} (already present?)");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[PNGService] Skipped update due to empty PNG for: {symbol}");
                    }
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PNGService] Error generating mana symbol images: {ex.Message}");
                statusVM.StatusMessage = $"Error generating mana symbol images: {ex.Message}";
            }
        }
        public async Task GenerateMissingManaCostImagesAsync(SQLiteConnection conn, StatusViewModel statusVM)
        {
            statusVM.Show("Generating mana cost images...", true);
            statusVM.ProgressValue = 0;

            try
            {
                var uniqueManaCosts = await _repository.GetUniqueValuesAsync(conn, "cards", "manaCost");

                foreach (var cost in uniqueManaCosts)
                {
                    await _repository.InsertIfNotExistsAsync(conn, "uniqueManaCostImages", "uniqueManaCost", cost);
                }

                var missingCosts = await _repository.GetValuesWithNullAsync(conn, "uniqueManaCostImages", "uniqueManaCost", "manaCostImage");

                // Extract all symbols needed
                var allSymbols = new HashSet<string>();
                foreach (var cost in missingCosts)
                {
                    string[] symbols = cost.Trim('{', '}')
                        .Split(["}{"], StringSplitOptions.RemoveEmptyEntries);
                    foreach (var s in symbols)
                    {
                        allSymbols.Add(s);
                    }
                }

                // Batch load all needed symbols once
                var symbolImageMap = await _repository.GetManaSymbolImagesAsync(conn, allSymbols);

                using var transaction = conn.BeginTransaction();

                int total = missingCosts.Count;
                int processed = 0;

                foreach (var manaCost in missingCosts)
                {
                    byte[] pngData = await _logic.ProcessManaCostInputAsync(manaCost, symbolImageMap);

                    if (pngData.Length > 0)
                    {
                        await _repository.UpdateImageAsync(conn,
                            "uniqueManaCostImages",
                            "manaCostImage",
                            "uniqueManaCost",
                            manaCost,
                            pngData);
                    }

                    // Update progress every 10 items or at the end to avoid spamming UI
                    processed++;
                    if (processed % 10 == 0 || processed == total)
                    {
                        statusVM.ProgressValue = (int)((double)processed / total * 100);
                    }
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PngService] Error generating mana cost images: {ex.Message}");
                statusVM.StatusMessage = $"Error generating mana cost images: {ex.Message}";
            }
        }
        public async Task GenerateMissingKeyRuneImagesAsync(SQLiteConnection conn, StatusViewModel statusVM)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            statusVM.Show("Generating set symbol images...", true);
            statusVM.ProgressValue = 0;

            try
            {
                stopwatch.Start();

                // Ensure all potential set codes exist in keyruneImages table
                await _repository.InsertMissingFromColumnAsync(conn, "sets", "code", "keyruneImages", "setCode");
                await _repository.InsertMissingFromColumnAsync(conn, "sets", "code", "keyruneImages", "setCode");

                var missingSetCodes = await _repository.GetValuesWithNullAsync(conn, "keyruneImages", "setCode", "keyruneImage");

                JArray? metadata = await _scryfallLookups.FetchSetMetadataAsync();
                if (metadata == null)
                {
                    statusVM.StatusMessage = "Failed to fetch keyrune metadata. Aborting.";
                    return;
                }
                stopwatch.Stop();
                Debug.WriteLine($"[PNGService] Fetched metadata in {stopwatch.ElapsedMilliseconds} ms.");

                stopwatch.Reset();
                stopwatch.Start();
                // Use throttled parallelism for balance
                //int maxParallelism = Environment.ProcessorCount;
                int maxParallelism = Math.Max(2, Environment.ProcessorCount / 2);

                var semaphore = new SemaphoreSlim(maxParallelism);
                var results = new ConcurrentBag<(string SetCode, byte[] PngData)>();
                int completed = 0;
                int total = missingSetCodes.Count;

                await Task.WhenAll(missingSetCodes.Select(async setCode =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        string svgUrl = _scryfallLookups.TryGetIconUriForSetCode(metadata, setCode)
                                        ?? "https://svgs.scryfall.io/sets/default.svg";

                        string? svgContent = await _scryfallLookups.FetchSvgContentAsync(svgUrl);
                        byte[] png = string.IsNullOrWhiteSpace(svgContent)
                            ? Array.Empty<byte>()
                            : await _logic.ConvertSvgToPngAsync(svgContent);

                        results.Add((SetCode: setCode, PngData: png));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PNGService] Error processing set {setCode}: {ex.Message}");
                    }
                    finally
                    {
                        int done = Interlocked.Increment(ref completed);
                        if (done % 10 == 0 || done == total)
                        {
                            int percent = (int)((double)done / total * 100);

                            // Schedule on dispatcher ASAP
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                statusVM.ProgressValue = percent;
                            }, DispatcherPriority.Render); // Render is higher priority than Background
                        }

                        semaphore.Release();
                    }


                }));

                stopwatch.Stop();
                Debug.WriteLine($"[PNGService] Processed {missingSetCodes.Count} set codes in {stopwatch.ElapsedMilliseconds} ms.");

                stopwatch.Reset();
                stopwatch.Start();

                using var transaction = conn.BeginTransaction();
                int updatedCount = 0;

                foreach (var (SetCode, PngData) in results)
                {
                    if (PngData.Length > 0)
                    {
                        bool updated = await _repository.UpdateImageAsync(
                            conn,
                            tableName: "keyruneImages",
                            imageColumn: "keyruneImage",
                            referenceColumn: "setCode",
                            referenceValue: SetCode,
                            imageData: PngData);

                        if (updated)
                        {
                            updatedCount++;
                        }
                        else
                        {
                            Debug.WriteLine($"[PNGService] Keyrune for {SetCode} was already populated. Skipping update.");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[PNGService] Empty PNG for set: {SetCode} — not updating.");
                    }
                }

                transaction.Commit();
                stopwatch.Stop();
                Debug.WriteLine($"[PNGService] Inserted {updatedCount} keyrune images into db in {stopwatch.ElapsedMilliseconds} ms.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PNGService] Error generating keyrune images: {ex.Message}");
                statusVM.StatusMessage = $"Error: {ex.Message}";
            }
        }


    }
}
