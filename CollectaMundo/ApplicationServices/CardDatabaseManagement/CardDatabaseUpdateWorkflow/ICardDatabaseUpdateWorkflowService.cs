using CollectaMundo.ApplicationServices.CardDatabaseManagement.Models;

namespace CollectaMundo.ApplicationServices.CardDatabaseManagement.CardDatabaseUpdateWorkflow
{
    public interface ICardDatabaseUpdateWorkflowService
    {
        Task<CardDatabaseUpdateWorkflowResult> UpdateWithCollectionBackupAsync(CancellationToken cancellationToken = default);
    }
}
