using CollectaMundo.ApplicationServices.EditCollection.Models;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.DomainLogic.Shared;

namespace CollectaMundo.DomainLogic.ModifyCollection
{
    public class ModifyCollectionLogic() : IModifyCollectionLogic
    {
        private static readonly string _defaultLanguage = CollectionCardItemDefaults.GetDefaultString(ImportField.Language);
        public CardSet PrepareCardForList(CardSet selectedCard, CardToAddMetadataDto metadata, bool isEdit)
        {
            if (selectedCard.Core is null)
            {
                throw new InvalidOperationException("CardSet.Core must be set. Use CardSet.FromCore.");
            }

            var clone = CardSet.FromCore(selectedCard.Core);

            // Carry over view-only fields from the source row
            clone.SelectedFinish = selectedCard.SelectedFinish;
            clone.SelectedCondition = selectedCard.SelectedCondition;
            clone.Count = selectedCard.Count;

            // Attach selectable metadata for the editor
            clone.AvailableFinishes = [.. metadata.AvailableFinishes];
            clone.OtherLanguages = NormalizeLanguages(metadata.AvailableLanguages, selectedCard.Language);

            if (isEdit)
            {
                // Preserve the full collection row state when editing
                clone.CardId = selectedCard.CardId;
                clone.CardsOwned = selectedCard.CardsOwned;
                clone.CardsForTrade = selectedCard.CardsForTrade;
                clone.Language = selectedCard.Language!;
                clone.SelectedFinish = selectedCard.SelectedFinish!;
                clone.SelectedCondition = selectedCard.SelectedCondition!;
                clone.SelectedLocationId = selectedCard.SelectedLocationId;
                clone.Comment = selectedCard.Comment;
            }
            else
            {
                // New rows start from collection defaults
                ApplyNewDefaults(clone);
            }

            clone.RecomputeCollectionPrice();
            return clone;
        }
        public CardSet PrepareNewCardWithDefaults(CardSet selectedCard, CardToAddMetadataDto metadata)
        {
            if (selectedCard.Core is null)
            {
                throw new InvalidOperationException("CardSet.Core must be set. Use CardSet.FromCore.");
            }

            var clone = CardSet.FromCore(selectedCard.Core);

            // Copy metadata lists so the edit row owns its own selections
            clone.AvailableFinishes = metadata.AvailableFinishes.ToList();
            clone.OtherLanguages = NormalizeLanguages(metadata.AvailableLanguages, selectedCard.Language);

            ApplyNewDefaults(clone);

            clone.RecomputeCollectionPrice();
            return clone;
        }

        // Helper methods for PrepareCardForList
        private static void ApplyNewDefaults(CardSet clone)
        {
            clone.CardId = null;
            clone.CardsOwned = CollectionCardItemDefaults.GetDefaultInt(ImportField.CardsOwned);
            clone.CardsForTrade = CollectionCardItemDefaults.GetDefaultInt(ImportField.CardsForTrade);
            clone.SelectedCondition = CollectionCardItemDefaults.GetDefaultString(ImportField.Condition);
            clone.SelectedFinish = ChooseDefaultFinish(clone.AvailableFinishes);
            clone.SelectedLocationId = null;
            clone.Comment = null;
            clone.Language = ChooseDefaultLanguage(clone.OtherLanguages);
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

            return finishes
                .OrderBy(Rank)
                .ThenBy(s => s, StringComparer.OrdinalIgnoreCase)
                .First();
        }
        private static string ChooseDefaultLanguage(List<string>? langs)
        {
            if (langs == null || langs.Count == 0)
            {
                return _defaultLanguage;
            }

            var english = langs.FirstOrDefault(l =>
                l.Equals(_defaultLanguage, StringComparison.OrdinalIgnoreCase));

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
