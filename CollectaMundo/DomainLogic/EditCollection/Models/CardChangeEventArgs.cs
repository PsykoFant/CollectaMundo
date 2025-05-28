using CollectaMundo.DomainLogic.CardLists.Models;

namespace CollectaMundo.DomainLogic.EditCollection.Models
{
    public class CardChangeEventArgs : EventArgs
    {
        public enum ChangeType { Upsert, Delete }

        /// <summary>Whether we just upserted (add/update/merge) or deleted by zero.</summary>
        public ChangeType Type { get; }

        /// <summary>The one true survivor after add/update/merge. Null if Type==Delete.</summary>
        public CardSet? Survivor { get; }

        /// <summary>
        /// The CardId(s) that should be removed from any in-memory list.
        /// If you deleted by zero, this is the single CardId that was deleted.
        /// If you merged duplicates, these are the extra IDs you collapsed.
        /// </summary>
        public IReadOnlyList<int> Removed { get; }

        // Upsert constructor
        public CardChangeEventArgs(CardSet survivor, IReadOnlyList<int>? removed = null)
        {
            Type = ChangeType.Upsert;
            Survivor = survivor;
            Removed = removed ?? [];
        }

        // Delete-by-zero constructor
        public CardChangeEventArgs(int deletedCardId)
        {
            Type = ChangeType.Delete;
            Survivor = null;
            Removed = [deletedCardId];
        }
    }
}
