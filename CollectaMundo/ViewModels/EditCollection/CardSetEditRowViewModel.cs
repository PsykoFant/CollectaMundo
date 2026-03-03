using CollectaMundo.DomainLogic.CardLists.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.ViewModels.EditCollection;

public sealed partial class CardSetEditRowViewModel(CardSet model) : ObservableObject
{
    public CardSet Model { get; } = model;

    // Display (read-only)
    public string Name => Model.Name;
    public string SetName => Model.SetName;

    // Editable pass-through properties (raise VM notifications)
    public string? SelectedCondition
    {
        get => Model.SelectedCondition;
        set => SetProperty(Model.SelectedCondition, value, Model, static (m, v) => m.SelectedCondition = v);
    }
    public string? SelectedFinish
    {
        get => Model.SelectedFinish;
        set => SetProperty(Model.SelectedFinish, value, Model, static (m, v) => m.SelectedFinish = v);
    }
    public string? Language
    {
        get => Model.Language;
        set => SetProperty(Model.Language, value, Model, static (m, v) => m.Language = v);
    }
    public int CardsOwned
    {
        get => Model.CardsOwned;
        set => SetProperty(Model.CardsOwned, value, Model, static (m, v) => m.CardsOwned = v);
    }
    public int CardsForTrade
    {
        get => Model.CardsForTrade;
        set => SetProperty(Model.CardsForTrade, value, Model, static (m, v) => m.CardsForTrade = v);
    }

    // Optional helper if you still need “remove when zero” checks in the parent VM
    public bool IsOwnedZeroOrLess => CardsOwned <= 0;
}
