using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.DomainLogic.Shared.CollectionSnapshot;

namespace CollectaMundo.ApplicationServices.Decks.DeckExport
{
    public interface IDeckExportService
    {
        Task<DeckExportAvailability> GetAvailabilityAsync(int deckLocationId, IReadOnlyList<OracleCard> oracleCards, ICollectionQuantitySnapshot quantitySnapshot);
        Task<string?> ExportCompleteDeckCsvAsync(int deckLocationId, string deckName, IReadOnlyList<OracleCard> oracleCards, CancellationToken cancellationToken = default);
        Task<string> GenerateWantListAsync(int deckLocationId, IReadOnlyList<OracleCard> oracleCards, ICollectionQuantitySnapshot quantitySnapshot);
        void CopyWantListToClipboard(string wantList);
    }
}
