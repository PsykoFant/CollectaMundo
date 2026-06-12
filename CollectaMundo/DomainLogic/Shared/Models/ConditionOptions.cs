namespace CollectaMundo.DomainLogic.Shared.Models
{
    public static class ConditionOptions
    {
        public static IReadOnlyList<string> Values { get; } =
        [
            "Mint",
            "Near Mint",
            "Excellent",
            "Good",
            "Light Played",
            "Played",
            "Poor"
        ];
    }
}
