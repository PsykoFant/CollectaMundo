using CollectaMundo.Infrastructure.CardLists.Models;

namespace CollectaMundo.DomainLogic.CardLists.Models
{
    public static class PrintingCardFactory
    {
        public static PrintingCard FromRow(CardPrintingDbRow row)
        {
            var oracle = new OracleCard
            {
                ScryfallOracleId = row.ScryfallOracleId ?? string.Empty,
                Name = row.Name ?? string.Empty,
                ManaCostRaw = row.ManaCostRaw,
                ManaCost = CardFieldNormalizer.ProcessManaCost(row.ManaCostRaw),
                Colors = CardFieldNormalizer.JoinAndDedupCsv(row.Colors),
                Type = CardFieldNormalizer.JoinAndDedupCsv(row.Type),
                Types = CardFieldNormalizer.JoinAndDedupCsv(row.Types),
                SuperTypes = CardFieldNormalizer.JoinAndDedupCsv(row.SuperTypes),
                SubTypes = CardFieldNormalizer.JoinAndDedupCsv(row.SubTypes),
                Keywords = CardFieldNormalizer.JoinAndDedupCsv(row.Keywords),
                Text = row.RulesText,
                Side = row.Side,
                OtherFaceIds = CardFieldNormalizer.ParseOtherFaceIds(row.OtherFaceIds),
                ManaValue = row.ManaValue ?? 0
            };

            return new PrintingCard
            {
                Oracle = oracle,
                Uuid = row.Uuid ?? string.Empty,
                SetCode = row.SetCode,
                Language = row.Language,
                Rarity = row.Rarity,
                Finishes = row.Finishes
            };
        }
    }
}
}
