using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.DomainLogic.Decks.Models.BuildingRules
{
    public static class CollectionQuantityRules
    {
        public static bool RequiresAvailabilityCheck(OracleCard card)
        {
            var isBasicLand = card.Type?.Contains("Basic Land") ?? false;

            return !isBasicLand || card.Name == "Wastes";
        }
    }
}
