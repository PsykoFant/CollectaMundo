using CollectaMundo.DomainLogic.CardLegalities;
using CollectaMundo.Infrastructure.CardLegalities;
using CollectaMundo.Infrastructure.CardLegalities.Models;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;

namespace CollectaMundo.ApplicationServices.CardLegalities
{
    public sealed class CardLegalityProviderService(ICardLegalityRepo cardLegalityRepo) : ICardLegalityProviderService
    {
        private readonly ICardLegalityRepo _cardLegalityRepo = cardLegalityRepo;

        private List<CardLegalityFormat> _formats = [];
        private Dictionary<string, CardLegalityMasks> _masksByUuid = new(StringComparer.OrdinalIgnoreCase);
        public CardLegalityFormat? GetFormat(string? format)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return null;
            }

            return _formats.FirstOrDefault(x => string.Equals(x.Value, format.Trim(), StringComparison.OrdinalIgnoreCase));
        }
        public IReadOnlyList<CardLegalityFormat> Formats => _formats;
        public IReadOnlyDictionary<string, CardLegalityMasks> MasksByUuid => _masksByUuid;
        public async Task LoadLegalitiesAsync(SQLiteConnection conn, SQLiteTransaction? tx = null)
        {
            var readSw = Stopwatch.StartNew();

            var data = await _cardLegalityRepo.GetAllAsync(conn, tx);

            readSw.Stop();

            Debug.WriteLine($"[Legalities] Read/materialize rows: {readSw.ElapsedMilliseconds} ms");

            var buildSw = Stopwatch.StartNew();

            LoadFromData(data);

            buildSw.Stop();

            Debug.WriteLine($"[Legalities] Build masks: {buildSw.ElapsedMilliseconds} ms");
        }
        private void LoadFromData(CardLegalityReadResult data)
        {
            var sortedFormatNames = data.FormatNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

            _formats = CreateFormats(sortedFormatNames);

            var formatByValue = _formats.ToDictionary(x => x.Value, StringComparer.OrdinalIgnoreCase);

            // Translate source-column position to its final bitmask ONCE.
            var masksByColumn = new ulong[data.FormatNames.Length];

            for (var i = 0; i < data.FormatNames.Length; i++)
            {
                var formatName = data.FormatNames[i];

                if (!formatByValue.TryGetValue(formatName, out var format))
                {
                    throw new InvalidOperationException($"No legality format metadata found for '{formatName}'.");
                }

                masksByColumn[i] = format.Mask;
            }

            var masksByUuid = new Dictionary<string, CardLegalityMasks>(data.Rows.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var row in data.Rows)
            {
                ulong playableMask = 0;
                ulong restrictedMask = 0;

                for (var i = 0; i < row.Statuses.Length; i++)
                {
                    var status = row.Statuses[i];

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

                masksByUuid[row.Uuid] = new CardLegalityMasks(PlayableFormatsMask: playableMask, RestrictedFormatsMask: restrictedMask);
            }

            _masksByUuid = masksByUuid;
        }
        private static List<CardLegalityFormat> CreateFormats(IReadOnlyList<string> formatNames)
        {
            if (formatNames.Count > 64)
            {
                throw new InvalidOperationException("Card legality format count exceeds ulong bitmask capacity.");
            }

            var formats = new List<CardLegalityFormat>(formatNames.Count);

            for (int i = 0; i < formatNames.Count; i++)
            {
                var value = formatNames[i];

                formats.Add(new CardLegalityFormat
                {
                    Id = i,
                    Value = value,
                    DisplayName = ToDisplayName(value),
                    Mask = 1UL << i
                });
            }

            return formats;
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
