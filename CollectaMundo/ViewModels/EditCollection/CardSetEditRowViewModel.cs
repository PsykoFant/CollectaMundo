using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.ViewModels.Pages.SharedElements;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace CollectaMundo.ViewModels.EditCollection;

public sealed partial class CardSetEditRowViewModel : ObservableObject
{
    public CardSet CardToAddOrEdit { get; }

    // Bindable properties for the combo boxes
    public ComboBindingViewModel ConditionCombo { get; }
    public ComboBindingViewModel FinishCombo { get; }
    public ComboBindingViewModel LanguageCombo { get; }
    public NumericBindingViewModel Owned { get; }
    public NumericBindingViewModel Trade { get; }

    // Constructor
    public CardSetEditRowViewModel(CardSet cardToAdd, ICommand refreshColumnsCommand)
    {
        CardToAddOrEdit = cardToAdd;

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

    // Dumb pass-through properties
    public string? Name => CardToAddOrEdit.Name;
    public string? SetName => CardToAddOrEdit.SetName;

    // Passthrough properties that raise PropertyChanged when set
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
    public int CardsOwned
    {
        get => CardToAddOrEdit.CardsOwned;
        set
        {
            if (!SetModelValue(CardToAddOrEdit.CardsOwned, value, v => CardToAddOrEdit.CardsOwned = v))
            {
                return;
            }

            // keep adapter in sync so UI updates when +/- changes the value
            Owned.NotifyValueChanged();

            // optional: keep trade valid if owned decreases
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

}

