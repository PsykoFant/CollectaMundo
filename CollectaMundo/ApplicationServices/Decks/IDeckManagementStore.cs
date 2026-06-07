using CollectaMundo.ApplicationServices.CardLocations.Models;
using CollectaMundo.ApplicationServices.Decks.Models;
using System.Collections.ObjectModel;

namespace CollectaMundo.ApplicationServices.Decks
{
    public interface IDeckManagementStore
    {
        ObservableCollection<DeckManagementRecord> Decks { get; }
        ObservableCollection<DeckFormatOption> DeckFormats { get; }
        Task LoadAsync();
        void Upsert(DeckManagementRecord deck);
        void Remove(int locationId);
    }
}
