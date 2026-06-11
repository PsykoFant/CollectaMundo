using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.ViewModels.Filtering;

namespace CollectaMundo.ApplicationServices.Filtering
{
    public interface IFacetUpdater
    {
        void RefreshFromCollection(IEnumerable<CollectionCard> collection, IReadOnlyDictionary<string, FilterItemViewModel> filters);
    }
}
