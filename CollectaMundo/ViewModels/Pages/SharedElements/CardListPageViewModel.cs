using CollectaMundo.ViewModels.Filtering;
using CollectaMundo.ViewModels.SideMenuRight;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Windows.Input;

namespace CollectaMundo.ViewModels.Pages.SharedElements
{
    public abstract class CardListPageViewModel : ObservableObject, IClearPageStatus
    {
        // Child VMs passed down from MainWindowViewModel
        public CardListViewModel CardsVM { get; }
        public CardImageViewModel CardImageVM { get; }
        public FilterViewModel FilterVM { get; }
        public ModifyCollectionViewModel? ModifyCollectionViewModel { get; }
        public PricesViewModel? PricesVM { get; }


        // Bindable pass-through properties for the filters 
        public FilterItemViewModel? NameFilter => FilterVM.Filters.TryGetValue("Name", out var f) ? f : null;
        public FilterItemViewModel? SetNameFilter => FilterVM.Filters.TryGetValue("SetName", out var f) ? f : null;


        public CardListEditPanelKind EditPanelKind { get; }
        public bool IsSearchEditPanelVisible => EditPanelKind == CardListEditPanelKind.SearchAndFilter;
        public bool IsMyCollectionEditPanelVisible => EditPanelKind == CardListEditPanelKind.MyCollection;


        // Bindable chrome-facing properties
        public string PageTitle { get; }
        public string PrimarySubmitButtonText { get; }

        public bool ShowCounts => ModifyCollectionViewModel?.ShowCounts ?? true;
        public bool HasStatus => ModifyCollectionViewModel?.HasStatus ?? false;
        public bool IsCollectionEditVisible => ModifyCollectionViewModel?.IsCollectionEditVisible ?? false;
        public string StatusMessage => ModifyCollectionViewModel?.StatusMessage ?? string.Empty;
        public ICommand? PrimarySubmitCommand { get; }
        public ICommand? ClearPendingChangesCommand => ModifyCollectionViewModel?.ClearCardsToAddCommand;

        public CardListPageViewModel(CardListViewModel cardsVM, CardImageViewModel cardImageVM, FilterViewModel filterVM, string pageTitle, CardListEditPanelKind editPanelKind, string primarySubmitButtonText, ICommand? primarySubmitCommand = null, PricesViewModel? pricesVM = null, ModifyCollectionViewModel? modifyCollectionVM = null)
        {
            CardsVM = cardsVM;
            CardImageVM = cardImageVM;
            FilterVM = filterVM;
            PricesVM = pricesVM;
            ModifyCollectionViewModel = modifyCollectionVM;

            PageTitle = pageTitle;
            EditPanelKind = editPanelKind;
            PrimarySubmitButtonText = primarySubmitButtonText;
            PrimarySubmitCommand = primarySubmitCommand;

            FilterVM.FiltersRebuilt += (_, _) =>
            {
                OnPropertyChanged(nameof(NameFilter));
                OnPropertyChanged(nameof(SetNameFilter));
            };

            if (ModifyCollectionViewModel is not null)
            {
                ModifyCollectionViewModel.PropertyChanged += ModifyCollectionViewModel_PropertyChanged;
            }
        }
        public virtual void ClearPageStatus()
        {
            ModifyCollectionViewModel?.ClearStatus();
        }
        private void ModifyCollectionViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ModifyCollectionViewModel.ShowCounts)
                or nameof(ModifyCollectionViewModel.HasStatus)
                or nameof(ModifyCollectionViewModel.IsCollectionEditVisible)
                or nameof(ModifyCollectionViewModel.StatusMessage))
            {
                OnPropertyChanged(nameof(ShowCounts));
                OnPropertyChanged(nameof(HasStatus));
                OnPropertyChanged(nameof(IsCollectionEditVisible));
                OnPropertyChanged(nameof(StatusMessage));
            }
        }
    }

    public enum CardListEditPanelKind
    {
        SearchAndFilter,
        MyCollection
    }
}
