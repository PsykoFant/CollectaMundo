using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.DomainLogic.Shared.CollectionSnapshot;

namespace CollectaMundo.ApplicationServices.Decks.DeckExport
{
    public interface IDeckExportService
    {
        Task ExportCompleteDeckCsvAsync(int deckLocationId, IReadOnlyList<OracleCard> oracleCards, string filePath, CancellationToken cancellationToken = default);
        Task<string> GenerateCardmarketWantListAsync(int deckLocationId, IReadOnlyList<OracleCard> oracleCards, ICollectionQuantitySnapshot quantitySnapshot);
    }
}
