using CollectaMundo.ApplicationServices.Utilities;
namespace CollectaMundo.ApplicationServices.CardDatabaseManagement
{
    public interface ICardDatabaseManagementService
    {
        Task<OperationResult> FirstTimeDbPrepOrchetrator(int defaultDelay = 3000);
        Task<OperationResult> CheckForDbUpdatesAsync(CancellationToken ct = default);
        Task<OperationResult> UpdateDbPrepOrchetrator(int defaultDelay = 3000, CancellationToken ct = default);
        Task<OperationResult> UpdateCardPricesOrchetrator(int defaultDelay = 3000, CancellationToken ct = default);
        Task<OperationResult> ExportCollectionAsync(CancellationToken ct = default);
    }

}
