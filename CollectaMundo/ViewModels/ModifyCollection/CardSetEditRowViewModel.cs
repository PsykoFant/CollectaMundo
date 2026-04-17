using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.ViewModels.Pages.SharedElements;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace CollectaMundo.ViewModels.ModifyCollection;

public sealed partial class CardSetEditRowViewModel : ObservableObject
{
    public CardSet CardToAddOrEdit { get; }

    // Bindable properties for the combo boxes
    public ComboBindingViewModel ConditionCombo { get; }
    public ComboBindingViewModel FinishCombo { get; }
    public ComboBindingViewModel LanguageCombo { get; }

    // Global location choices for direct ComboBox binding
    public IReadOnlyList<CardLocation> AvailableLocations { get; }

    // Bindable properties for the numeric inputs
    public NumericBindingViewModel Owned { get; }
    public NumericBindingViewModel Trade { get; }

    public CardSetEditRowViewModel(CardSet cardToAdd, IReadOnlyList<CardLocation> availableLocations, ICommand refreshColumnsCommand)
    {
        CardToAddOrEdit = cardToAdd;
        AvailableLocations = availableLocations;
        RefreshColumnsCommand = refreshColumnsCommand;

        ConditionCombo = new ComboBindingViewModel(
            items: cardToAdd.Conditions,
            getter: () => SelectedCondition,
            setter: v => SelectedCondition = (string?)v,
            refreshCommand: refreshColumnsCommand);

        FinishCombo = new ComboBindingViewModel(
            items: cardToAdd.AvailableFinishes,
            getter: () => SelectedFinish,
            setter: v => SelectedFinish = (string?)v,
            refreshCommand: refreshColumnsCommand);

        LanguageCombo = new ComboBindingViewModel(
            items: cardToAdd.OtherLanguages,
            getter: () => Language,
            setter: v => Language = (string?)v,
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
            if (!SetModelValue(CardToAddOrEdit.SelectedLocationId, value, v => CardToAddOrEdit.SelectedLocationId = v))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedLocationName));
            OnPropertyChanged(nameof(SelectedLocationType));

            // Keep layout refresh behavior aligned with the other editors
            if (RefreshColumnsCommand.CanExecute(null))
            {
                RefreshColumnsCommand.Execute(null);
            }
        }
    }
    public string? SelectedLocationName => CardToAddOrEdit.SelectedLocationName;
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

    // Command to trigger column refresh 
    private ICommand RefreshColumnsCommand { get; }

    // Helpers
    private bool SetModelValue<T>(
    T currentValue,
    T newValue,
    Action<T> assign,
    [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
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
