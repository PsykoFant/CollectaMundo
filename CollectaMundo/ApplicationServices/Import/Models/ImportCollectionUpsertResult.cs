using CollectaMundo.DomainLogic.Shared.Models;

namespace CollectaMundo.ApplicationServices.Import.Models
{
    public sealed class ImportCollectionUpsertResult
    {
        // DB-truth rows: CardId + Identity + quantities
        public IReadOnlyList<MyCollectionRow> UpsertedRows { get; init; } = [];
    }
}
