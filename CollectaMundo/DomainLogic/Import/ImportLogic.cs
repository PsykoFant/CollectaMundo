using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.ViewModels.Models;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;

namespace CollectaMundo.DomainLogic.Import
{
    public class ImportLogic : IImportLogic
    {
        #region Step 1
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

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue; // skip empty CSV rows
                }

                var values = ParseCsvLine(line, delimiter);
                var item = new TempCardItem();

                for (int i = 0; i < headers.Count; i++)
                {
                    string cleaned = RemoveUnwantedPrefixes(values.Count > i ? values[i] : string.Empty);
                    item.CsvFields[headers[i]] = cleaned;
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

        #endregion

        #region Step 2
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

                if (!item.CsvFields.TryGetValue(selectedCsvHeader, out var csvValue) || string.IsNullOrWhiteSpace(csvValue))
                {
                    continue;
                }

                if (!idToUuids.TryGetValue(csvValue, out var uuids) || uuids == null || uuids.Count == 0)
                {
                    continue;
                }

                if (uuids.Count == 1)
                {
                    item.CsvFields["uuid"] = uuids[0];
                    matchedUuid++;
                }
                else
                {
                    item.CsvFields["uuids"] = string.Join(",", uuids);
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

        #endregion

        #region Step 3
        // Step 3
        public (bool HasName, bool HasSetName, bool HasSetCode, string? NameHeader, string? SetNameHeader, string? SetCodeHeader) ExtractMappedFields(IReadOnlyList<CsvFieldMapping> mappings)
        {
            string? name = mappings.FirstOrDefault(m => m.FieldToMap == ImportField.CardName)?.SelectedCsvHeader;
            string? setName = mappings.FirstOrDefault(m => m.FieldToMap == ImportField.SetName)?.SelectedCsvHeader;
            string? setCode = mappings.FirstOrDefault(m => m.FieldToMap == ImportField.SetCode)?.SelectedCsvHeader;

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
            return item.CsvFields.ContainsKey("uuid") || item.CsvFields.ContainsKey("uuids");
        }

        //  Apply matches — per item fallback logic

        public void ApplySetCodeMatches(IReadOnlyList<TempCardItem> batch, IReadOnlyList<(string Name, string SetCode)> pairs, Dictionary<string, List<string>> results)
        {
            for (int i = 0; i < batch.Count; i++)
            {
                var item = batch[i];
                var (name, setCode) = pairs[i];

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(setCode))
                {
                    continue;
                }

                string key = $"{name}_{setCode}".ToLowerInvariant();

                if (results.TryGetValue(key, out var list) && list != null && list.Count > 0)
                {
                    AssignUuids(item, list);
                }
            }
        }
        public void ApplySetNameMatches(IReadOnlyList<TempCardItem> batch, IReadOnlyList<(string Name, string SetName)> pairs, Dictionary<string, List<string>> results)
        {
            for (int i = 0; i < batch.Count; i++)
            {
                // skip items already matched by SetCode
                if (batch[i].CsvFields.ContainsKey("uuid") || batch[i].CsvFields.ContainsKey("uuids"))
                {
                    continue;
                }

                var item = batch[i];
                var (name, setName) = pairs[i];

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(setName))
                {
                    continue;
                }

                string key = $"{name}_{setName}".ToLowerInvariant();

                if (results.TryGetValue(key, out var list) && list != null && list.Count > 0)
                {
                    AssignUuids(item, list);
                }
            }
        }
        public void ApplyNameOnlyMatches(IReadOnlyList<TempCardItem> batch, IReadOnlyList<string> names, Dictionary<string, List<string>> results)
        {
            for (int i = 0; i < batch.Count; i++)
            {
                // skip items already matched by SetCode / SetName
                if (batch[i].CsvFields.ContainsKey("uuid") || batch[i].CsvFields.ContainsKey("uuids"))
                {
                    continue;
                }

                string name = names[i];
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                string key = name.ToLowerInvariant();

                if (results.TryGetValue(key, out var list) && list != null && list.Count > 0)
                {
                    AssignUuids(batch[i], list);
                }
            }
        }

        // Helper
        private static void AssignUuids(TempCardItem item, List<string> list)
        {
            if (list.Count == 1)
            {
                item.CsvFields.Remove("uuids");
                item.CsvFields["uuid"] = list[0];
            }
            else if (list.Count > 1)
            {
                item.CsvFields.Remove("uuid");
                item.CsvFields["uuids"] = string.Join(",", list);
            }
            // list.Count == 0 → no assignment
        }

