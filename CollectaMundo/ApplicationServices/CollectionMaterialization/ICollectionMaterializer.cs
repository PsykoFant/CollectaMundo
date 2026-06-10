using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared.Models;

namespace CollectaMundo.ApplicationServices.CollectionMaterialization
{
    public interface ICollectionMaterializer
    {
        CollectionCard MaterializeFromRow(MyCollectionRow row, IReadOnlyDictionary<string, PrintingCard> printingByUuid);
        IReadOnlyList<CollectionCard> MaterializeRows(IEnumerable<MyCollectionRow> rows, IReadOnlyDictionary<string, PrintingCard> printingByUuid);
        CollectionCard MergeIntoExisting(CollectionCard existing, CollectionCard incoming);
    }
}
