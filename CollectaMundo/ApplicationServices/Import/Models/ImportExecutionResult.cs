using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import.Models;

namespace CollectaMundo.ApplicationServices.Import.Models
{
    public sealed class ImportExecutionResult(OperationResult result, IReadOnlyList<CollectionUpsertItem> upserts)
    {
        public OperationResult Result { get; } = result;
        public IReadOnlyList<CollectionUpsertItem> Upserts { get; } = upserts;
    }
}
