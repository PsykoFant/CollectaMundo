using CollectaMundo.DomainLogic.CardLegalities;
using CollectaMundo.Infrastructure.CardLegalities;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardLegalities
{
    public sealed class CardLegalityService(ICardLegalityLogic cardLegalityLogic, ICardLegalityRepo cardLegalityRepo) : ICardLegalityService
    {
        private readonly ICardLegalityLogic _cardLegalityLogic = cardLegalityLogic;
        private readonly ICardLegalityRepo _cardLegalityRepo = cardLegalityRepo;
        private List<CardLegalityFormat> _formats = [];
        private Dictionary<string, CardLegalityFormat> _formatByValue = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<CardLegalityFormat> Formats => _formats;
        public CardLegalityFormat? GetFormat(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return _formatByValue.TryGetValue(value, out var format) ? format : null;
        }
        public async Task LoadFormatsAsync(SQLiteConnection conn, SQLiteTransaction? tx = null)
        {
            var rows = await _cardLegalityRepo.GetFormatCatalogAsync(conn, tx);

            if (rows.Count == 0)
            {
                throw new InvalidOperationException("Card legality format catalog is empty.");
            }

            var formats = rows.Select(row => _cardLegalityLogic.CreateFormat(row.BitIndex, row.Value)).ToList();

            var formatByValue = formats.ToDictionary(format => format.Value, StringComparer.OrdinalIgnoreCase);

            _formats = formats;
            _formatByValue = formatByValue;
        }

        // Called during first time startup
        public async Task<IReadOnlyDictionary<string, CardLegalityMasks>> PrepareDerivedDataAsync(SQLiteConnection conn, SQLiteTransaction tx)
        {
            var legalityData = await _cardLegalityRepo.GetAllAsync(conn, tx);

            if (legalityData.FormatNames.Length == 0)
            {
                throw new InvalidOperationException(
                    "Card legality source contains no format columns.");
            }

            if (legalityData.Rows.Count == 0)
            {
                throw new InvalidOperationException("Card legality source contains no rows.");
            }

            var formats = _cardLegalityLogic.CreateFormats(legalityData.FormatNames);
            var masksByColumn = _cardLegalityLogic.CreateMasksBySourceColumn(legalityData.FormatNames, formats);
            var masksByUuid = new Dictionary<string, CardLegalityMasks>(legalityData.Rows.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var row in legalityData.Rows)
            {
                masksByUuid[row.Uuid] = _cardLegalityLogic.CreateMasks(row.Statuses, masksByColumn);
            }

            await _cardLegalityRepo.RebuildFormatCatalogAsync(conn, tx, formats);

            return masksByUuid;
        }
    }
}