        // Summary evaluation
        public ImportMatchSummaryDto FinalizeMatchResults(IReadOnlyList<TempCardItem> items)
        {
            bool anyBoth = false;
            bool anySingle = false;
            bool anyMulti = false;

            foreach (var item in items)
            {
                bool hasUuid = item.CsvFields.ContainsKey("uuid");
                bool hasUuids = item.CsvFields.ContainsKey("uuids");

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
            }

            if (anyBoth)
            {
                throw new InvalidOperationException("Internal invariant broken: item has both uuid and uuids.");
            }

            if (!anySingle && !anyMulti)
            {
                throw new InvalidOperationException("No matches were found using name, set name or set code.");
            }

            return new ImportMatchSummaryDto
            {
                ItemsWithMultipleUuids = items.Count(i => i.CsvFields.ContainsKey("uuids"))
            };
        }
        #endregion

        #region Step 4
        public ImportMatchSummaryDto ApplySelectedUuids(ObservableCollection<TempCardItem> importCandidates, List<MultipleUuidsItem> userSelections)
        {
            foreach (var selection in userSelections)
            {
                if (string.IsNullOrWhiteSpace(selection.SelectedUuid))
                {
                    continue;
                }

                // Match by first-class identity, not CSV fields
                var match = importCandidates
                    .FirstOrDefault(t => t.TempItemImportKey == selection.TempItemImportKey);

                if (match != null)
                {
                    match.CsvFields["uuid"] = selection.SelectedUuid;
                    match.CsvFields.Remove("uuids"); // remove ambiguity marker
                }
            }

            var stillUnresolved = importCandidates.Count(i =>
                i.CsvFields.TryGetValue("uuids", out var uuids) &&
                !string.IsNullOrWhiteSpace(uuids));

            return new ImportMatchSummaryDto
            {
                ItemsWithMultipleUuids = stillUnresolved
            };
        }


        #endregion

        #region Step 9
        public IReadOnlyList<ResolvedImportItem> ResolveImportItems(IReadOnlyList<TempCardItem> items, IReadOnlyList<CsvFieldMapping> fieldMappings, IReadOnlyList<CsvValueMapping> conditionMappings, IReadOnlyList<CsvValueMapping> finishMappings, IReadOnlyList<CsvValueMapping> languageMappings)
        {
            var uuidHeader = "uuid"; // already normalized earlier
            var ownedHeader = GetMappedHeader(fieldMappings, ImportField.CardsOwned);
            var tradeHeader = GetMappedHeader(fieldMappings, ImportField.CardsForTrade);
            var conditionHeader = GetMappedHeader(fieldMappings, ImportField.Condition);
            var finishHeader = GetMappedHeader(fieldMappings, ImportField.CardFinish);
            var languageHeader = GetMappedHeader(fieldMappings, ImportField.Language);

            var resolved = new List<ResolvedImportItem>(items.Count);

            foreach (var item in items)
            {
                var warnings = new List<string>();

                // UUID & importability
                item.CsvFields.TryGetValue(uuidHeader, out var uuid);
                var isImportable = !string.IsNullOrWhiteSpace(uuid);

                // Quantities
                var owned = ParseNonNegativeWholeNumberOrDefault(item, ownedHeader, defaultValue: CollectionCardItemDefaults.GetDefaultInt(ImportField.CardsOwned), warnings, "CardsOwned");
                var trade = ParseNonNegativeWholeNumberOrDefault(item, tradeHeader, defaultValue: CollectionCardItemDefaults.GetDefaultInt(ImportField.CardsForTrade), warnings, "CardsForTrade");

                // Additional fields mapped values
                var condition = ResolveMappedValue(item, conditionHeader, conditionMappings) ?? CollectionCardItemDefaults.GetDefaultString(ImportField.Condition);
                var finish = ResolveMappedValue(item, finishHeader, finishMappings) ?? CollectionCardItemDefaults.GetDefaultString(ImportField.CardFinish);
                var language = ResolveMappedValue(item, languageHeader, languageMappings) ?? CollectionCardItemDefaults.GetDefaultString(ImportField.Language);

                resolved.Add(new ResolvedImportItem
                {
                    TempItemImportKey = item.TempItemImportKey,
                    IsImportable = isImportable,
                    Uuid = uuid,
                    CardsOwned = owned,
                    CardsForTrade = trade,
                    Condition = condition,
                    Finish = finish,
                    Language = language,
                    Warnings = warnings
                });
            }

            return resolved;
        }
        public ImportSummary BuildImportSummary(IReadOnlyList<ResolvedImportItem> resolvedItems, IReadOnlyList<TempCardItem> tempItems, IReadOnlyList<CsvFieldMapping> nameSetMappings, IReadOnlyList<CsvFieldMapping> additionalFieldMappings, IReadOnlyList<CsvValueMapping> conditionMappings, IReadOnlyList<CsvValueMapping> finishMappings, IReadOnlyList<CsvValueMapping> languageMappings)

