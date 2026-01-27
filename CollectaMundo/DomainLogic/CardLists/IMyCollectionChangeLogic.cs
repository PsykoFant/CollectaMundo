using CollectaMundo.ApplicationServices.Import.Models;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.ViewModels;

namespace CollectaMundo.DomainLogic.CardLists
{
    public interface IMyCollectionChangeLogic
    {
        CollectionChangeSet<CardSet> BuildChangeSet(CollectionMutation mutation, CardViewModel myCollection, CardViewModel allCards);
        void ApplyMyCollectionChanges(IList<CardSet> collection, CollectionChangeSet<CardSet> changes);
    }
}
