namespace CollectaMundo.ViewModels.ModifyCollection.ParameterModels
{
    public sealed record MatchRetailPriceForSelectedCardsParameter(IReadOnlyList<object> SelectedItems, string Currency);
}
