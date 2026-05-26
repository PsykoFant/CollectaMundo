using CollectaMundo.DomainLogic.Decks.Models;
using System.Collections.ObjectModel;

namespace CollectaMundo.ApplicationServices.Decks
{
    public interface IDeckManagementStore
    {
        ObservableCollection<DeckManagementRecord> Decks { get; }
        Task LoadAsync();
        void Upsert(DeckManagementRecord deck);
        void Remove(int locationId);
    }
}
