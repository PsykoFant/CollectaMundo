using CollectaMundo.DomainLogic.CardLocations.Models;

namespace CollectaMundo.ViewModels.ModifyCollection
{
    public sealed class LocationMenuItemViewModel
    {
        public required string Header { get; init; }
        public int? LocationId { get; init; }
        public IReadOnlyList<LocationMenuItemViewModel> Children { get; init; } = [];

        public static LocationMenuItemViewModel FromLocation(CardLocation location)
        {
            return new LocationMenuItemViewModel
            {
                Header = location.Name,
                LocationId = location.Id
            };
        }
    }
}
