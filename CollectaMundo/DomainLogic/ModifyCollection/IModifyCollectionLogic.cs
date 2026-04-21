using CollectaMundo.ApplicationServices.EditCollection.Models;
using CollectaMundo.DomainLogic.CardLists.Models;

namespace CollectaMundo.DomainLogic.ModifyCollection
{
    public interface IModifyCollectionLogic
    {
        CardSet PrepareCardForList(CardSet selectedCard, CardToAddMetadataDto metadata, bool isEdit);
        CardSet PrepareNewCardWithDefaults(CardSet selectedCard, CardToAddMetadataDto metadata);
    }
}