        {
            var summary = new ImportSummary();

            if (resolvedItems == null || resolvedItems.Count == 0)
            {
                return summary;
            }

            summary.TotalImportItems = resolvedItems.Count;

            // Precompute row numbers (1-based)
            var rowNumbersByKey = tempItems.Select((item, index) => new { item.TempItemImportKey, RowNumber = index + 1 }).ToDictionary(x => x.TempItemImportKey, x => x.RowNumber);

            // Resolve mapped headers once
            var cardNameHeader = GetMappedHeader(nameSetMappings, ImportField.CardName);
            var setNameHeader = GetMappedHeader(nameSetMappings, ImportField.SetName);
            var setCodeHeader = GetMappedHeader(nameSetMappings, ImportField.SetCode);

            foreach (var item in resolvedItems)
            {
                if (item.IsImportable)
                {
                    summary.ReadyToImportCount++;
                    summary.TotalCardsToAdd += item.CardsOwned;
                    continue;
                }

                summary.UnableToImportCount++;

                // Try to find original temp item
                var temp = tempItems.FirstOrDefault(t =>
                    t.TempItemImportKey == item.TempItemImportKey);

                summary.UnimportableItems.Add(new UnimportableItem
                {
                    TempItemImportKey = item.TempItemImportKey,
                    CardName = GetCsvValue(temp, cardNameHeader),
                    SetName = GetCsvValue(temp, setNameHeader),
                    SetCode = GetCsvValue(temp, setCodeHeader),
                    RowNumber = rowNumbersByKey.TryGetValue(item.TempItemImportKey, out var row)
                        ? row
                        : (int?)null
                });
            }

            // -----------------------------
            // Field mappings (Step 5)
            // -----------------------------
            summary.FieldMappings =
            [
                .. additionalFieldMappings.Select(m =>
                    !string.IsNullOrWhiteSpace(m.SelectedCsvHeader)
                        ? new FieldMappingSummary(m.FieldToMap, m.SelectedCsvHeader!)
                        : new FieldMappingSummary(
                            m.FieldToMap,
                            $"{CollectionCardItemDefaults.GetDefaultDisplayValue(m.FieldToMap)} (default value)")
                )
            ];

            // -----------------------------
            // Value mappings (Steps 6–8)
            // -----------------------------
            var valueMappings = new List<ValueMappingSummary>();

            AddValueMappingsIfFieldMapped(ImportField.Condition, conditionMappings);
            AddValueMappingsIfFieldMapped(ImportField.CardFinish, finishMappings);
            AddValueMappingsIfFieldMapped(ImportField.Language, languageMappings);

            // If NONE of the value-mapped fields are mapped to a CSV column, show a single explanatory row so the grid isn't empty.
            var anyValueFieldMapped =
                additionalFieldMappings.Any(m =>
                    (m.FieldToMap == ImportField.Condition ||
                     m.FieldToMap == ImportField.CardFinish ||
                     m.FieldToMap == ImportField.Language) &&
                    !string.IsNullOrWhiteSpace(m.SelectedCsvHeader));

            if (!anyValueFieldMapped)
            {
                valueMappings.Add(new ValueMappingSummary(
                    ImportField.None,
                    CsvValue: "—",
                    MappedValue: "All values use defaults"));
            }

            void AddValueMappingsIfFieldMapped(ImportField field, IReadOnlyList<CsvValueMapping> mappings)
            {
                var fieldMapping = additionalFieldMappings
                    .FirstOrDefault(m => m.FieldToMap == field && !string.IsNullOrWhiteSpace(m.SelectedCsvHeader));

                if (fieldMapping is null)
                {
                    return;
                }

                var defaultValue = CollectionCardItemDefaults.GetDefaultDisplayValue(field);

                // Case 4: mapped field, but CSV column contained no values
                if (mappings.Count == 0)
                {
                    valueMappings.Add(new ValueMappingSummary(field, $"(no values in '{fieldMapping.SelectedCsvHeader}')", $"(default -> {defaultValue})"));

                    return;
                }

                // Cases 2 & 3
                valueMappings.AddRange(
                    mappings.Select(m =>
                    {
                        var isBlank = string.IsNullOrWhiteSpace(m.SelectedCardSetValue);

                        return new ValueMappingSummary(
                            field,
                            m.CsvValue,
                            isBlank
                                ? $"(blank -> {defaultValue})"
                                : m.SelectedCardSetValue!);
                    }));
            }

            summary.ValueMappings = valueMappings;

            return summary;
        }
        public string BuildUnimportableItemsCsv(IReadOnlyList<ResolvedImportItem> resolvedItems, IReadOnlyList<TempCardItem> importItems)
        {
            var sb = new StringBuilder();

            // Identify unimportable rows using FINAL import decision
            var unimportableKeys = resolvedItems.Where(r => !r.IsImportable).Select(r => r.TempItemImportKey).ToHashSet();

            var rows = importItems.Where(i => unimportableKeys.Contains(i.TempItemImportKey)).ToList();

            if (rows.Count == 0)
            {
                return string.Empty;
            }

            // Preserve original column order as best as possible
            var headers = rows.First().CsvFields.Keys.ToList();

            // Header row
            sb.AppendLine(string.Join(";", headers.Select(ToCsvCell)));

            // Data rows
            foreach (var row in rows)
            {
                var values = headers.Select(h =>
                    row.CsvFields.TryGetValue(h, out var v)
                        ? ToCsvCell(v)
                        : string.Empty);

                sb.AppendLine(string.Join(";", values));
            }

            return sb.ToString();
        }
        public IReadOnlyList<CollectionUpsertItem> CollapseResolvedItemsForCollection(IReadOnlyList<ResolvedImportItem> resolvedItems)
        {
            return [.. resolvedItems
                .Where(r => r.IsImportable && !string.IsNullOrWhiteSpace(r.Uuid))
                .Select(r => new
                {
                    Uuid = r.Uuid!, // already checked
                    Language = r.Language ?? CollectionCardItemDefaults.GetDefaultString(ImportField.Language),
                    Finish = r.Finish ?? CollectionCardItemDefaults.GetDefaultString(ImportField.CardFinish),
                    Condition = r.Condition ?? CollectionCardItemDefaults.GetDefaultString(ImportField.Condition),
                    r.CardsOwned,
                    r.CardsForTrade
                })
                .GroupBy(r => new
                {
                    r.Uuid,
                    r.Language,
                    r.Finish,
                    r.Condition
                })
                .Select(g => new CollectionUpsertItem(
                    Uuid: g.Key.Uuid,
                    Language: g.Key.Language,
                    Finish: g.Key.Finish,
                    Condition: g.Key.Condition,
                    CardsOwned: g.Sum(x => x.CardsOwned),
                    CardsForTrade: g.Sum(x => x.CardsForTrade)
                ))];
        }

