using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.DomainLogic.CardLegalities;
using CollectaMundo.Infrastructure.CardLegalities;
using CollectaMundo.Infrastructure.CardLegalities.Models.CollectaMundo.Infrastructure.CardLegalities.Models;
using System.Data.SQLite;
using System.Globalization;

namespace CollectaMundo.ApplicationServices.CardLegalities
{
    public sealed class CardLegalityProviderService(IUnitOfWorkRunner uowRunner, ICardLegalityRepo cardLegalityRepo) : ICardLegalityProviderService
    {
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly ICardLegalityRepo _cardLegalityRepo = cardLegalityRepo;

        private List<CardLegalityFormat> _formats = [];
        private Dictionary<string, CardLegalityMasks> _masksByUuid = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<CardLegalityFormat> Formats => _formats;
        public IReadOnlyDictionary<string, CardLegalityMasks> MasksByUuid => _masksByUuid;
        public async Task LoadAsync(SQLiteConnection conn, SQLiteTransaction? tx = null)
        {
            var rows = await _uowRunner.ExecuteReadOnlyAsync(conn => _cardLegalityRepo.GetAllAsync(conn));

            LoadFromRows(rows);
        }
        private void LoadFromRows(IReadOnlyList<CardLegalityDbRow> rows)
        {
            var formatNames = rows
                .SelectMany(row => row.Legalities.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _formats = CreateFormats(formatNames);

            var formatByValue = _formats.ToDictionary(x => x.Value, StringComparer.OrdinalIgnoreCase);

            var masksByUuid = new Dictionary<string, CardLegalityMasks>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                ulong playableMask = 0;
                ulong restrictedMask = 0;

                foreach (var (format, status) in row.Legalities)
                {
                    if (string.IsNullOrWhiteSpace(status))
                    {
                        continue;
                    }

                    if (!formatByValue.TryGetValue(format, out var formatInfo))
                    {
                        continue;
                    }

                    if (IsPlayableStatus(status))
                    {
                        playableMask |= formatInfo.Mask;
                    }

                    if (IsRestrictedStatus(status))
                    {
                        restrictedMask |= formatInfo.Mask;
                    }
                }

                masksByUuid[row.Uuid] = new CardLegalityMasks(
                    PlayableFormatsMask: playableMask,
                    RestrictedFormatsMask: restrictedMask);
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
