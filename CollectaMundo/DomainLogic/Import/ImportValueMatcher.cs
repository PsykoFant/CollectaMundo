using CollectaMundo.DomainLogic.Import.Models.Enums;

namespace CollectaMundo.DomainLogic.Import
{
    public static class ImportValueMatcher
    {
        public static string? GuessCsvHeader(ImportField field, IReadOnlyList<string> csvHeaders)
        {
            if (csvHeaders == null || csvHeaders.Count == 0)
            {
                return null;
            }

            // Collect guesses: canonical field name + domain aliases
            var allGuesses = new List<string> { field.ToString() };

            if (_headerGuesses.TryGetValue(field, out var fieldGuesses))
            {
                allGuesses.AddRange(fieldGuesses);
            }

            // Normalize guesses
            var normalizedGuesses = allGuesses
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Select(g => g.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();

            if (normalizedGuesses.Count == 0)
            {
                return null;
            }

            // Normalize headers once (preserve original)
            var normalizedHeaders = csvHeaders
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .Select(h => new
                {
                    Original = h,
                    Normalized = h.Trim().ToLowerInvariant()
                })
                .ToList();

            // 1. Exact match
            foreach (var header in normalizedHeaders)
            {
                if (normalizedGuesses.Contains(header.Normalized))
                {
                    return header.Original;
                }
            }

            // 2. Contains / fuzzy match
            foreach (var header in normalizedHeaders)
            {
                foreach (var guess in normalizedGuesses)
                {
                    if (header.Normalized.Contains(guess) ||
                        guess.Contains(header.Normalized))
                    {
                        return header.Original;
                    }
                }
            }

            return null;
        }
        public static string? MapImportValue(string importValue, ImportField field, IReadOnlyList<string> canonicalValues)
        {
            if (string.IsNullOrWhiteSpace(importValue))
            {
                return null;
            }

            if (!_aliases.TryGetValue(field, out var aliasesForField))
            {
                return null;
            }

            var normalizedImport = importValue.Trim().ToLowerInvariant();

            // 1) Exact match against canonical values
            foreach (var canonical in canonicalValues)
            {
                if (string.Equals(canonical, importValue, StringComparison.OrdinalIgnoreCase))
                {
                    return canonical;
                }
            }

            // NOTE: alias dictionary order defines precedence

            // 2) Alias exact match
            foreach (var (canonical, knownAliases) in aliasesForField)
            {
                if (knownAliases.Any(a => normalizedImport == a))
                {
                    return canonical;
                }
            }

            // 3) Alias contains match
            foreach (var (canonical, knownAliases) in aliasesForField)
            {
                if (knownAliases.Any(a => normalizedImport.Contains(a)))
                {
                    return canonical;
                }
            }

            return null;
        }

        #region lookup dictionaries for guesses

        // Header guesses for ImportField
        private static readonly Dictionary<ImportField, string[]> _headerGuesses = new()
        {
            [ImportField.CardName] = ["name", "card name", "card_name"],
            [ImportField.SetName] = ["set name", "setname", "set", "edition"],
            [ImportField.SetCode] = ["set code", "setcode", "code", "edition code"],

            [ImportField.Condition] = ["condition", "state", "card condition"],
            [ImportField.CardFinish] = ["finish", "foiling", "card finish", "foil", "printing"],
            [ImportField.Language] = ["lang", "language"],
            [ImportField.CardsOwned] = ["quantity", "count", "owned", "qty"],
            [ImportField.CardsForTrade] = ["trade", "for trade", "sell", "forsale", "selling"]
        };

        // Value guesses / aliases for ImportField
        private static readonly Dictionary<ImportField, Dictionary<string, string[]>> _aliases = new()
        {
            [ImportField.Language] = new()
            {
                ["Ancient Greek"] = ["ancient greek", "greek (ancient)", "anc greek", "grc"],
                ["Arabic"] = ["ara", "ar"],
                ["Chinese Simplified"] = ["simplified chinese", "chinese (s)", "chinese(s)", "cn", "zh-cn", "zh-hans", "hans"],
                ["Chinese Traditional"] = ["traditional chinese", "chinese (t)", "chinese(t)", "tw", "zh-tw", "zh-hant", "hant"],
                ["English"] = ["eng", "en", "gb", "uk", "us"],
                ["French"] = ["fra", "fre", "fr", "frn"],
                ["German"] = ["ger", "de", "deu", "deutsch"],
                ["Hebrew"] = ["heb", "he", "iw"],
                ["Italian"] = ["ita", "it"],
                ["Japanese"] = ["jpn", "jp", "ja"],
                ["Korean"] = ["kor", "kr", "ko"],
                ["Latin"] = ["lat", "la"],
                ["Phyrexian"] = ["phrexian", "phy", "ph", "phyr"],
                ["Portuguese (Brazil)"] =
                [
                    "portuguese brazil", "brazilian portuguese",
                    "portuguese", "português", "pt-br", "ptbr", "pt"
                    ],
                ["Quenya"] = ["qya"],
                ["Russian"] = ["rus", "ru"],
                ["Sanskrit"] = ["san", "sa"],
                ["Spanish"] = ["spa", "es", "esp", "español"]
            },

            [ImportField.CardFinish] = new()
            {
                ["etched"] = ["etch", "etched foil", "efoil", "et"],
                ["foil"] = ["foiled", "holo", "holofoil", "holfoil", "f"],
                ["nonfoil"] = ["non-foil", "non foil", "regular", "normal", "standard", "nf"],
                ["signed"] = ["autograph", "autographed", "sig"]
            },

            [ImportField.Condition] = new()
            {
                ["Mint"] = ["m", "pristine", "perfect"],
                ["Near Mint"] = ["nm", "near-mint", "nm/m", "nm-m", "pack fresh", "fresh", "booster-Fresh"],
                ["Excellent"] = ["ex", "exc", "slightly played", "sp", "minor wear"],
                ["Good"] = ["gd", "Moderately played", "mp", "visible wear", "vg", "very good"],
                ["Light Played"] = ["lp", "lightly played", "severe wear"],
                ["Played"] = ["pl", "heavily played", "hp"],
                ["Poor"] = ["damaged", "dmg", "heavily damaged", "crease", "creased"]
            }
        };

        #endregion
    }
}
