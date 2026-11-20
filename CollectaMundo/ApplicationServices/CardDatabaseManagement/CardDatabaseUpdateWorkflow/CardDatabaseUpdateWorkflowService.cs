using CollectaMundo.ApplicationServices.CardDatabaseManagement.Models;
using CollectaMundo.ApplicationServices.CollectionExport;
using CollectaMundo.ApplicationServices.Shared.Operation;

namespace CollectaMundo.ApplicationServices.CardDatabaseManagement.CardDatabaseUpdateWorkflow
{
    public sealed class CardDatabaseUpdateWorkflowService(ICollectionExportService collectionExportService, ICardDatabaseManagementService cardDatabaseManagementService) : ICardDatabaseUpdateWorkflowService
    {
        private readonly ICollectionExportService _collectionExportService = collectionExportService;
        private readonly ICardDatabaseManagementService _cardDatabaseManagementService = cardDatabaseManagementService;
        public async Task<CardDatabaseUpdateWorkflowResult> UpdateWithCollectionBackupAsync(CancellationToken cancellationToken = default)
        {
            var backupResult = await _collectionExportService.ExportFullCollectionBackupAsync(cancellationToken);

            if (backupResult.Code != OperationResultCode.Success && backupResult.Code != OperationResultCode.Empty)
            {
                return new CardDatabaseUpdateWorkflowResult(backupResult, null);
            }

            var updateResult = await _cardDatabaseManagementService.UpdateDbPrepOrchestrator(ct: cancellationToken);

            return new CardDatabaseUpdateWorkflowResult(backupResult, updateResult);
        }
    }
}
