namespace CollectaMundo.DomainLogic.Shared
{
    public static class CardColorSort
    {
        public static int GetRank(string? colors)
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
    }
}
