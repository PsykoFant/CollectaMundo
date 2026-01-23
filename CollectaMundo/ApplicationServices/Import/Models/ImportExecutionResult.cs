using CollectaMundo.ApplicationServices.Shared;

namespace CollectaMundo.ApplicationServices.Import.Models
{
    public sealed record ImportExecutionResult(OperationResult Result, CollectionMutation? Mutation);
}
