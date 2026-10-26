namespace CollectaMundo.Infrastructure.CardLists.Models
{
    public sealed record OracleCardDbRow(string ScryfallOracleId, string Name, int GamePlayCard, string? ManaCostRaw, double? ManaValue, string? Colors, string? Keywords, string? RulesText, string? SuperTypes, string? Types, string? SubTypes, string? Type);
}
