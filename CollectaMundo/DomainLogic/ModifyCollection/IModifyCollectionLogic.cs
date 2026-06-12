using CollectaMundo.ApplicationServices.EditCollection.Models;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CollectionMutations.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.DomainLogic.ModifyCollection
{
    public interface IModifyCollectionLogic
    {
        CollectionCardDraft PrepareCardForList(PrintingCard printing, CollectionCard? existingCollectionCard, CardToAddMetadataDto metadata, bool isEdit);
        CollectionCardDraft PrepareNewCardWithDefaults(PrintingCard selectedCard, CardToAddMetadataDto metadata);
    }
}
