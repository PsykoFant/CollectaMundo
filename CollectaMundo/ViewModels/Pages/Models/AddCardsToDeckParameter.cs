namespace CollectaMundo.ViewModels.Pages.Models
{
    public sealed record AddCardsToDeckParameter(IEnumerable<object> SelectedItems, int DeckLocationId);
}
