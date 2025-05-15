using CollectaMundo.Data;
using CollectaMundo.DomainLogic.Models;

namespace CollectaMundo.DomainLogic
{
    public class EditCollectionLogic(IEditCollectionRepository repo) : IEditCollectionLogic
    {
        private readonly IEditCollectionRepository _repo = repo;
        public async Task<CardSet> PrepareCardForListAsync(CardSet selectedCard, bool isEdit)
        {
            if (selectedCard.Uuid == null)
            {
                throw new ArgumentException("UUID cannot be null", nameof(selectedCard));
            }

            // 1) Pull the metadata you still need for a new vs. edit
            var languages = await _repo.FetchLanguagesForCardAsync(selectedCard.Uuid);
            var finishes = await _repo.FetchFinishesForCardAsync(selectedCard.Uuid);

            // 2) Decide which values to use in edit vs. add
            string chosenFinish = isEdit
                                        ? selectedCard.SelectedFinish!
                                        : finishes.FirstOrDefault()!;
            string chosenCondition = isEdit
                                        ? selectedCard.SelectedCondition!
                                        : "Near Mint";
            string language = isEdit
                                        ? selectedCard.Language!
                                        : (selectedCard.Language ?? "English");
            int ownedCount = isEdit
                                        ? selectedCard.CardsOwned
                                        : 1;
            int tradeCount = isEdit
                                        ? selectedCard.CardsForTrade
                                        : 0;
            int? cardId = isEdit
                                        ? selectedCard.CardId
                                        : null;

            // 3) Clone everything else verbatim
            var clone = new CardSet
            {
                // --- common / view fields ---
                Name = selectedCard.Name,
                ManaCostRaw = selectedCard.ManaCostRaw,
                ManaValue = selectedCard.ManaValue,
                Colors = selectedCard.Colors,
                Type = selectedCard.Type,
                ManaCostImageBytes = selectedCard.ManaCostImageBytes,

                Types = selectedCard.Types,
                SuperTypes = selectedCard.SuperTypes,
                SubTypes = selectedCard.SubTypes,
                Keywords = selectedCard.Keywords,
                Text = selectedCard.Text,
                Side = selectedCard.Side,

                Language = language,
                Uuid = selectedCard.Uuid,
                SetName = selectedCard.SetName,
                Rarity = selectedCard.Rarity,
                Finishes = selectedCard.Finishes,
                ReleaseDate = selectedCard.ReleaseDate,
                KeyRuneImageBytes = selectedCard.KeyRuneImageBytes,
                CardInCollectionPrice = selectedCard.CardInCollectionPrice,

                // --- collection-specific fields ---
                CardId = cardId,
                CardsOwned = ownedCount,
                CardsForTrade = tradeCount,
                SelectedCondition = chosenCondition,
                SelectedFinish = chosenFinish,

                AvailableFinishes = finishes,
                OtherLanguages = languages
            };

            return clone;
        }


        // Prepare a new card directly for submission to db with defaults (taking into account non-English or non-nonfoil cards)
        public async Task<CardSet> PrepareNewCardWithDefaultsAsync(CardSet selectedCard)
        {
            if (selectedCard.Uuid == null)
            {
                throw new ArgumentException("UUID is required", nameof(selectedCard));
            }

            // 1) grab all finishes / languages
            var finishes = await _repo.FetchFinishesForCardAsync(selectedCard.Uuid);
            var languages = await _repo.FetchLanguagesForCardAsync(selectedCard.Uuid);

            // 2) pick “nonfoil” if available, else first; same for English
            string chosenFinish = finishes
                .FirstOrDefault(f => f.Equals("nonfoil", StringComparison.OrdinalIgnoreCase))
                ?? finishes.FirstOrDefault()
                ?? "nonfoil";

            string chosenLanguage = languages
                .FirstOrDefault(l => l.Equals("English", StringComparison.OrdinalIgnoreCase))
                ?? languages.FirstOrDefault()
                ?? "English";

            // 3) build CardSet
            return new CardSet
            {
                Uuid = selectedCard.Uuid,
                Name = selectedCard.Name,
                SelectedFinish = chosenFinish,
                SelectedCondition = "Near Mint",
                Language = chosenLanguage,
                CardsOwned = 1,
                CardsForTrade = 0
            };
        }

