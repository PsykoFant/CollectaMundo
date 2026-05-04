using CollectaMundo.DomainLogic.CardLocations.Models;

namespace CollectaMundo.ViewModels.ModifyCollection.BindinViewModels
{
    public sealed class LocationOption
    {
        public int? Id { get; init; }
        public string DisplayName { get; init; } = string.Empty;

        public static LocationOption None { get; } = new()
        {
            Id = null,
            DisplayName = string.Empty
        };

        public static LocationOption From(CardLocation location)
        {
            return new LocationOption
            {
                Id = location.Id,
                DisplayName = location.DisplayName
            };
        }
    }
}
