namespace CollectaMundo.DomainLogic.Import
{
    public static class CsvHeaderMatcher
    {
        public static string? GuessCsvHeader(string fieldToMap, IReadOnlyList<string> guesses, IReadOnlyList<string> csvHeaders)
        {
            if (csvHeaders == null || csvHeaders.Count == 0)
            {
                return null;
            }

            var candidates = new List<string> { fieldToMap };
            if (guesses != null)
            {
                candidates.AddRange(guesses);
            }

            candidates = [.. candidates
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Select(g => g.Trim())];

            if (candidates.Count == 0)
            {
                return null;
            }

            // 1) Exact match (case-insensitive)
            foreach (var header in csvHeaders)
            {
                foreach (var candidate in candidates)
                {
                    if (string.Equals(header, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return header;
                    }
                }
            }

            // 2) Contains match (case-insensitive)
            foreach (var header in csvHeaders)
            {
                string headerLower = header.ToLowerInvariant();

                foreach (var candidate in candidates)
                {
                    string candidateLower = candidate.ToLowerInvariant();

                    if (headerLower.Contains(candidateLower) || candidateLower.Contains(headerLower))
                    {
                        return header;
                    }
                }
            }

            return null;
        }
    }
}
