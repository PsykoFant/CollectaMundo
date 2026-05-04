using CollectaMundo.DomainLogic.CardLocations.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CollectaMundo.ViewModels.ModifyCollection.BindinViewModels
{
    public sealed class LocationBindingViewModel(IReadOnlyList<CardLocation> items, Func<int?> getSelectedLocationId, Action<int?> setSelectedLocationId, ICommand refreshCommand) : ObservableObject
    {
        private IReadOnlyList<LocationOption> _items = BuildOptions(items);
        private readonly Func<int?> _getSelectedLocationId = getSelectedLocationId;
        private readonly Action<int?> _setSelectedLocationId = setSelectedLocationId;
        private bool _isReplacingItems;
        public IReadOnlyList<LocationOption> Items => _items; // Bound to xaml ComboBox
        public ICommand RefreshCommand { get; } = refreshCommand;
        public int? SelectedLocationId
        {
            get => _getSelectedLocationId();
            set
            {
                // During ItemsSource replacement, WPF may briefly push null.
                // Ignore that transient write unless null is explicitly selected by the user.
                if (_isReplacingItems && value is null)
                {
                    return;
                }

                if (_getSelectedLocationId() == value)
                {
                    return;
                }

                _setSelectedLocationId(value);
                OnPropertyChanged();
            }
        }
        public void ReplaceItems(IReadOnlyList<CardLocation> items)
        {
            _isReplacingItems = true;

            try
            {
                _items = BuildOptions(items);
                OnPropertyChanged(nameof(Items));
                OnPropertyChanged(nameof(SelectedLocationId));
            }
            finally
            {
                _isReplacingItems = false;
            }
        }
        private static IReadOnlyList<LocationOption> BuildOptions(IReadOnlyList<CardLocation> locations)
        {
            return [LocationOption.None, .. locations.Select(LocationOption.From)];
        }
    }
}
