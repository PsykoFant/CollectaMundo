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
        public CardLocation? Get(int id)
        {
            return _byId.GetValueOrDefault(id);
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
        public bool Remove(int id)
        {
            var removed = _byId.Remove(id);

            if (removed)
            {
                LocationsChanged?.Invoke(this, EventArgs.Empty);
            }

            return removed;
        }
    }
}
