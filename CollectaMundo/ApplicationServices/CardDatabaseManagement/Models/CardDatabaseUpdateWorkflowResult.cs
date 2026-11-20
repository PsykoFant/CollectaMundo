using CollectaMundo.ApplicationServices.Shared.Operation;

namespace CollectaMundo.ApplicationServices.CardDatabaseManagement.Models
{
    public sealed record CardDatabaseUpdateWorkflowResult(OperationResult BackupResult, OperationResult? UpdateResult);
}
