using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.DomainLogic.CardLists.Models
{
    public sealed record PrintingCardAggregationResult(
        IReadOnlyList<PrintingCard> Printings,
        IReadOnlyDictionary<string, PrintingCard> BySourceUuid);
}
