using CollectaMundo.ApplicationServices.Decks.Shared;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Files;
using CollectaMundo.DomainLogic.Decks;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.DomainLogic.Shared.CollectionSnapshot;

namespace CollectaMundo.ApplicationServices.Decks.DeckExport
{
    public sealed class DeckExportService(IDeckCardReader deckCardReader, IDeckExportLogic deckExportLogic, ICsvFileWriter csvFileWriter, IDesktopFileSystem desktopFileSystem, IClipboardWriter clipboardWriter) : IDeckExportService
    {
        private readonly IDeckCardReader _deckCardReader = deckCardReader;
        private readonly IDeckExportLogic _deckExportLogic = deckExportLogic;
        private readonly ICsvFileWriter _csvFileWriter = csvFileWriter;
        private readonly IDesktopFileSystem _desktopFileSystem = desktopFileSystem;
        private readonly IClipboardWriter _clipboardWriter = clipboardWriter;

        // Check if deck can be exported or want list can be generated
        public async Task<DeckExportAvailability> GetAvailabilityAsync(int deckLocationId, IReadOnlyList<OracleCard> oracleCards, ICollectionQuantitySnapshot quantitySnapshot)
        {
            var states = await LoadDeckStatesAsync(deckLocationId, oracleCards);
            var completeDeck = _deckExportLogic.GetCompleteDeck(states);
            var missingCards = _deckExportLogic.GetMissingCards(states, quantitySnapshot, deckLocationId);

            return new DeckExportAvailability(CanExportCsv: completeDeck.Count > 0, CanGenerateWantList: missingCards.Count > 0);
        }

        // Generate want list
        public async Task<string> GenerateWantListAsync(int deckLocationId, IReadOnlyList<OracleCard> oracleCards, ICollectionQuantitySnapshot quantitySnapshot)
        {
            var states = await LoadDeckStatesAsync(deckLocationId, oracleCards);
            var missingCards = _deckExportLogic.GetMissingCards(states, quantitySnapshot, deckLocationId);

            return DeckExportFormatter.FormatCardmarketWantList(missingCards);
        }
        private async Task<IReadOnlyList<DeckCardState>> LoadDeckStatesAsync(int deckLocationId, IReadOnlyList<OracleCard> oracleCards)
        {
            var entries = await _deckCardReader.LoadAsync(deckLocationId);

            return DeckCardStateMapper.CreateStates(
                entries,
                oracleCards);
        }

        // Export complete deck to CSV
        public async Task<string?> ExportCompleteDeckCsvAsync(int deckLocationId, string deckName, IReadOnlyList<OracleCard> oracleCards, CancellationToken cancellationToken = default)
        {
            var filePath = PickCompleteDeckCsvFilePath(deckName);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            var states = await LoadDeckStatesAsync(deckLocationId, oracleCards);
            var deckCards = _deckExportLogic.GetCompleteDeck(states);
            var headers = DeckExportFormatter.GetCompleteDeckCsvHeaders();
            var rows = DeckExportFormatter.CreateCompleteDeckCsvRows(deckCards);

            await _csvFileWriter.WriteAsync(filePath, headers, rows, ',', cancellationToken);

            return filePath;
        }
        private string? PickCompleteDeckCsvFilePath(string deckName)
        {
            return _desktopFileSystem.PickSaveFile("Export deck as CSV", $"{deckName}.csv", "CSV Files (*.csv)|*.csv", ".csv");
        }

        public void CopyWantListToClipboard(string wantList)
        {
            _clipboardWriter.SetText(wantList);
        }
    }
}
