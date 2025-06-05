using CollectaMundo.Data.GenerateMissingPng;
using CollectaMundo.Data.ScryfallLookups;
using CollectaMundo.DomainLogic.GenerateMissingPng;
using CollectaMundo.ViewModels;
using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.GenerateMissingPng
{
    public class GenerateMissingPngService(IGenerateMissingPngRepository repository, IScryfallLookups scryfallLookups, IGenerateMissingPngLogic logic) : IGenerateMissingPngService
    {
        private readonly IGenerateMissingPngRepository _repository = repository;
        private readonly IScryfallLookups _scryfallLookups = scryfallLookups;
        private readonly IGenerateMissingPngLogic _logic = logic;

        public async Task GenerateMissingManaSymbolImagesAsync(SQLiteConnection conn, StatusViewModel statusVm)
        {
            statusVm.StatusMessage = "Generating mana symbols...";

            try
            {
                // Step 1: Get unique mana cost strings from 'cards' table
                List<string> uniqueManaCosts = await _repository.GetUniqueValuesAsync(conn, "cards", "manaCost");

                // Step 2: Use logic layer to extract unique symbols from mana cost strings
                List<string> extractedSymbols = _logic.ExtractSymbolsFromManaCosts(uniqueManaCosts).ToList();


                // Step 3: Insert any new symbols into the uniqueManaSymbols table
                foreach (string symbol in extractedSymbols)
                {
                    await _repository.InsertIfNotExistsAsync(conn, "uniqueManaSymbols", "uniqueManaSymbol", symbol);
                }

                // Step 4: Get symbols where the PNG image is missing
                List<string> symbolsWithNullImage = await _repository.GetValuesWithNullAsync(conn, "uniqueManaSymbols", "uniqueManaSymbol", "manaSymbolImage");

                // Step 5: Generate PNGs for each and update the DB
                foreach (string symbol in symbolsWithNullImage)
                {
                    string svgUrl = $"https://svgs.scryfall.io/card-symbols/{symbol.Replace("/", "")}.svg";

                    byte[] pngData = await _logic.DownloadAndConvertSvgToPngAsync(svgUrl);

                    if (pngData.Length > 0)
                    {
                        await _repository.UpdateImageAsync(
                            conn,
                            table: "uniqueManaSymbols",
                            imageColumn: "manaSymbolImage",
                            keyColumn: "uniqueManaSymbol",
                            keyValue: symbol,
                            imageData: pngData);
                    }
                    else
                    {
                        Debug.WriteLine($"Skipped empty PNG result for symbol: {symbol}");
                    }
                }

                statusVm.StatusMessage = "Mana symbol generation complete.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PNGService] Error generating mana symbol images: {ex.Message}");
                statusVm.StatusMessage = $"Error generating mana symbol images: {ex.Message}";
            }
        }
        public async Task GenerateMissingManaCostImagesAsync(SQLiteConnection conn, StatusViewModel statusVm)
        {
            statusVm.StatusMessage = "Generating mana cost images...";

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
                        .Split(new[] { "}{" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var s in symbols)
                        allSymbols.Add(s);
                }

                // Batch load all needed symbols once
                var symbolImageMap = await _repository.GetManaSymbolImagesAsync(conn, allSymbols);

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
                }

                statusVm.StatusMessage = "Mana cost image generation complete.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PngService] Error generating mana cost images: {ex.Message}");
                statusVm.StatusMessage = $"Error generating mana cost images: {ex.Message}";
            }
        }
        public async Task GenerateMissingKeyRuneImagesAsync(SQLiteConnection conn, StatusViewModel statusVm)
        {
            statusVm.StatusMessage = "Generating keyrune images...";

            try
            {
                // Step 1: Ensure all potential set codes exist in keyruneImages table
                await _repository.CopyColumnIfEmptyOrAddMissingRowsAsync(conn, "keyruneImages", "setCode", "sets", "code");
                await _repository.CopyColumnIfEmptyOrAddMissingRowsAsync(conn, "keyruneImages", "setCode", "sets", "tokenSetCode");

                // Step 2: Find set codes missing keyrune images
                var missingSetCodes = await _repository.GetValuesWithNullAsync(conn, "keyruneImages", "setCode", "keyruneImage");

                // Step 3: Download all set metadata from Scryfall once
                var allSetMetadata = await _scryfallLookups.FetchSetMetadataAsync();
                if (allSetMetadata == null)
                {
                    statusVm.StatusMessage = "Failed to fetch keyrune metadata. Aborting.";
                    Debug.WriteLine("[PNGService] Skipping keyrune image generation due to null metadata.");
                    return;
                }

                // Step 4: Convert SVGs to PNG in parallel
                var imageTasks = missingSetCodes.Select(setCode =>
                    _logic.ProcessSetSvgAsync(setCode, allSetMetadata)).ToList();

                var results = await Task.WhenAll(imageTasks);

                // Step 5: Insert images where applicable
                foreach (var (SetCode, PngData) in results.Where(r => r.PngData.Length > 0))
                {
                    await _repository.UpdateImageAsync(conn, "keyruneImages", "keyruneImage", "setCode", SetCode, PngData);
                }

                statusVm.StatusMessage = "Keyrune image generation complete.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PNGService] Error generating keyrune images: {ex.Message}");
                statusVm.StatusMessage = $"Error generating keyrune images: {ex.Message}";
            }
        }

    }
}
