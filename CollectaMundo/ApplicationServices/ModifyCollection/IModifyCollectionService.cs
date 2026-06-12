using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CollectionMutations.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.Shared.Models;

namespace CollectaMundo.ApplicationServices.ModifyCollection
{
    public interface IModifyCollectionService
    {
        Task<CollectionCardDraft> CreateCardForListAsync(PrintingCard printing, CollectionCard? existingCollectionCard, bool isEdit);
        Task<CollectionChangeSet<CollectionCardDbRow>> SubmitCardBatchAsync(IEnumerable<CollectionCardDraft> cards, ICollectionSnapshot snapshot);
        Task<CollectionChangeSet<CollectionCardDbRow>> SubmitNewCardsWithDefaultsBatchAsync(IEnumerable<PrintingCard> cards, ICollectionSnapshot snapshot);
    }
}
