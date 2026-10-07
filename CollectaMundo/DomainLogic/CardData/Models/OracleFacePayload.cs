namespace CollectaMundo.DomainLogic.CardData.Models
{
    public sealed record OracleFacePayload(
        string? Name,
        string? ManaCostRaw,
        double? ManaValue,
        string? Colors,
        string? Keywords,
        string? RulesText,
        string? SuperTypes,
        string? Types,
        string? SubTypes,
        string? Type);
}
