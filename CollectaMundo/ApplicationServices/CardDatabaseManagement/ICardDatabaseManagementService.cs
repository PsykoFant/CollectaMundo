using CollectaMundo.ApplicationServices.Shared.Operation;

namespace CollectaMundo.ApplicationServices.CardDatabaseManagement
{
    public interface ICardDatabaseManagementService
    {
        Task<OperationResult> FirstTimeDbPrepOrchestrator(int defaultDelay = 3000);
        Task<OperationResult> CheckForDbUpdatesAsync(CancellationToken ct = default);
        Task<OperationResult> UpdateDbPrepOrchestrator(int defaultDelay = 3000, CancellationToken ct = default);
        Task<OperationResult> UpdateCardPricesOrchetrator(int defaultDelay = 3000, CancellationToken ct = default);
    }
}
