using CollectaMundo.ApplicationServices.Shared.Operation;

namespace CollectaMundo.ApplicationServices.CardDatabaseManagement
{
    public interface ICardDatabaseManagementService
    {
        string BackupFolderPath { get; }
        Task<OperationResult> FirstTimeDbPrepOrchestrator(int defaultDelay = 3000);
        Task<OperationResult> CheckForDbUpdatesAsync(CancellationToken ct = default);
        Task<OperationResult> UpdateDbPrepOrchetrator(int defaultDelay = 3000, CancellationToken ct = default);
        Task<OperationResult> UpdateCardPricesOrchetrator(int defaultDelay = 3000, CancellationToken ct = default);
        Task<OperationResult> ExportCollectionAsync(CancellationToken ct = default);
        OperationResult ChangeBackupFolderPath(string newBackupPath);
    }

}
