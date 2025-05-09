using CollectaMundo.DomainLogic.Models;
using CollectaMundo.ViewModels;

namespace CollectaMundo.DomainLogic
{
    public interface IEditCollectionLogic
    {
        Task AddOrUpdateCardAsync(CardSet card, bool isEdit);
        Task<CardSet> PrepareCardForListAsync(CardSet selectedCard, bool isEdit);
        Task<CardSet> PrepareNewCardWithDefaultsAsync(CardSet selectedCard);

        /// <summary>
        /// Persist & fetch back the survivor (or delete‐marker if count==0).
        /// </summary>
        Task<CardSet> SaveAndFetchAsync(CardSet card, bool isEdit);

        /// <summary>
        /// Prep with defaults, then persist & fetch.
        /// </summary>
        Task<CardSet> SaveWithDefaultsAsync(CardSet raw);

        Task<CardChangeEventArgs> SaveAndReturnChangesAsync(CardSet raw, bool isEdit);

    }
}
