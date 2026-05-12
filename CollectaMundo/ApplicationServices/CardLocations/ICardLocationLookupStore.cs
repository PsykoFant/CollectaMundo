using CollectaMundo.DomainLogic.CardLocations.Models;

namespace CollectaMundo.ApplicationServices.CardLocations
{
    public interface ICardLocationLookupStore
    {
        IReadOnlyList<CardLocation> GetAll();
        CardLocation? Get(int id);

        void ReplaceAll(IReadOnlyList<CardLocation> locations);
        void Upsert(CardLocation location);
        void UpsertMany(IReadOnlyList<CardLocation> locations);
        bool Remove(int id);

        event EventHandler? LocationsChanged;
    }
}
