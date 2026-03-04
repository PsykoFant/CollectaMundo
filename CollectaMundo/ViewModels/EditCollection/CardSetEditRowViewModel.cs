using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.ViewModels.Pages.SharedElements;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace CollectaMundo.ViewModels.EditCollection;

public sealed partial class CardSetEditRowViewModel : ObservableObject
{
    public CardSet Model { get; }

    public ComboBindingViewModel ConditionCombo { get; }
    public ComboBindingViewModel FinishCombo { get; }
    public ComboBindingViewModel LanguageCombo { get; }

    public CardSetEditRowViewModel(CardSet model,ICommand refreshColumnsCommand)
    {
        Model = model;

        ConditionCombo = new ComboBindingViewModel(
            items: model.Conditions,
            getter: () => SelectedCondition,
            setter: v => SelectedCondition = (string?)v,
            refreshCommand: refreshColumnsCommand);

        FinishCombo = new ComboBindingViewModel(
            items: model.AvailableFinishes,
            getter: () => SelectedFinish,
            setter: v => SelectedFinish = (string?)v,
            refreshCommand: refreshColumnsCommand);

        LanguageCombo = new ComboBindingViewModel(
            items: model.OtherLanguages,
            getter: () => Language,
            setter: v => Language = (string?)v,
            refreshCommand: refreshColumnsCommand);
    }

    // pass-through properties
    public string? SelectedCondition
    {
        get => Model.SelectedCondition;
        set
        {
            if (Model.SelectedCondition == value) return;
            Model.SelectedCondition = value;
            OnPropertyChanged();
        }
    }
    public string? SelectedFinish
    {
        get => Model.SelectedFinish;
        set
        {
            if (Model.SelectedFinish == value) return;
            Model.SelectedFinish = value;
            OnPropertyChanged();
        }
    }
    public string? Language
    {
        get => Model.Language;
        set
        {
            if (Model.Language == value) return;
            Model.Language = value;
            OnPropertyChanged();
        }
    }
    public int CardsOwned
    {
        get => Model.CardsOwned;
        set
        {
            if (Model.CardsOwned == value) return;
            Model.CardsOwned = value;
            OnPropertyChanged();
        }
    }
    public int CardsForTrade
    {
        get => Model.CardsForTrade;
        set
        {
            if (Model.CardsForTrade == value) return;
            Model.CardsForTrade = value;
            OnPropertyChanged();
        }
    }
}