        // Helpers
        private static string? ResolveMappedValue(TempCardItem item, string? csvHeader, IReadOnlyList<CsvValueMapping> mappings)
        {
            if (string.IsNullOrWhiteSpace(csvHeader))
            {
                return null;
            }

            if (!item.CsvFields.TryGetValue(csvHeader, out var raw))
            {
                return null;
            }

            var mapping = mappings.FirstOrDefault(m =>
                string.Equals(m.CsvValue, raw, StringComparison.OrdinalIgnoreCase));

            return mapping?.SelectedCardSetValue;
        }
        private static string? GetMappedHeader(IReadOnlyList<CsvFieldMapping> mappings, ImportField field)
        {
            return mappings.FirstOrDefault(m => m.FieldToMap == field)?.SelectedCsvHeader;
        }
        private static int ParseNonNegativeWholeNumberOrDefault(TempCardItem item, string? header, int defaultValue, List<string> warnings, string warningContext)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                return defaultValue;
            }

            if (!item.CsvFields.TryGetValue(header, out var raw) ||
                string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            var trimmed = raw.Trim();

            // Reject multiple separators
            if (trimmed.Count(c => c == '.' || c == ',') > 1)
            {
                warnings.Add($"{warningContext}: invalid number '{raw}', defaulted to {defaultValue}");
                return defaultValue;
            }

            var normalized = trimmed.Replace(',', '.');

            if (!decimal.TryParse(
                    normalized,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                warnings.Add($"{warningContext}: invalid number '{raw}', defaulted to {defaultValue}");
                return defaultValue;
            }

            if (value < 0 || value != decimal.Truncate(value))
            {
                warnings.Add($"{warningContext}: non-whole number '{raw}', defaulted to {defaultValue}");
                return defaultValue;
            }

            return (int)value;
        }
        private static string GetCsvValue(TempCardItem? item, string? header, string fallback = "Unknown")
        {
            if (item == null || string.IsNullOrWhiteSpace(header))
            {
                return fallback;
            }

            return item.CsvFields.TryGetValue(header, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : fallback;
        }
        private static string ToCsvCell(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var needsQuotes =
                value.Contains(' ') ||
                value.Contains(';') ||
                value.Contains('"') ||
                value.Contains('\n') ||
                value.Contains('\r');

            if (!needsQuotes)
            {
                return value;
            }

            var escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        #endregion

    }
}
