using CollectaMundo.DomainLogic.CardLocations.Models;

namespace CollectaMundo.ApplicationServices.CardLocations
{
    public interface ICardLocationLookupStore
    {
        IReadOnlyList<CardLocation> GetAll();

        void ReplaceAll(IReadOnlyList<CardLocation> locations);
        void Upsert(CardLocation location);
        void UpsertMany(IReadOnlyList<CardLocation> locations);
        void RemoveMany(IReadOnlyList<int> ids);

        event EventHandler? LocationsChanged;
    }
}
