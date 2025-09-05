using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.ApplicationServices.Utilities.Progress;
using CollectaMundo.Data.GenerateMissingPng;
using CollectaMundo.Data.RemoteLookups;
using CollectaMundo.DomainLogic.GenerateMissingPng;
using Newtonsoft.Json.Linq;
using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.GenerateMissingPng
{
    public class GenerateMissingPngService(IGenerateMissingPngRepository repository, IRemoteLookups scryfallLookups, IGenerateMissingPngLogic logic) : IGenerateMissingPngService
    {
        private readonly IGenerateMissingPngRepository _repository = repository;
        private readonly IRemoteLookups _scryfallLookups = scryfallLookups;
        private readonly IGenerateMissingPngLogic _logic = logic;
        public async Task GenerateMissingManaSymbolImagesAsync(SQLiteConnection conn, IProgress<int>? percentProgress = null)
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
            using var coordinator = new ParallelWorkCoordinator<(string Symbol, byte[] PngData)>(percentProgress ?? new Progress<int>(_ => { }), symbolsWithNullImage.Count, Environment.ProcessorCount);

            await Task.WhenAll(symbolsWithNullImage.Select(symbol =>
                coordinator.DoAsync(async () =>
                {
                    try
                    {
                        string svgUrl = $"https://svgs.scryfall.io/card-symbols/{symbol.Replace("/", "")}.svg";
                        string? svgContent = await _scryfallLookups.FetchSvgContentAsync(svgUrl);
                        byte[] pngData = string.IsNullOrWhiteSpace(svgContent)
                            ? []
                            : await _logic.ConvertSvgToPngAsync(svgContent);
                        return (symbol, pngData);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing symbol {symbol}: {ex.Message}");
                        return (symbol, []);
                    }
                })
            ));

            var results = coordinator.Results;

            // Step 6: Fail if too many results failed
            int failed = results.Count(r => r.PngData.Length == 0);
            int total = results.Count;
            if (failed > total * 0.5)
            {
                throw new Exception($"More than half of mana symbol image downloads failed ({failed}/{total}).");
            }

            // Step 7: Update the DB for each result
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
        public async Task GenerateMissingManaCostImagesAsync(SQLiteConnection conn, IProgress<int>? percentProgress = null)
        {
            var effectiveProgress = percentProgress ?? new Progress<int>(_ => { }); // Use percentProgress if provided, otherwise use a no-op progress reporter
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
            using var reporter = new ProgressReporter(effectiveProgress, missingCosts.Count);

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

                reporter.Increment(); // Updates progress with throttle
            }

            transaction.Commit();

        }
        public async Task GenerateMissingKeyRuneImagesAsync(SQLiteConnection conn, IProgress<int>? percentProgress = null)
        {
            // Setup
            await _repository.InsertMissingFromColumnAsync(conn, "sets", "code", "keyruneImages", "setCode");
            await _repository.InsertMissingFromColumnAsync(conn, "sets", "code", "keyruneImages", "setCode");

            // slet default set koder

            var missingSetCodes = await _repository.GetValuesWithNullAsync(conn, "keyruneImages", "setCode", "keyruneImage");

            JArray? metadata = await _scryfallLookups.FetchSetMetadataAsync();
            if (metadata == null)
            {
                Debug.WriteLine($"Failed to fetch keyrune metadata. Aborting.");
                return;
            }

            // Parallel processing with progress
            int maxParallelism = Math.Max(2, Environment.ProcessorCount / 2);
            using var coordinator = new ParallelWorkCoordinator<(string SetCode, byte[] PngData)>(percentProgress ?? new Progress<int>(_ => { }), missingSetCodes.Count, maxParallelism);

            await Task.WhenAll(missingSetCodes.Select(setCode =>
                coordinator.DoAsync(async () =>
                {
                    string svgUrl = _scryfallLookups.TryGetIconUriForSetCode(metadata, setCode) ?? "https://svgs.scryfall.io/sets/default.svg";

                    string? svgContent = await _scryfallLookups.FetchSvgContentAsync(svgUrl);
                    byte[] png = string.IsNullOrWhiteSpace(svgContent)
                        ? []
                        : await _logic.ConvertSvgToPngAsync(svgContent);

                    return (SetCode: setCode, PngData: png);
                })
            ));

            var results = coordinator.Results;

            // Database update
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
        }
    }
}
