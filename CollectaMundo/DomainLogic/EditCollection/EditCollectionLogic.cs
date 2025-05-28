using CollectaMundo.Data;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.EditCollection.Models;

namespace CollectaMundo.DomainLogic.EditCollection
{
    public class EditCollectionLogic(IEditCollectionRepository repo) : IEditCollectionLogic
    {
        private readonly IEditCollectionRepository _repo = repo;
        public async Task<CardSet> PrepareCardForListAsync(CardSet selectedCard, bool isEdit)
        {
            var clone = await CloneWithMetadataHelperAsync(selectedCard);

            if (isEdit)
            {
                // carry forward existing collection fields
                clone.CardId = selectedCard.CardId;
                clone.CardsOwned = selectedCard.CardsOwned;
                clone.CardsForTrade = selectedCard.CardsForTrade;
                clone.SelectedCondition = selectedCard.SelectedCondition!;
                clone.SelectedFinish = selectedCard.SelectedFinish!;
                clone.Language = selectedCard.Language!;
            }
            else
            {
                // new card defaults
                clone.CardId = null;
                clone.CardsOwned = 1;
                clone.CardsForTrade = 0;
                clone.SelectedCondition = "Near Mint";
                clone.SelectedFinish = clone.AvailableFinishes.FirstOrDefault() ?? "Near Mint";
                clone.Language = clone.OtherLanguages?.FirstOrDefault() ?? "English";
            }

            return clone;
        }
        public async Task<CardSet> PrepareNewCardWithDefaultsAsync(CardSet selectedCard)
        {
            var clone = await CloneWithMetadataHelperAsync(selectedCard);

            // always brand‐new
            clone.CardId = null;
            clone.CardsOwned = 1;
            clone.CardsForTrade = 0;
            clone.SelectedCondition = "Near Mint";

            // pick “nonfoil” if possible, else first:
            clone.SelectedFinish = clone.AvailableFinishes
                                          .FirstOrDefault(f => f.Equals("nonfoil", StringComparison.OrdinalIgnoreCase))
                                      ?? clone.AvailableFinishes.FirstOrDefault()
                                      ?? "nonfoil";

            // pick English if possible:
            clone.Language = clone.OtherLanguages?.FirstOrDefault(l => l.Equals("English", StringComparison.OrdinalIgnoreCase))
                                      ?? clone.OtherLanguages?.FirstOrDefault()
                                      ?? "English";

            return clone;
        }
        private async Task<CardSet> CloneWithMetadataHelperAsync(CardSet src)
        {
            if (src.Uuid == null)
                throw new ArgumentException("UUID cannot be null", nameof(src));

            // fetch just once
            var finishes = await _repo.FetchFinishesForCardAsync(src.Uuid);
            var languages = await _repo.FetchLanguagesForCardAsync(src.Uuid);

            // shallow‐clone of every “view” field
            var c = new CardSet
            {
                Name = src.Name,
                ManaCostRaw = src.ManaCostRaw,
                ManaCost = src.ManaCost,
                ManaValue = src.ManaValue,
                Colors = src.Colors,
                Type = src.Type,
                ManaCostImageBytes = src.ManaCostImageBytes,

                Types = src.Types,
                SuperTypes = src.SuperTypes,
                SubTypes = src.SubTypes,
                Keywords = src.Keywords,
                Text = src.Text,
                Side = src.Side,

                Uuid = src.Uuid,
                SetName = src.SetName,
                Rarity = src.Rarity,
                Finishes = src.Finishes,
                ReleaseDate = src.ReleaseDate,
                KeyRuneImageBytes = src.KeyRuneImageBytes,
                CardInCollectionPrice = src.CardInCollectionPrice,

                // lookup lists
                AvailableFinishes = finishes,
                OtherLanguages = languages,
            };

            return c;
        }

        // Save a card and return the changes to viewmodel
        public async Task<IReadOnlyList<CardChangeEventArgs>> SaveBatchAsync(IEnumerable<CardSet> cards, bool isEdit)
        {
            // explicitly tell the compiler what delegate type we're using
            Func<CardSet, Task<CardChangeEventArgs>> persister = isEdit
                ? PersistEditedCardsAndReturnChangesAsync
                : PersistAddedCardsAndReturnChangesAsync;

            // now Task.WhenAll can infer correctly
            var results = await Task.WhenAll(cards.Select(r => persister(r)));
            return results;
        }
        private async Task<CardChangeEventArgs> PersistAddedCardsAndReturnChangesAsync(CardSet card)
        {


            // Do we already have a db row?
            var existingId = await _repo.FindExistingCardReturnIdAsync(card);
            if (existingId.HasValue)
            {
                // update counts in db
                card.CardId = existingId.Value;
                await _repo.UpdateCardCountsAsync(card);

                // Get new totals
                int sumOwned;
                int sumTrade;
                (sumOwned, sumTrade) = await _repo.GetTotalsAsync(card.Uuid!, card.SelectedCondition!, card.Language!, card.SelectedFinish!);

                card.CardsOwned = sumOwned;
                card.CardsForTrade = sumTrade;
            }
            else
            {
                // Insert and grab the new PK in one shot
                card.CardId = await _repo.AddCardAndReturnIdAsync(card);
            }
            return new CardChangeEventArgs(card, []);
        }
        private async Task<CardChangeEventArgs> PersistEditedCardsAndReturnChangesAsync(CardSet card)
        {
            // 1) Deletion-by-zero?
            if (card.CardsOwned == 0)
            {
                // delete in DB
                await _repo.DeleteCardByIdAsync(card);

                // make sure we have an ID
                var deletedId = card.CardId
                    ?? throw new InvalidOperationException("Cannot delete a card without an ID");

                return new CardChangeEventArgs(deletedId);
            }

            // Persist edits
            int keepId;
            int sumOwned;
            int sumTrade;

            int[] removedIds = [];

            await _repo.UpdateCardAsync(card);

            // Now we check for duplicate values for a merge scenario
            var allIds = await _repo.FindRecordByIdAsync(card.Uuid!, card.SelectedCondition!, card.Language!, card.SelectedFinish!);

            if (allIds.Count > 1)
            {
                // pick lowest‐PK as “keeper”
                allIds.Sort();
                keepId = allIds[0];
                removedIds = [.. allIds.Skip(1)];

                // get the total sums in one shot
                (sumOwned, sumTrade) = await _repo.GetTotalsAsync(card.Uuid!, card.SelectedCondition!, card.Language!, card.SelectedFinish!);

                // merge in DB
                await _repo.MergeDuplicateRecordsAsync(card.Uuid!, card.SelectedCondition!, card.Language!, card.SelectedFinish!, keepId);

                // Build the final in‐memory survivor
                card.CardsOwned = sumOwned;
                card.CardsForTrade = sumTrade;
                card.CardId = keepId;

            }

            // Return upsert event
            return new CardChangeEventArgs(card, removedIds);
        }
    }
}
