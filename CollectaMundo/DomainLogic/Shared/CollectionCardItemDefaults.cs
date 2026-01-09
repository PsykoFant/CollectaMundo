using CollectaMundo.DomainLogic.Import.Models;
using System.Globalization;

namespace CollectaMundo.DomainLogic.Shared
{
    public static class CollectionCardItemDefaults
    {
        // ---------- String defaults ----------
        public static string GetDefaultString(ImportField field) => field switch
        {
            ImportField.Condition => "Near Mint",
            ImportField.CardFinish => "nonfoil",
            ImportField.Language => "English",
            _ => throw new NotSupportedException($"No string default defined for {field}")
        };

        // ---------- Integer defaults ----------
        public static int GetDefaultInt(ImportField field) => field switch
        {
            ImportField.CardsOwned => 1,
            ImportField.CardsForTrade => 0,
            _ => throw new NotSupportedException($"No int default defined for {field}")
        };
        public static string GetDefaultDisplayValue(ImportField field) => field switch
        {
            ImportField.Condition => GetDefaultString(ImportField.Condition),
            ImportField.CardFinish => GetDefaultString(ImportField.CardFinish),
            ImportField.Language => GetDefaultString(ImportField.Language),

            ImportField.CardsOwned => GetDefaultInt(ImportField.CardsOwned).ToString(CultureInfo.InvariantCulture),
            ImportField.CardsForTrade => GetDefaultInt(ImportField.CardsForTrade).ToString(CultureInfo.InvariantCulture),

            _ => throw new NotSupportedException($"No default display value defined for {field}")
        };
    }
}
