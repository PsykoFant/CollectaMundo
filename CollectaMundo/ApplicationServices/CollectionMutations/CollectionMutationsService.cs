using CollectaMundo.DomainLogic.CollectionMutations;
using CollectaMundo.DomainLogic.CollectionMutations.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.CollectionMutations;
using CollectaMundo.Infrastructure.Shared.Models;
using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.CollectionMutations
{
    public class CollectionMutationsService(ICollectionMutationsLogic logic, ICollectionMutationsRepo repo) : ICollectionMutationsService
    {
        private readonly ICollectionMutationsLogic _logic = logic;
        private readonly ICollectionMutationsRepo _repo = repo;
        public async Task<CollectionChangeSet<CollectionCardDbRow>> SubmitBatchAsync(IEnumerable<CollectionCardDraft> cards, ICollectionSnapshot snapshot, SQLiteConnection connection, SQLiteTransaction transaction)
        {
            Debug.WriteLine($"Submitting batch of {cards.Count()} cards with snapshot of {snapshot.Rows.Count} cards");
            foreach (var card in cards)
            {
                Debug.WriteLine($"Card draft: {card.Uuid} - {card.SelectedCondition} - {card.Language} - {card.SelectedFinish} - {card.CardId} - {card.Comment} - Owned: {card.CardsOwned} - ForTrade: {card.CardsForTrade}");
            }

            var plan = _logic.PlanIdentityRewriteBatch(cards, snapshot);

            await ExecutePlanAsync(plan, connection, transaction);

            plan.ChangeSet = BuildExecutedChangeSet(plan);

            foreach (var row in plan.ChangeSet.AddedOrUpdated)
            {
                Debug.WriteLine($"ChangeSet row: CardId={row.CardId}, Identity={row.Identity}");
            }

            return plan.ChangeSet;
        }
        private static CollectionChangeSet<CollectionCardDbRow> BuildExecutedChangeSet(CollectionMutationPlan plan)
        {
            var rows = plan.UpsertsByIdentity.Values.Select(row =>
                {
                    var matchingInsert = plan.Inserts.FirstOrDefault(i =>
                        i.Identity == row.Identity);

                    if (matchingInsert?.AssignedCardId is int assignedId)
                    {
                        return new CollectionCardDbRow
                        {
                            CardId = assignedId,
                            Identity = row.Identity,
                            CardsOwned = row.CardsOwned,
                            CardsForTrade = row.CardsForTrade
                        };
                    }

                    return row;
                })
                .ToList();

            return new CollectionChangeSet<CollectionCardDbRow>
            {
                RemovedIds = [.. plan.DeleteIds.Distinct()],
                AddedOrUpdated = rows
            };
        }
        private async Task ExecutePlanAsync(CollectionMutationPlan plan, SQLiteConnection connection, SQLiteTransaction transaction)
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
                Debug.WriteLine($"Insert bound. NewId={newId}, Draft.CardId={insert.Draft.CardId}");
            }

            var unbound = plan.Inserts.Where(i => i.AssignedCardId is null).ToList();

            if (unbound.Count > 0)
            {
                throw new InvalidOperationException($"Unbound insert ids: {unbound.Count}");
            }
        }
    }
}
