using CollectaMundo.DomainLogic.CardLocations.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CollectaMundo.ViewModels.Pages.SharedElements
{
    public sealed class LocationBindingViewModel(IReadOnlyList<CardLocation> items, Func<int?> getSelectedLocationId, Action<int?> setSelectedLocationId, ICommand refreshCommand) : ObservableObject
    {
        public IReadOnlyList<CardLocation> Items { get; } = items;
        public ICommand RefreshCommand { get; } = refreshCommand;
        public string DisplayMemberPath => nameof(CardLocation.DisplayName); // should not be static, used for wpf binding

        private readonly Func<int?> _getSelectedLocationId = getSelectedLocationId;
        private readonly Action<int?> _setSelectedLocationId = setSelectedLocationId;

        public CardLocation? Selected
        {
            get => _getSelectedLocationId() is int id
                ? Items.FirstOrDefault(x => x.Id == id)
                : null;
            set
            {
                var newId = value?.Id;
                if (_getSelectedLocationId() == newId)
                {
                    return;
                }

                _setSelectedLocationId(newId);
                OnPropertyChanged();
            }
        }

        public void NotifySelectionChanged()
        {
            OnPropertyChanged(nameof(Selected));
        }
    }
}
