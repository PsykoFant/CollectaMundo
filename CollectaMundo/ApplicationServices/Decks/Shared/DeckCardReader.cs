using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.Infrastructure.Decks;

namespace CollectaMundo.ApplicationServices.Decks.Shared
{
    public sealed class DeckCardReader(IUnitOfWorkRunner uowRunner, IDeckBuilderRepo deckBuilderRepo) : IDeckCardReader
    {
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly IDeckBuilderRepo _deckBuilderRepo = deckBuilderRepo;
        public Task<IReadOnlyList<DeckCardEntry>> LoadAsync(int deckLocationId)
        {
            return _uowRunner.ExecuteReadOnlyAsync(conn => _deckBuilderRepo.GetByDeckLocationIdAsync(conn, deckLocationId));
        }
    }
}
