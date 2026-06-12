using CollectaMundo.ApplicationServices.EditCollection.Models;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CollectionMutations.Models;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.DomainLogic.ModifyCollection
{
    public class ModifyCollectionLogic() : IModifyCollectionLogic
    {
        private static readonly string _defaultLanguage = CollectionCardItemDefaults.GetDefaultString(ImportField.Language);
        public CollectionCardDraft PrepareCardForList(PrintingCard printing, CollectionCard? existingCollectionCard, CardToAddMetadataDto metadata, bool isEdit)
        {
            var draft = new CollectionCardDraft
            {
                Uuid = printing.Uuid,
                Name = printing.Name,
                SetName = printing.SetName,

                FinishOptions = [.. metadata.AvailableFinishes],
                OtherLanguages = NormalizeLanguages(metadata.AvailableLanguages, existingCollectionCard?.Language ?? printing.Language)
            };

            if (isEdit)
            {
                if (existingCollectionCard is null)
                {
                    throw new InvalidOperationException("Editing requires an existing collection card.");
                }

                draft.CardId = existingCollectionCard.CardId;
                draft.CardsOwned = existingCollectionCard.CardsOwned;
                draft.CardsForTrade = existingCollectionCard.CardsForTrade;
                draft.Language = existingCollectionCard.Language;
                draft.SelectedFinish = existingCollectionCard.SelectedFinish;
                draft.SelectedCondition = existingCollectionCard.SelectedCondition;
                draft.SelectedLocationId = existingCollectionCard.SelectedLocationId;
                draft.Comment = existingCollectionCard.Comment;

                return draft;
            }

            ApplyNewDefaults(draft, printing);

            return draft;
        }
        public CollectionCardDraft PrepareNewCardWithDefaults(PrintingCard selectedCard, CardToAddMetadataDto metadata)
        {
            var draft = new CollectionCardDraft
            {
                Uuid = selectedCard.Uuid,
                Name = selectedCard.Name,
                SetName = selectedCard.SetName,

                FinishOptions = [.. metadata.AvailableFinishes],
                OtherLanguages = NormalizeLanguages(
                    metadata.AvailableLanguages,
                    selectedCard.Language)
            };

            ApplyNewDefaults(draft, selectedCard);

            return draft;
        }
        // Helper methods for PrepareCardForList
        private static void ApplyNewDefaults(CollectionCardDraft draft, PrintingCard printing)
        {
            draft.CardsOwned = 1;
            draft.CardsForTrade = 0;

            draft.SelectedCondition = "Near Mint";
            draft.SelectedFinish =
                ChooseDefaultFinish([.. draft.FinishOptions])
                ?? "nonfoil";

            draft.Language =
                ChooseDefaultLanguage([.. draft.OtherLanguages]);

            draft.SelectedLocationId = null;
            draft.Comment = null;
        }
        private static string? ChooseDefaultFinish(List<string>? finishes)
        {
            if (finishes == null || finishes.Count == 0)
            {
                return null;
            }

            static int Rank(string s) => s switch
            {
                var x when x.Equals("nonfoil", StringComparison.OrdinalIgnoreCase) => 0,
                var x when x.Equals("foil", StringComparison.OrdinalIgnoreCase) => 1,
                var x when x.Equals("etched", StringComparison.OrdinalIgnoreCase) => 2,
                _ => 3
            };

            return finishes.OrderBy(Rank).ThenBy(s => s, StringComparer.OrdinalIgnoreCase).First();
        }
        private static string ChooseDefaultLanguage(List<string>? langs)
        {
            if (langs == null || langs.Count == 0)
            {
                return _defaultLanguage;
            }

            var english = langs.FirstOrDefault(l => l.Equals(_defaultLanguage, StringComparison.OrdinalIgnoreCase));

            return english ?? langs[0];
        }
        private static List<string> NormalizeLanguages(IEnumerable<string>? langs, string? primary)
        {
            var list = (langs ?? [])
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Include the current language if it is not already present
            if (!string.IsNullOrWhiteSpace(primary) &&
                !list.Contains(primary, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(primary);
            }

            // Prefer English first, then the current language, then alphabetical
            list.Sort(StringComparer.OrdinalIgnoreCase);
            MoveToFront(list, _defaultLanguage);

            if (!string.Equals(primary, _defaultLanguage, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(primary))
            {
                MoveToFront(list, primary);
            }

            return list;
        }
        private static void MoveToFront(List<string> list, string value)
        {
            var idx = list.FindIndex(s => string.Equals(s, value, StringComparison.OrdinalIgnoreCase));
            if (idx > 0)
            {
                var item = list[idx];
                list.RemoveAt(idx);
                list.Insert(0, item);
            }
        }
    }
}
