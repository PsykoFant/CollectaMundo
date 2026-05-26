using CollectaMundo.DomainLogic.CollectionMutations.Models;
using CollectaMundo.Infrastructure.CollectionMutations;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CollectionMutations
{
    public class CollectionMutationsService(ICollectionMutationsRepo repo) : ICollectionMutationsService
    {
        private readonly ICollectionMutationsRepo _repo = repo;
        public async Task ExecutePlanAsync(CollectionMutationPlan plan, SQLiteConnection connection, SQLiteTransaction transaction)
        {
            foreach (var deleteId in plan.DeleteIds)
            {
                await _repo.DeleteCardByIdAsync(deleteId, connection, transaction);
            }

            foreach (var update in plan.Updates)
            {
                await _repo.UpdateCardFieldsByIdAsync(
                    update.CardId,
                    update.CardsOwned,
                    update.CardsForTrade,
                    update.Identity.Condition,
                    update.Identity.Language,
                    update.Identity.Finish,
                    update.Identity.LocationId,
                    update.Identity.Comment,
                    connection,
                    transaction);
            }

            foreach (var insert in plan.Inserts)
            {
                var newId = await _repo.AddCardAndReturnIdAsync(
                    insert.Identity.Uuid,
                    insert.Identity.Condition,
                    insert.Identity.Language,
                    insert.Identity.Finish,
                    insert.Identity.LocationId,
                    insert.Identity.Comment,
                    insert.CardsOwned,
                    insert.CardsForTrade,
                    connection,
                    transaction);

                insert.BindCardId(newId);
            }

            var unbound = plan.Inserts.Where(i => i.AssignedCardId is null).ToList();
            if (unbound.Count > 0)
            {
                throw new InvalidOperationException($"Unbound insert ids: {unbound.Count}");
            }
        }
    }
}
