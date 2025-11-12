using CollectaMundo.DomainLogic.Import.Models;
using System.IO;
using System.Text;

namespace CollectaMundo.DomainLogic.Import
{
    public class ImportLogic : IImportLogic
    {
        // Step 1
        public async Task<List<TempCardItem>> ParseCsvFileAsync(string filePath, IProgress<int> progress, CancellationToken cancelToken)
        {
            var cardItems = new List<TempCardItem>();
            var delimiter = ',';

            // Step 1: Estimate total lines
            int totalLines = 0;
            using (var counter = new StreamReader(filePath, Encoding.UTF8))
                while (await counter.ReadLineAsync(cancelToken) is not null)
                    totalLines++;

            // Step 2: Parse actual content
            using var reader = new StreamReader(filePath, Encoding.UTF8);
            string? header = await reader.ReadLineAsync(cancelToken);
            if (header == null)
                return cardItems;

            if (header.Contains(';')) delimiter = ';';
            var headers = ParseCsvLine(header, delimiter);

            int currentLine = 0;
            while (!reader.EndOfStream)
            {
                cancelToken.ThrowIfCancellationRequested();

                string? line = await reader.ReadLineAsync(cancelToken);
                currentLine++;

                if (line == null)
                    continue;

                var values = ParseCsvLine(line, delimiter);
                var item = new TempCardItem();

                for (int i = 0; i < headers.Count; i++)
                {
                    string cleaned = RemoveUnwantedPrefixes(values.Count > i ? values[i] : string.Empty);
                    item.Fields[headers[i]] = cleaned;
                }

                cardItems.Add(item);

                // Report progress every 100 lines (or adjust)
                if (currentLine % 100 == 0 && totalLines > 0)
                {
                    int percent = (int)((double)currentLine / totalLines * 100);
                    progress?.Report(percent);
                }
            }

            // Ensure 100% reported at end
            progress?.Report(100);
            return cardItems;
        }

        private static List<string> ParseCsvLine(string line, char delimiter)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == delimiter) { result.Add(sb.ToString().Trim()); sb.Clear(); }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }

            result.Add(sb.ToString().Trim());
            return result;
        }
        private static string RemoveUnwantedPrefixes(string input)
        {
            if (input.StartsWith("Extras: "))
            {
                return input["Extras: ".Length..].Trim();
            }

            if (input.StartsWith("Art Card: "))
            {
                return input["Art Card: ".Length..].Trim();
            }

            return input;
        }

        // Step 2
        public ImportMatchSummaryDto AssignUuidsToImportItems(List<TempCardItem> importCandidates, Dictionary<string, List<string>> idToUuids, string selectedCsvHeader)
        {
            int total = 0;
            int matchedUuid = 0;
            int matchedMultipleUuids = 0;

            foreach (var item in importCandidates)
            {
                total++;

                if (!item.Fields.TryGetValue(selectedCsvHeader, out var csvValue) || string.IsNullOrWhiteSpace(csvValue))
                {
                    continue;
                }

                if (!idToUuids.TryGetValue(csvValue, out var uuids) || uuids == null || uuids.Count == 0)
                {
                    continue;
                }

                if (uuids.Count == 1)
                {
                    item.Fields["uuid"] = uuids[0];
                    matchedUuid++;
                }
                else // multiple matches
                {
                    item.Fields["uuids"] = string.Join(",", uuids);
                    matchedUuid++;
                    matchedMultipleUuids++;
                }
            }

            return new ImportMatchSummaryDto
            {
                TotalItems = total,
                ItemsWithUuid = matchedUuid,
                ItemsWithMultipleUuids = matchedMultipleUuids
            };
        }
    }
}
