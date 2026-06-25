using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.Infrastructure.Decks;

namespace CollectaMundo.ApplicationServices.Decks
{
    public sealed class DeckBuilderService(IUnitOfWorkRunner uowRunner, IDeckBuilderRepo deckBuilderRepo) : IDeckBuilderService
    {
        private readonly IDeckBuilderRepo _deckBuilderRepo = deckBuilderRepo;
        public Task<IReadOnlyList<DeckCardEntry>> LoadDeckAsync(int locationId)
        {
            return uowRunner.ExecuteReadOnlyAsync(async conn =>
            {
                return await _deckBuilderRepo.GetByDeckLocationIdAsync(conn, locationId);
            });
        }
        public Task SaveDeckAsync(int locationId, IEnumerable<DeckCardEntry> entries)
        {
            return uowRunner.ExecuteWriteAsync(async (conn, tx) =>
            {
                await _deckBuilderRepo.ReplaceDeckAsync(conn, tx, locationId, [.. entries]);

                return (Result: true, Commit: true);
            });
        }
    }
}
