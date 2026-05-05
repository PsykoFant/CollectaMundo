using CollectaMundo.DomainLogic.CardLocations.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    public sealed record SetLocationForSelectedCardsParameter(System.Collections.IEnumerable SelectedItems, int? LocationId);
}
