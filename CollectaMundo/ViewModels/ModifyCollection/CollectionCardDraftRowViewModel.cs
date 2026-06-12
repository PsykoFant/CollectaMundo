using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.CollectionMutations.Models;
using CollectaMundo.ViewModels.ModifyCollection.BindinViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace CollectaMundo.ViewModels.ModifyCollection;

public sealed partial class CollectionCardDraftRowViewModel : ObservableObject
{
    public CollectionCardDraft CardToAddOrEdit { get; }
    public bool CanSplit => CardsOwned > 1;

    // Bindable properties for the combo boxes
    public ComboBindingViewModel ConditionCombo { get; }
    public ComboBindingViewModel FinishCombo { get; }
    public ComboBindingViewModel LanguageCombo { get; }
    public LocationBindingViewModel LocationCombo { get; }

    // Bindable properties for the numeric inputs
    public NumericBindingViewModel Owned { get; }
    public NumericBindingViewModel Trade { get; }
    public CollectionCardDraftRowViewModel(CollectionCardDraft cardToAdd, IReadOnlyList<CardLocation> availableLocations, ICommand refreshColumnsCommand)
    {
        CardToAddOrEdit = cardToAdd;

        ConditionCombo = new ComboBindingViewModel(
            items: cardToAdd.Conditions,
            getter: () => SelectedCondition,
            setter: v => SelectedCondition = (string?)v,
            refreshCommand: refreshColumnsCommand);

        FinishCombo = new ComboBindingViewModel(
            items: cardToAdd.FinishOptions,
            getter: () => SelectedFinish,
            setter: v => SelectedFinish = (string?)v,
            refreshCommand: refreshColumnsCommand);

        LanguageCombo = new ComboBindingViewModel(
            items: cardToAdd.OtherLanguages,
            getter: () => Language,
            setter: v => Language = (string?)v,
            refreshCommand: refreshColumnsCommand);

        LocationCombo = new LocationBindingViewModel(
            items: availableLocations,
            getSelectedLocationId: () => SelectedLocationId,
            setSelectedLocationId: v => SelectedLocationId = v,
            refreshCommand: refreshColumnsCommand);

        Owned = new NumericBindingViewModel(
            getter: () => CardsOwned,
            setter: v => CardsOwned = v,
            changedCommand: refreshColumnsCommand,
            min: 0,
            delayMs: 500);

        Trade = new NumericBindingViewModel(
            getter: () => CardsForTrade,
            setter: v => CardsForTrade = v,
            changedCommand: refreshColumnsCommand,
            min: 0,
            maxGetter: () => CardsOwned,
            delayMs: 0);
    }
    public string? Name => CardToAddOrEdit.Name;
    public string? SetName => CardToAddOrEdit.SetName;

    public string? SelectedCondition
    {
        get => CardToAddOrEdit.SelectedCondition;
        set => SetModelValue(CardToAddOrEdit.SelectedCondition, value, v => CardToAddOrEdit.SelectedCondition = v);
    }
    public string? SelectedFinish
    {
        get => CardToAddOrEdit.SelectedFinish;
        set => SetModelValue(CardToAddOrEdit.SelectedFinish, value, v => CardToAddOrEdit.SelectedFinish = v);
    }
    public string? Language
    {
        get => CardToAddOrEdit.Language;
        set => SetModelValue(CardToAddOrEdit.Language, value, v => CardToAddOrEdit.Language = v);
    }
    public int? SelectedLocationId
    {
        get => CardToAddOrEdit.SelectedLocationId;
        set
        {
            if (CardToAddOrEdit.SelectedLocationId == value)
            {
                return;
            }

            CardToAddOrEdit.SelectedLocationId = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedLocationName));
            OnPropertyChanged(nameof(SelectedLocationType));
        }
    }
    public string? SelectedLocationName => CardToAddOrEdit.SelectedLocationDisplayName;
    public CardLocationType? SelectedLocationType => CardToAddOrEdit.SelectedLocationType;
    public string? Comment
    {
        get => CardToAddOrEdit.Comment;
        set => SetModelValue(CardToAddOrEdit.Comment, value, v => CardToAddOrEdit.Comment = v);
    }
    public int CardsOwned
    {
        get => CardToAddOrEdit.CardsOwned;
        set
        {
            if (!SetModelValue(CardToAddOrEdit.CardsOwned, value, v => CardToAddOrEdit.CardsOwned = v))
            {
                return;
            }

            Owned.NotifyValueChanged();
            OnPropertyChanged(nameof(CanSplit));

            if (CardsForTrade > CardsOwned)
            {
                CardsForTrade = CardsOwned;
            }
        }
    }
    public int CardsForTrade
    {
        get => CardToAddOrEdit.CardsForTrade;
        set
        {
            if (!SetModelValue(CardToAddOrEdit.CardsForTrade, value, v => CardToAddOrEdit.CardsForTrade = v))
            {
                return;
            }

            Trade.NotifyValueChanged();
        }
    }

    public void UpdateAvailableLocations(IReadOnlyList<CardLocation> availableLocations)
    {
        LocationCombo.ReplaceItems(availableLocations);

        if (SelectedLocationId is int selectedId && availableLocations.All(x => x.Id != selectedId))
        {
            SelectedLocationId = null;
        }
    }

    // Helpers
    private bool SetModelValue<T>(T currentValue, T newValue, Action<T> assign, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(currentValue, newValue))
        {
            return false;
        }

        assign(newValue);
        OnPropertyChanged(propertyName);
        return true;
    }
}
