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
        private IReadOnlyList<CardLocation> _items = items;
        private readonly Func<int?> _getSelectedLocationId = getSelectedLocationId;
        private readonly Action<int?> _setSelectedLocationId = setSelectedLocationId;
        private bool _isReplacingItems;
        public IReadOnlyList<CardLocation> Items
        {
            get => _items;
            private set
            {
                if (ReferenceEquals(_items, value))
                {
                    return;
                }

                _items = value;
                OnPropertyChanged();
            }
        }
        public ICommand RefreshCommand { get; } = refreshCommand;
        public string DisplayMemberPath => nameof(CardLocation.DisplayName);
        public int? SelectedLocationId
        {
            get => _getSelectedLocationId();
            set
            {
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
                Items = items;
                OnPropertyChanged(nameof(SelectedLocationId));
            }
            finally
            {
                _isReplacingItems = false;
            }
        }
    }
}
