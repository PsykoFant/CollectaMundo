using System.Text;

namespace CollectaMundo.DomainLogic.CardLists
{
    public static class CardFieldNormalizer
    {
        public static string JoinAndDedupCsv(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return string.Empty;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var segments = csv.Split(',');

            var sb = new StringBuilder();

            foreach (var segment in segments)
            {
                var trimmed = segment.Trim();

                if (trimmed.Length == 0 || !seen.Add(trimmed))
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(',');
                }

                sb.Append(trimmed);
            }

            return sb.ToString();
        }
        public static List<string> ParseOtherFaceIds(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return [];
            }

            return [.. raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)];
        }
        public static string ProcessManaCost(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            char[] separators = ['{', '}'];

            return string
                .Join(",", raw.Split(separators, StringSplitOptions.RemoveEmptyEntries))
                .Trim(',');
        }
    }
}
