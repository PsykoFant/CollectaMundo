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

                SetCode = row.SetCode,
                Language = row.Language,
                Rarity = row.Rarity,
                Finishes = row.Finishes,
                Availability = row.Availability,
                IsPromo = row.IsPromo,

                OtherFaceIds = ParseOtherFaceIds(row.OtherFaceIds),

                LegalityMasks = oracle.LegalityMasks
            };
        }
        private static IReadOnlyList<string> ParseOtherFaceIds(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return [];
            }

            var values = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return values.Length == 0 ? [] : values;
        }
    }
}
