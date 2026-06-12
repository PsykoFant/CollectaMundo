using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.DomainLogic.GenerateMissingPng;
using CollectaMundo.Infrastructure.GenerateMissingPng;
using CollectaMundo.Infrastructure.RemoteLookups;
using Newtonsoft.Json.Linq;
using ServiceStack;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.GenerateMissingPng
{
    public class GenerateMissingPngService(IUnitOfWorkRunner uowRunner, IGenerateMissingPngRepo repo, IRemoteLookups remoteLookups, IGenerateMissingPngLogic missingPngLogic) : IGenerateMissingPngService
    {
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly IGenerateMissingPngRepo _repo = repo;
        private readonly IRemoteLookups _remoteLookups = remoteLookups;
        private readonly IGenerateMissingPngLogic _missingPngLogic = missingPngLogic;
        public async Task GenerateMissingManaSymbolImagesAsync(IProgress<int>? percentProgress = null)
        {
            var symbolsWithNullImage = await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
            {
                var uniqueManaCosts = await _repo.GetUniqueValuesAsync(conn, tx, "cards", "manaCost");

                var extractedSymbols = _missingPngLogic.ExtractSymbolsFromManaCosts(uniqueManaCosts).ToList();

                foreach (string symbol in extractedSymbols)
                {
                    await _repo.InsertIfNotExistsAsync(conn, tx, "uniqueManaSymbols", "uniqueManaSymbol", symbol);
                }

                var missingSymbols = await _repo.GetValuesWithNullAsync(conn, tx, "uniqueManaSymbols", "uniqueManaSymbol", "manaSymbolImage");
                return (Result: missingSymbols, Commit: true);
            });

            using var coordinator = new ParallelWorkCoordinator<(string Symbol, byte[] PngData)>(
                    percentProgress ?? new Progress<int>(_ => { }),
                    symbolsWithNullImage.Count,
                    Environment.ProcessorCount);

            await Task.WhenAll(symbolsWithNullImage.Select(symbol => coordinator.DoAsync(async () =>
                {
                    try
                    {
                        string svgUrl = $"https://svgs.scryfall.io/card-symbols/{symbol.Replace("/", "")}.svg";

                        string? svgContent = await _remoteLookups.FetchSvgContentAsync(svgUrl);

                        byte[] pngData = string.IsNullOrWhiteSpace(svgContent)
                            ? []
                            : await _missingPngLogic.ConvertSvgToPngAsync(svgContent);

                        return (symbol, pngData);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing symbol {symbol}: {ex.Message}");
                        return (symbol, []);
                    }
                })));

            var results = coordinator.Results;

            int failed = results.Count(r => r.PngData.Length == 0);
            int total = results.Count;

            if (total > 0 && failed > total * 0.5)
            {
                throw new Exception($"More than half of mana symbol image downloads failed ({failed}/{total}).");
            }

            await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
            {
                foreach (var (symbol, pngData) in results)
                {
                    if (pngData.Length == 0)
                    {
                        Debug.WriteLine($"[PNGService] Skipped update due to empty PNG for: {symbol}");
                        continue;
                    }

                    bool updated = await _repo.UpdateImageAsync(conn, tx, tableName: "uniqueManaSymbols", imageColumn: "manaSymbolImage", referenceColumn: "uniqueManaSymbol", referenceValue: symbol, imageData: pngData);

                    if (!updated)
                    {
                        Debug.WriteLine($"[PNGService] No update performed for: {symbol}.");
                    }
                }

                return (Result: true, Commit: true);
            });
        }
        public async Task GenerateMissingManaCostImagesAsync(IProgress<int>? percentProgress = null)
        {
            var effectiveProgress = percentProgress ?? new Progress<int>(_ => { });

            var missingCosts = await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
            {
                var uniqueManaCosts = await _repo.GetUniqueValuesAsync(conn, tx, "cards", "manaCost");

                foreach (var cost in uniqueManaCosts)
                {
                    await _repo.InsertIfNotExistsAsync(conn, tx, "uniqueManaCostImages", "uniqueManaCost", cost);
                }

                var costs = await _repo.GetValuesWithNullAsync(conn, tx, "uniqueManaCostImages", "uniqueManaCost", "manaCostImage");

                return (Result: costs, Commit: true);
            });

            var allSymbols = new HashSet<string>();

            foreach (var cost in missingCosts)
            {
                string[] symbols = cost
                    .Trim('{', '}')
                    .Split(["}{"], StringSplitOptions.RemoveEmptyEntries);

                foreach (var symbol in symbols)
                {
                    allSymbols.Add(symbol);
                }
            }

            var symbolImageMap = await _uowRunner.ExecuteReadOnlyAsync(conn => _repo.GetManaSymbolImagesAsync(conn, allSymbols));

            using var reporter = new ProgressReporter(effectiveProgress, missingCosts.Count);

            var processedImages = new List<(string ManaCost, byte[] PngData)>();

            foreach (var manaCost in missingCosts)
            {
                byte[] pngData = await _missingPngLogic.ProcessManaCostInputAsync(
                    manaCost,
                    symbolImageMap);

                processedImages.Add((manaCost, pngData));

                reporter.Increment();
            }

            await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
            {
                foreach (var (manaCost, pngData) in processedImages)
                {
                    if (pngData.Length == 0)
                    {
                        continue;
                    }

                    await _repo.UpdateImageAsync(conn, tx, "uniqueManaCostImages", "manaCostImage", "uniqueManaCost", manaCost, pngData);
                }

                return (Result: true, Commit: true);
            });
        }
        public async Task GenerateMissingKeyRuneImagesAsync(IProgress<int>? percentProgress = null)
        {
            var missingSetCodes = await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
            {
                await _repo.InsertMissingFromColumnAsync(conn, tx, "sets", "code", "keyruneImages", "setCode");
                await _repo.DeleteWhereDefaultSvgUsedAsync(conn, tx);

                var setCodes = await _repo.GetValuesWithNullAsync(conn, tx, "keyruneImages", "setCode", "keyruneImage");
                return (Result: setCodes, Commit: true);
            });

            JArray? metadata = await _remoteLookups.FetchSetMetadataAsync();

            if (metadata == null)
            {
                Debug.WriteLine("Failed to fetch keyrune metadata. Aborting.");
                return;
            }

            int maxParallelism = Math.Max(2, Environment.ProcessorCount / 2);

            using var coordinator = new ParallelWorkCoordinator<(string SetCode, byte[] PngData, bool IsFallback)>(
                    percentProgress ?? new Progress<int>(_ => { }),
                    missingSetCodes.Count,
                    maxParallelism);

            await Task.WhenAll(missingSetCodes.Select(setCode => coordinator.DoAsync(async () =>
                {
                    string svgUrl = _remoteLookups.TryGetIconUriForSetCode(metadata, setCode) ?? "https://svgs.scryfall.io/sets/default.svg";

                    bool isFallback = svgUrl.Contains("default.svg", StringComparison.OrdinalIgnoreCase);

                    if (isFallback)
                    {
                        Debug.WriteLine($"[PNGService] Using default.svg fallback for set {setCode}");
                    }

                    string? svgContent = await _remoteLookups.FetchSvgContentAsync(svgUrl);

                    byte[] png = string.IsNullOrWhiteSpace(svgContent)
                        ? []
                        : await _missingPngLogic.ConvertSvgToPngAsync(svgContent);

                    return (
                        SetCode: setCode,
                        PngData: png,
                        IsFallback: isFallback
                    );
                })));

            var results = coordinator.Results;

            int updatedCount = await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
            {
                int count = 0;

                foreach (var (setCode, pngData, isFallback) in results)
                {
                    if (pngData.Length == 0)
                    {
                        continue;
                    }

                    bool updated = await _repo.UpdateKeyruneImageAsync(conn, tx, setCode, pngData, isFallback);

                    if (updated)
                    {
                        count++;
                    }
                }

                return (Result: count, Commit: true);
            });

            Debug.WriteLine($"[PNGService] Keyrune regeneration complete. Updated {updatedCount} row(s).");
        }

    }
}
