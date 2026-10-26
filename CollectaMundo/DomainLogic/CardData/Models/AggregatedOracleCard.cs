namespace CollectaMundo.DomainLogic.CardData.Models
{
    public sealed record AggregatedOracleCard(string ScryfallOracleId, string Name, int GamePlayCard, string? ManaCostRaw, double? ManaValue, string? Colors, string? Keywords, string? RulesText, string? SuperTypes, string? Types, string? SubTypes, string? Type);
}
