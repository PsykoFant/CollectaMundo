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
            {
                while (await counter.ReadLineAsync(cancelToken) is not null)
                {
                    totalLines++;
                }
            }

            // Step 2: Parse actual content
            using var reader = new StreamReader(filePath, Encoding.UTF8);
            string? header = await reader.ReadLineAsync(cancelToken);
            if (header == null)
            {
                return cardItems;
            }

            if (header.Contains(';'))
            {
                delimiter = ';';
            }

            var headers = ParseCsvLine(header, delimiter);

            if (headers.Count < 2)
            {
                // Probably not a valid CSV — possibly a text file or malformed export
                return [];
            }

            int currentLine = 0;
            while (!reader.EndOfStream)
            {
                if (totalLines % 100 == 0)
                {
                    cancelToken.ThrowIfCancellationRequested();
                }

                string? line = await reader.ReadLineAsync(cancelToken);
                currentLine++;

                if (line == null)
                {
                    continue;
                }

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

            if (cardItems.Count == 0)
            {
                // File had header but no usable rows
                return [];
            }

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
        public ImportMatchSummaryDto AssignUuidsToImportItems(List<TempCardItem> importCandidates, Dictionary<string, List<string>> idToUuids, string selectedCsvHeader, IProgress<int>? percentProgress, CancellationToken cancelToken)
        {
            int processed = 0;
            int matchedUuid = 0;
            int matchedMultipleUuids = 0;
            int total = importCandidates.Count;

            foreach (var item in importCandidates)
            {
                processed++;

                if (processed % 100 == 0)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    if (total > 0)
                    {
                        int percent = (int)((double)processed / total * 100);
                        percentProgress?.Report(percent);
                    }
                }

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
                else
                {
                    item.Fields["uuids"] = string.Join(",", uuids);
                    matchedUuid++;
                    matchedMultipleUuids++;
                }
            }

            percentProgress?.Report(100);

            return new ImportMatchSummaryDto
            {
                TotalItems = total,
                ItemsWithUuid = matchedUuid,
                ItemsWithMultipleUuids = matchedMultipleUuids
            };
        }

        // Step 3
        public (bool HasName, bool HasSetName, bool HasSetCode, string? NameHeader, string? SetNameHeader, string? SetCodeHeader) ExtractMappedFields(IReadOnlyList<NameSetColumnMapping> mappings)
        {
            string? name = mappings.FirstOrDefault(m => m.FieldToMap == "Card Name")?.SelectedCsvHeader;
            string? setName = mappings.FirstOrDefault(m => m.FieldToMap == "Set Name")?.SelectedCsvHeader;
            string? setCode = mappings.FirstOrDefault(m => m.FieldToMap == "Set Code")?.SelectedCsvHeader;

            return (
                HasName: !string.IsNullOrWhiteSpace(name),
                HasSetName: !string.IsNullOrWhiteSpace(setName),
                HasSetCode: !string.IsNullOrWhiteSpace(setCode),
                NameHeader: name,
                SetNameHeader: setName,
                SetCodeHeader: setCode
            );
        }
        public bool IsItemResolved(TempCardItem item)
        {
            return item.Fields.ContainsKey("uuid") || item.Fields.ContainsKey("uuids");
        }

        //  Apply Matching Results (Name + Set Code)
        public void ApplySetCodeMatches(IReadOnlyList<TempCardItem> items, IReadOnlyList<(string Name, string SetCode)> pairs, Dictionary<string, List<string>> results)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var (name, setCode) = pairs[i];

                string key = $"{name}_{setCode}";

                if (!results.TryGetValue(key, out var uuidList))
                {
                    continue;
                }

                AssignUuids(item, uuidList);
            }
        }

        //  Apply Matching Results (Name + Set Name)
        public void ApplySetNameMatches(IReadOnlyList<TempCardItem> items, IReadOnlyList<(string Name, string SetName)> pairs, Dictionary<string, List<string>> results)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var (name, setName) = pairs[i];

                string key = $"{name}_{setName}";

                if (!results.TryGetValue(key, out var uuidList))
                {
                    continue;
                }

                AssignUuids(item, uuidList);
            }
        }

        // Applies name-only UUID matches to the batch of TempCardItem objects. This is a fallback scenario used when neither SetCode nor SetName mappings are available.
        public void ApplyNameOnlyMatches(IReadOnlyList<TempCardItem> batch, IReadOnlyList<string> names, Dictionary<string, List<string>> results)
        {
            if (batch == null || names == null || results == null)
            {
                return;
            }

            for (int i = 0; i < batch.Count; i++)
            {
                var item = batch[i];
                var name = names[i];

                // Skip if name missing or empty
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                // Look up via exact card name
                if (!results.TryGetValue(name, out var uuidList) ||
                    uuidList == null || uuidList.Count == 0)
                {
                    // No matches found → do nothing (no uuid, no uuids)
                    continue;
                }

                // Apply domain invariant:
                // - Exactly one UUID  → store as "uuid"
                // - Multiple UUIDs    → store as comma-separated string in "uuids"
                AssignUuids(item, uuidList);
            }
        }


        //  Assign uuid / uuids to TempCardItem, enforcing invariants
        private static void AssignUuids(TempCardItem item, List<string> uuidList)
        {
            if (uuidList == null || uuidList.Count == 0)
            {
                return;
            }

            if (uuidList.Count == 1)
            {
                // enforce invariant: uuid + no uuids
                item.Fields.Remove("uuids");
                item.Fields["uuid"] = uuidList[0];
            }
            else
            {
                // enforce invariant: uuids + no uuid
                item.Fields.Remove("uuid");
                item.Fields["uuids"] = string.Join(",", uuidList);
            }
        }
        public ImportMatchSummaryDto FinalizeMatchResults(IReadOnlyList<TempCardItem> items)
        {
            bool anyBoth = false;
            bool anySingle = false;
            bool anyMulti = false;

            foreach (var item in items)
            {
                bool hasUuid = item.Fields.ContainsKey("uuid");
                bool hasUuids = item.Fields.ContainsKey("uuids");

                if (hasUuid && hasUuids)
                {
                    anyBoth = true;
                }
                else if (hasUuid)
                {
                    anySingle = true;
                }
                else if (hasUuids)
                {
                    anyMulti = true;
                }
                // else → no match, but we don’t need a flag
            }

            if (anyBoth)
            {
                throw new InvalidOperationException("Internal error: item has both uuid and uuids.");
            }

            if (!anySingle && !anyMulti)
            {
                throw new InvalidOperationException("No matches were found using name + set mapping.");
            }

            if (anyMulti)
            {
                return new ImportMatchSummaryDto
                {
                    ItemsWithMultipleUuids = items.Count(i => i.Fields.ContainsKey("uuids"))
                };
            }

            return new ImportMatchSummaryDto
            {
                ItemsWithMultipleUuids = 0
            };
        }

    }
}
