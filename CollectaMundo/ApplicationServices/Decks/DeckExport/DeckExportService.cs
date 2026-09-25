using CollectaMundo.ApplicationServices.Decks.Shared;
using CollectaMundo.ApplicationServices.Shared.Files;
using CollectaMundo.DomainLogic.Decks;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.DomainLogic.Shared.CollectionSnapshot;

namespace CollectaMundo.ApplicationServices.Decks.DeckExport
{
    public sealed class DeckExportService(IDeckCardReader deckCardReader, IDeckExportLogic deckExportLogic, ICsvFileWriter csvFileWriter) : IDeckExportService
    {
        private readonly IDeckCardReader _deckCardReader = deckCardReader;
        private readonly IDeckExportLogic _deckExportLogic = deckExportLogic;
        private readonly ICsvFileWriter _csvFileWriter = csvFileWriter;
        public async Task ExportCompleteDeckCsvAsync(int deckLocationId, IReadOnlyList<OracleCard> oracleCards, string filePath, CancellationToken cancellationToken = default)
        {
            var states = await LoadDeckStatesAsync(deckLocationId, oracleCards);
            var deckCards = _deckExportLogic.GetCompleteDeck(states);
            var headers = DeckExportFormatter.GetCompleteDeckCsvHeaders();
            var rows = DeckExportFormatter.CreateCompleteDeckCsvRows(deckCards);

            await _csvFileWriter.WriteAsync(filePath, headers, rows, ',', cancellationToken);
        }
        public async Task<string> GenerateCardmarketWantListAsync(int deckLocationId, IReadOnlyList<OracleCard> oracleCards, ICollectionQuantitySnapshot quantitySnapshot)
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
    }
}
