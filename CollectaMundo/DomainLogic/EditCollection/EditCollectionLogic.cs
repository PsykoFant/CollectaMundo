using CollectaMundo.Data.EditCollection;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.EditCollection.Models;
using System.Data.SQLite;

namespace CollectaMundo.DomainLogic.EditCollection
{
    public class EditCollectionLogic(IEditCollectionRepository repo) : IEditCollectionLogic
    {
        private readonly IEditCollectionRepository _repo = repo;
        public async Task<CardSet> PrepareCardForListAsync(CardSet selectedCard, bool isEdit, SQLiteConnection connection)
        {
            var clone = await CloneWithMetadataHelperAsync(selectedCard, connection);

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
        public async Task<CardSet> PrepareNewCardWithDefaultsAsync(CardSet selectedCard, SQLiteConnection connection)
        {
            var clone = await CloneWithMetadataHelperAsync(selectedCard, connection);

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
        private async Task<CardSet> CloneWithMetadataHelperAsync(CardSet src, SQLiteConnection connection)
        {
            if (src?.Uuid == null)
                throw new ArgumentException("UUID cannot be null", nameof(src));

            // fetch just once
            var finishes = await _repo.FetchFinishesForCardAsync(src.Uuid, connection);
            var languages = await _repo.FetchLanguagesForCardAsync(src.Uuid, connection);

            // New: build clone from Core so image byte forwarders work
            CardSet clone;
            if (src.Core != null)
            {
                clone = CardSet.FromCore(src.Core);
            }
            else
            {
                // Fallback (shouldn't happen in the new flow): reconstruct a minimal Core from src
                var core = new CardCore
                {
                    Uuid = src.Uuid,
                    Name = src.Name ?? "",
                    SetName = src.SetName,
                    ReleaseDate = src.ReleaseDate,
                    ManaCostRaw = src.ManaCostRaw,
                    ManaCost = src.ManaCost,
                    ManaValue = src.ManaValue,
                    Colors = src.Colors,
                    Type = src.Type,
                    Types = src.Types,
                    SuperTypes = src.SuperTypes,
                    SubTypes = src.SubTypes,
                    Keywords = src.Keywords,
                    Text = src.Text,
                    Side = src.Side,
                    Rarity = src.Rarity,
                    Finishes = src.Finishes,
                    Language = src.Language,
                    NormalPrice = src.NormalPrice,
                    FoilPrice = src.FoilPrice,
                    EtchedPrice = src.EtchedPrice,

                    // These read from src’s forwarders; will be null if src had no Core
                    KeyRuneImageBytes = src.KeyRuneImageBytes,
                    ManaCostImageBytes = src.ManaCostImageBytes,
                };

                clone = CardSet.FromCore(core);
            }

            // Copy over mutable / view-only extras you previously set in the initializer
            clone.CardInCollectionPrice = src.CardInCollectionPrice;
            clone.SelectedFinish = src.SelectedFinish;
            clone.SelectedCondition = src.SelectedCondition;
            clone.Count = src.Count; // if relevant

            // Attach lookup lists
            clone.AvailableFinishes = finishes ?? new List<string>();
            clone.OtherLanguages = languages;

            return clone;
        }


        // Save a card and return the changes to viewmodel
        public async Task<IReadOnlyList<CardChangeEventArgs>> SaveBatchAsync(IEnumerable<CardSet> cards, bool isEdit, SQLiteConnection connection)
        {
            // explicitly tell the compiler what delegate type we're using
            Func<CardSet, Task<CardChangeEventArgs>> persister = isEdit
                ? (card) => PersistEditedCardsAndReturnChangesAsync(card, connection)
                : (card) => PersistAddedCardsAndReturnChangesAsync(card, connection);

            // now Task.WhenAll can infer correctly
            var results = await Task.WhenAll(cards.Select(card => persister(card)));
            return results;
        }
        private async Task<CardChangeEventArgs> PersistAddedCardsAndReturnChangesAsync(CardSet card, SQLiteConnection connection)
        {
            // Do we already have a db row?
            var existingId = await _repo.FindExistingCardReturnIdAsync(card, connection);
            if (existingId.HasValue)
            {
                // update counts in db
                card.CardId = existingId.Value;
                await _repo.UpdateCardCountsAsync(card, connection);

                // Get new totals
                int sumOwned;
                int sumTrade;
                (sumOwned, sumTrade) = await _repo.GetTotalsAsync(card.Uuid!, card.SelectedCondition!, card.Language!, card.SelectedFinish!, connection);

                card.CardsOwned = sumOwned;
                card.CardsForTrade = sumTrade;
            }
            else
            {
                // Insert and grab the new PK in one shot
                card.CardId = await _repo.AddCardAndReturnIdAsync(card, connection);
            }
            return new CardChangeEventArgs(card, []);
        }
        private async Task<CardChangeEventArgs> PersistEditedCardsAndReturnChangesAsync(CardSet card, SQLiteConnection connection)
        {
            // 1) Deletion-by-zero?
            if (card.CardsOwned == 0)
            {
                // delete in DB
                await _repo.DeleteCardByIdAsync(card, connection);

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

            await _repo.UpdateCardAsync(card, connection);

            // Now we check for duplicate values for a merge scenario
            var allIds = await _repo.FindRecordByIdAsync(card.Uuid!, card.SelectedCondition!, card.Language!, card.SelectedFinish!, connection);

            if (allIds.Count > 1)
            {
                // pick lowest‐PK as “keeper”
                allIds.Sort();
                keepId = allIds[0];
                removedIds = [.. allIds.Skip(1)];

                // get the total sums in one shot
                (sumOwned, sumTrade) = await _repo.GetTotalsAsync(card.Uuid!, card.SelectedCondition!, card.Language!, card.SelectedFinish!, connection);

                // merge in DB
                await _repo.MergeDuplicateRecordsAsync(card.Uuid!, card.SelectedCondition!, card.Language!, card.SelectedFinish!, keepId, connection);

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