        // Save a card and return the changes to viewmodel
        public async Task<IReadOnlyList<CardChangeEventArgs>> SaveBatchAsync(IEnumerable<CardSet> raws, bool isEdit)
        {
            var changes = new List<CardChangeEventArgs>();

            foreach (var raw in raws)
            {
                var change = await SaveAndReturnChangesAsync(raw, isEdit);
                changes.Add(change);
            }

            return changes;
        }
        public async Task<CardChangeEventArgs> SaveAndReturnChangesAsync(CardSet raw, bool isEdit)
        {
            // 1) Deletion-by-zero?
            if (isEdit && raw.CardsOwned == 0)
            {
                // delete in DB
                await _repo.DeleteCardByIdAsync(raw);

                // make sure we have an ID
                var deletedId = raw.CardId
                    ?? throw new InvalidOperationException("Cannot delete a card without an ID");

                // safe to use .Value now
                return new CardChangeEventArgs(deletedId);
            }

            // 2) Upsert path
            // 2a) Do we already have a DB row?
            var existingId = await _repo.FindExistingCardReturnIdAsync(raw);
            if (existingId.HasValue)
            {
                // update counts
                raw.CardId = existingId.Value;
                await _repo.UpdateCardCountsAsync(raw);
            }
            else
            {
                // new insert
                await _repo.AddCardAsync(raw);
                // then fetch the newly‐inserted id
                raw.CardId = (await _repo.FindExistingCardReturnIdAsync(raw))!.Value;
            }

            // 3) Deduplicate *and* compute new sums
            var allIds = await _repo.FindRecordByIdAsync(raw.Uuid!, raw.SelectedCondition!, raw.Language!, raw.SelectedFinish!);

            // if no dupes → just one survivor, no sums or removals
            int keepId = raw.CardId.Value;
            int sumOwned = raw.CardsOwned;
            int sumTrade = raw.CardsForTrade;
            int[] removedIds = Array.Empty<int>();

            if (allIds.Count > 1)
            {
                // pick lowest‐PK as “keeper”
                allIds.Sort();
                keepId = allIds[0];
                removedIds = [.. allIds.Skip(1)];

                // get the total sums in one shot
                (sumOwned, sumTrade) = await _repo.GetTotalsAsync(
                    raw.Uuid!,
                    raw.SelectedCondition!,
                    raw.Language!,
                    raw.SelectedFinish!);

                // merge in DB
                await _repo.MergeDuplicateRecordsAsync(
                    raw.Uuid!,
                    raw.SelectedCondition!,
                    raw.Language!,
                    raw.SelectedFinish!,
                    keepId);
            }

            // 4) Build the final in‐memory survivor
            raw.CardId = keepId;
            raw.CardsOwned = sumOwned;
            raw.CardsForTrade = sumTrade;

            // 5) Return upsert event
            return new CardChangeEventArgs(raw, removedIds);
        }

        // Persist the single incoming CardSet (insert / update / delete)
        //private async Task PersistAsync(CardSet card, bool isEdit)
        //{
        //    if (isEdit && card.CardsOwned == 0)
        //    {
        //        await _repo.DeleteCardByIdAsync(card);
        //    }
        //    else if (isEdit)
        //    {
        //        await _repo.UpdateCardAsync(card);
        //    }
        //    else
        //    {
        //        var existingId = await _repo.FindExistingCardReturnIdAsync(card);
        //        if (existingId.HasValue)
        //        {
        //            card.CardId = existingId.Value;
        //            await _repo.UpdateCardCountsAsync(card);
        //        }
        //        else
        //        {
        //            await _repo.AddCardAsync(card);
        //        }
        //    }
        //}

        // Fetch all record-IDs sharing the same business key
        //private Task<List<int>> FetchMatchingIdsAsync(CardSet card) => _repo.FindRecordByIdAsync(card.Uuid!, card.SelectedCondition!, card.Language!, card.SelectedFinish!);

        // If there are duplicates, merge sums in DB and return the IDs we deleted
        //private async Task<(int keepId, int sumOwned, int sumTrade, int[] removed)> MergeDuplicatesIfNeededAsync(CardSet card, List<int> allIds)
        //{
        //    if (allIds.Count <= 1)
        //    {
        //        // no merge needed:
        //        var id = allIds.Count == 1 ? allIds[0] : card.CardId!.Value;
        //        return (id, card.CardsOwned, card.CardsForTrade, Array.Empty<int>());
        //    }

        //    allIds.Sort();
        //    var keepId = allIds[0];
        //    var removed = allIds.Skip(1).ToArray();

        //    // repo call returns the new sums:
        //    var (sumOwned, sumTrade) = await _repo.MergeDuplicateRecordsAsync(card.Uuid!, card.SelectedCondition!, card.Language!, card.SelectedFinish!, keepId);

        //    return (keepId, sumOwned, sumTrade, removed);
        //}

        private static string DumpCardSet(CardSet c)
        {
            return $@"
            CardId:              {c.CardId}
            Name:                {c.Name}
            ManaCostRaw:         {c.ManaCostRaw}
            ManaValue:           {c.ManaValue}
            Colors:              {c.Colors}
            Type:                {c.Type}
            ManaCostImageBytes:  {(c.ManaCostImageBytes?.Length.ToString() ?? "null")}
            ---------------- Common end
            Types:               {c.Types}
            SuperTypes:          {c.SuperTypes}
            SubTypes:            {c.SubTypes}
            Keywords:            {c.Keywords}
            Text (RulesText):    {c.Text}
            Side:                {c.Side}
            Language:            {c.Language}
            Uuid:                {c.Uuid}
            SetName:             {c.SetName}
            Rarity:              {c.Rarity}
            Finishes:            {c.Finishes}
            ReleaseDate:         {c.ReleaseDate:yyyy-MM-dd}
            KeyRuneImageBytes:   {(c.KeyRuneImageBytes?.Length.ToString() ?? "null")}
            ---------------- MyCollection
            CardsOwned:          {c.CardsOwned}
            CardsForTrade:       {c.CardsForTrade}
            SelectedCondition:   {c.SelectedCondition}
            SelectedFinish:      {c.SelectedFinish}
            CardInCollectionPrice:{c.CardInCollectionPrice:C}
            ".Replace("\r\n", "\n");  // normalize line endings
        }
    }
}
