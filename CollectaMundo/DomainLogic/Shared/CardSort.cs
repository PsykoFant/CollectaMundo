namespace CollectaMundo.DomainLogic.Shared
{
    public static class CardSort
    {
        public static int GetColorRank(string? colors)
        {
            if (string.IsNullOrWhiteSpace(colors))
            {
                return 6; // Colorless
            }

            if (colors.Length == 1)
            {
                return colors[0] switch
                {
                    'W' => 0,
                    'U' => 1,
                    'B' => 2,
                    'R' => 3,
                    'G' => 4,
                    _ => 7
                };
            }

            return 5; // Multicolor
        }
        public static int GetTypeRank(string? type, int gamePlayCard)
        {
            type ??= string.Empty;

            if (gamePlayCard == 0)
            {
                return 99;
            }

            // Lands deliberately take precedence over other types,
            // e.g. Artifact Land or Land Creature.
            if (type.Contains("Basic Land", StringComparison.OrdinalIgnoreCase))
            {
                return 9;
            }

            if (type.Contains("Land", StringComparison.OrdinalIgnoreCase))
            {
                return 8;
            }

            if (type.Contains("Creature", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (type.Contains("Sorcery", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (type.Contains("Instant", StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (type.Contains("Enchantment", StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            if (type.Contains("Planeswalker", StringComparison.OrdinalIgnoreCase))
            {
                return 5;
            }

            if (type.Contains("Artifact", StringComparison.OrdinalIgnoreCase))
            {
                return 6;
            }

            return 7; // Other gameplay card types
        }
    }

}

