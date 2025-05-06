using CollectaMundo.Data;
using CollectaMundo.DomainLogic.Models;

namespace CollectaMundo.ApplicationServices
{
    public class CardListCoordinator : ICardListCoordinator
    {
        private readonly ICardListRepository _repo;

        public CardListCoordinator(ICardListRepository repo)
            => _repo = repo;

        public async Task LoadAllCardsAsync(List<CardSet> target)
        {
            var cards = await _repo.GetAllCardsAsync();    // Data layer call
            target.Clear();
            foreach (var c in cards)
                target.Add(c);
        }

        public async Task LoadMyCollectionAsync(List<CardSet> target)
        {
            var cards = await _repo.GetMyCollectionAsync(); // Data layer call
            target.Clear();
            foreach (var c in cards)
                target.Add(c);
        }
        public async Task LoadAllCardsForDecksAsync(List<CardSet> target)
        {
            var cards = await _repo.GetCardsForDecksAsync(); // Data layer call
            target.Clear();
            foreach (var c in cards)
                target.Add(c);
        }
        public async Task LoadAllCardsInDecksAsync(List<CardSet> target)
        {
            var cards = await _repo.GetCardsInDecksAsync(); // Data layer call
            target.Clear();
            foreach (var c in cards)
                target.Add(c);
        }
    }
}
