using CollectaMundo.Infrastructure.Shared.Models;

namespace CollectaMundo.ApplicationServices.Import.Models
{
    public sealed class ImportCollectionUpsertResult
    {
        // DB-truth rows: CardId + Identity + quantities
        public IReadOnlyList<CollectionCardDbRow> UpsertedRows { get; init; } = [];
    }
}
