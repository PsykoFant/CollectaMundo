namespace CollectaMundo.DomainLogic.Decks.Models
{
    public static class CommanderFormats
    {
        private static readonly HashSet<string> Formats = new(StringComparer.OrdinalIgnoreCase)
        {
            "commander",
            "duel",
            "predh",
            "brawl",
            "standardbrawl",
            "paupercommander",
            "oathbreaker",
            "tlr"
        };
        public static bool IsCommanderLike(string? format)
        {
            return Formats.Contains(format ?? string.Empty);
        }
    }
}
