using CollectaMundo.DomainLogic.CardLocations.Models;

namespace CollectaMundo.ApplicationServices.CardLocations
{
    public sealed class CardLocationLookupStore : ICardLocationLookupStore
    {
        private readonly Dictionary<int, CardLocation> _byId = [];

        public event EventHandler? LocationsChanged;

        public IReadOnlyList<CardLocation> GetAll()
        {
            return
            [
                .. _byId.Values
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            ];
        }
        public void ReplaceAll(IReadOnlyList<CardLocation> locations)
        {
            _byId.Clear();

            foreach (var location in locations)
            {
                _byId[location.Id] = location;
            }

            LocationsChanged?.Invoke(this, EventArgs.Empty);
        }
        public void Upsert(CardLocation location)
        {
            _byId[location.Id] = location;
            LocationsChanged?.Invoke(this, EventArgs.Empty);
        }
        public void UpsertMany(IReadOnlyList<CardLocation> locations)
        {
            foreach (var location in locations)
            {
                _byId[location.Id] = location;
            }

            LocationsChanged?.Invoke(this, EventArgs.Empty);
        }
        public void RemoveMany(IReadOnlyList<int> ids)
        {
            bool changed = false;

            foreach (int id in ids)
            {
                changed |= _byId.Remove(id);
            }

            if (changed)
            {
                LocationsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
