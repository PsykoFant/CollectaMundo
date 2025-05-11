using CollectaMundo.DomainLogic.Models;
using CollectaMundo.ViewModels;

namespace CollectaMundo.DomainLogic
{
    public interface IEditCollectionLogic
    {
        //Task AddOrUpdateCardAsync(CardSet card, bool isEdit);
        Task<CardSet> PrepareCardForListAsync(CardSet selectedCard, bool isEdit);
        Task<CardSet> PrepareNewCardWithDefaultsAsync(CardSet selectedCard);
        Task<CardChangeEventArgs> SaveAndReturnChangesAsync(CardSet raw, bool isEdit);

    }
}
