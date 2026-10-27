using CollectaMundo.DomainLogic.CardLegalities;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.Infrastructure.CardLists.Models;

namespace CollectaMundo.ApplicationServices.CardLists.Factories
{
    public static class PrintingCardFactory
    {
        public static PrintingCard FromRow(PrintingCardDbRow row, OracleCard oracle)
        {
            return new PrintingCard
            {
                Uuid = row.Uuid,
                Oracle = oracle,

                Side = row.Side,
                SetCode = row.SetCode,
                Language = row.Language,
                Rarity = row.Rarity,
                Finishes = row.Finishes,
                Availability = row.Availability,
                IsPromo = row.IsPromo,

                LegalityMasks = new CardLegalityMasks(PlayableFormatsMask: row.PlayableFormatsMask, RestrictedFormatsMask: row.RestrictedFormatsMask)
            };
        }
    }
}
