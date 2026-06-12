using CollectaMundo.ApplicationServices.Shared.Operation;

namespace CollectaMundo.ApplicationServices.Import.Models
{
    public sealed record ImportExecutionResult(OperationResult Result, ImportCollectionUpsertResult? Mutation);
}
