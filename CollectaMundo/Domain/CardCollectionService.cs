using CollectaMundo.Data;
using CollectaMundo.Domain.CollectaMundo.Domain;
using CollectaMundo.Models;

namespace CollectaMundo.Domain
{
    public class CardCollectionService : ICardCollectionService
    {
        private readonly ICardRepository _repo;
        public CardCollectionService(ICardRepository repo) { _repo = repo; }

        public async Task AddOrUpdateCardAsync(CardSet card)
        {
            var existing = await _repo.CheckForExistingCardAsync(card);
            if (existing.HasValue)
                await _repo.UpdateCardAsync(card);
            else
                await _repo.AddCardAsync(card);
        }

        public Task DeleteCardAsync(CardSet card)
            => _repo.DeleteCardAsync(card);

        public Task UpdateCardDetailsAsync(CardSet card)
            => _repo.UpdateCardAsync(card);
    }

}
