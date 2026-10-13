using System.Globalization;

namespace CollectaMundo.DomainLogic.CardLegalities
{
    public class CardLegalityLogic : ICardLegalityLogic
    {
        public List<CardLegalityFormat> CreateFormats(IReadOnlyList<string> formatNames)
        {
            var orderedNames = formatNames.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

            if (orderedNames.Count > 64)
            {
                throw new InvalidOperationException("Card legality format count exceeds ulong bitmask capacity.");
            }

            var formats = new List<CardLegalityFormat>(orderedNames.Count);

            for (var i = 0; i < orderedNames.Count; i++)
            {
                formats.Add(CreateFormat(i, orderedNames[i]));
            }

            return formats;
        }
        public ulong[] CreateMasksBySourceColumn(IReadOnlyList<string> sourceFormatNames, IReadOnlyList<CardLegalityFormat> formats)
        {
            var formatByValue = formats.ToDictionary(x => x.Value, StringComparer.OrdinalIgnoreCase);

            var masksByColumn = new ulong[sourceFormatNames.Count];

            for (var i = 0; i < sourceFormatNames.Count; i++)
            {
                var formatName = sourceFormatNames[i];

                if (!formatByValue.TryGetValue(formatName, out var format))
                {
                    throw new InvalidOperationException($"No legality format metadata found for '{formatName}'.");
                }

                masksByColumn[i] = format.Mask;
            }

            return masksByColumn;
        }
        public CardLegalityMasks CreateMasks(IReadOnlyList<string?> statuses, IReadOnlyList<ulong> masksByColumn)
        {
            if (statuses.Count != masksByColumn.Count)
            {
                throw new InvalidOperationException("Legality status count does not match format column count.");
            }

            ulong playableMask = 0;
            ulong restrictedMask = 0;

            for (var i = 0; i < statuses.Count; i++)
            {
                var status = statuses[i];

                if (string.IsNullOrWhiteSpace(status))
                {
                    continue;
                }

                var formatMask = masksByColumn[i];

                if (IsPlayableStatus(status))
                {
                    playableMask |= formatMask;
                }

                if (IsRestrictedStatus(status))
                {
                    restrictedMask |= formatMask;
                }
            }

            return new CardLegalityMasks(PlayableFormatsMask: playableMask, RestrictedFormatsMask: restrictedMask);
        }
        public CardLegalityFormat CreateFormat(int bitIndex, string value)
        {
            if (bitIndex is < 0 or >= 64)
            {
                throw new ArgumentOutOfRangeException(nameof(bitIndex), "Card legality bit index must be between 0 and 63.");
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            return new CardLegalityFormat
            {
                Id = bitIndex,
                Value = value,
                DisplayName = ToDisplayName(value),
                Mask = 1UL << bitIndex
            };
        }
        private static bool IsPlayableStatus(string status)
        {
            return status.Equals("Legal", StringComparison.OrdinalIgnoreCase) || status.Equals("Restricted", StringComparison.OrdinalIgnoreCase);
        }
        private static bool IsRestrictedStatus(string status)
        {
            return status.Equals("Restricted", StringComparison.OrdinalIgnoreCase);
        }
        private static string ToDisplayName(string value)
        {
            return value switch
            {
                "paupercommander" => "Pauper Commander",
                "standardbrawl" => "Standard Brawl",
                "oldschool" => "Old School",
                "predh" => "PreDH",
                "tlr" => "Tiny Leaders: Reborn",
                _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value)
            };
        }
    }
}
