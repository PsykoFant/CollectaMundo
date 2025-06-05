using CollectaMundo.Data.GenerateMissingPng;
using CollectaMundo.DomainLogic.GenerateMissingPng;
using CollectaMundo.ViewModels;
using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.GenerateMissingPng
{
    public class GenerateMissingPngService(IGenerateMissingPngRepository repository, IGenerateMissingPngLogic logic) : IGenerateMissingPngService
    {
        private readonly IGenerateMissingPngRepository _repository = repository;
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
        public Task GenerateMissingManaCostImagesAsync(SQLiteConnection conn, StatusViewModel statusVm)
        {
            statusVm.StatusMessage = "Generating mana cost images... (not implemented)";
            return Task.CompletedTask;
        }
        public Task GenerateMissingKeyRuneImagesAsync(SQLiteConnection conn, StatusViewModel statusVm)
        {
            statusVm.StatusMessage = "Generating keyrune images... (not implemented)";
            return Task.CompletedTask;
        }
    }
}
