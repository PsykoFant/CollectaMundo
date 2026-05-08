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
                if (currentLine % 100 == 0)
                {
                    cancelToken.ThrowIfCancellationRequested();
                }

                string? line = await reader.ReadLineAsync(cancelToken);
                currentLine++;

                if (line is null)
                {
                    break; // or continue; but break is correct at EOF
                }

                if (IsEffectivelyEmptyCsvRow(line, delimiter))
                {
                    continue;
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
        private static bool IsEffectivelyEmptyCsvRow(string line, char delimiter)
        {
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                // Ignore delimiters, quotes, and whitespace
                if (c == delimiter || c == '"' || char.IsWhiteSpace(c))
                {
                    continue;
                }

                // Found real content
                return false;
            }

            // No meaningful characters found
            return true;
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
                    item.CsvFields["collectaMundoUuidImportField"] = uuids[0];
                    matchedUuid++;
                }
                else
                {
                    item.CsvFields["collectaMundoUuidsImportField"] = string.Join(",", uuids);
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
            return item.CsvFields.ContainsKey("collectaMundoUuidImportField") || item.CsvFields.ContainsKey("collectaMundoUuidsImportField");
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
                if (batch[i].CsvFields.ContainsKey("collectaMundoUuidImportField") || batch[i].CsvFields.ContainsKey("collectaMundoUuidsImportField"))
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
                if (batch[i].CsvFields.ContainsKey("collectaMundoUuidImportField") || batch[i].CsvFields.ContainsKey("collectaMundoUuidsImportField"))
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
                item.CsvFields.Remove("collectaMundoUuidsImportField");
                item.CsvFields["collectaMundoUuidImportField"] = list[0];
            }
            else if (list.Count > 1)
            {
                item.CsvFields.Remove("collectaMundoUuidImportField");
                item.CsvFields["collectaMundoUuidsImportField"] = string.Join(",", list);
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
                bool hasUuid = item.CsvFields.ContainsKey("collectaMundoUuidImportField");
                bool hasUuids = item.CsvFields.ContainsKey("collectaMundoUuidsImportField");

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
                ItemsWithMultipleUuids = items.Count(i => i.CsvFields.ContainsKey("collectaMundoUuidsImportField"))
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
                    match.CsvFields["collectaMundoUuidImportField"] = selection.SelectedUuid;
                    match.CsvFields.Remove("collectaMundoUuidsImportField"); // remove ambiguity marker
                }
            }

            var stillUnresolved = importCandidates.Count(i =>
                i.CsvFields.TryGetValue("collectaMundoUuidsImportField", out var uuids) &&
                !string.IsNullOrWhiteSpace(uuids));

            return new ImportMatchSummaryDto
            {
                ItemsWithMultipleUuids = stillUnresolved
            };
        }


        #endregion

        #region Step 10
        // ----------------------
        // Strict validation of mapped values against availability
        // ----------------------
        public IReadOnlyList<ResolvedImportItem> ResolveImportItems(IReadOnlyList<TempCardItem> items, IReadOnlyList<CsvFieldMapping> fieldMappings, IReadOnlyList<CsvValueMapping> conditionMappings, IReadOnlyList<CsvValueMapping> finishMappings, IReadOnlyList<CsvValueMapping> languageMappings)
        {
            var uuidHeader = "collectaMundoUuidImportField";
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

                if (!isImportable)
                {
                    warnings.Add("No UUID resolved for this row (cannot import). Check ID / Name+Set mapping steps.");
                }

                // Quantities
                var owned = ParseNonNegativeWholeNumberOrDefault(item, ownedHeader, defaultValue: CollectionCardItemDefaults.GetDefaultInt(ImportField.CardsOwned), warnings, "CardsOwned");

                var trade = ParseNonNegativeWholeNumberOrDefault(item, tradeHeader, defaultValue: CollectionCardItemDefaults.GetDefaultInt(ImportField.CardsForTrade), warnings, "CardsForTrade");

                // Additional fields mapped values (defaulting happens here, but will be validated strictly later)
                var condition = ResolveMappedValue(item, conditionHeader, conditionMappings) ?? CollectionCardItemDefaults.GetDefaultString(ImportField.Condition);

                var finish = ResolveMappedValue(item, finishHeader, finishMappings) ?? CollectionCardItemDefaults.GetDefaultString(ImportField.CardFinish);

                var language = ResolveMappedValue(item, languageHeader, languageMappings) ?? CollectionCardItemDefaults.GetDefaultString(ImportField.Language);

                var resolvedItem = new ResolvedImportItem
                {
                    TempItemImportKey = item.TempItemImportKey,
                    IsImportable = isImportable,
                    Uuid = uuid,
                    CardsOwned = owned,
                    CardsForTrade = trade,
                    Condition = condition,
                    Finish = finish,
                    Language = language
                };

                resolvedItem.AddWarnings(warnings);
                resolved.Add(resolvedItem);
            }

            return resolved;
        }
        public void ApplyStrictVariantValidation(IReadOnlyList<ResolvedImportItem> resolved, AvailabilityIndex availability)
        {
            foreach (var r in resolved)
            {
                if (!r.IsImportable || string.IsNullOrWhiteSpace(r.Uuid))
                {
                    continue;
                }

                if (!availability.BaseByUuid.TryGetValue(r.Uuid, out var baseAvail))
                {
                    MarkUnimportable(r, $"UUID not found in database: {r.Uuid}");
                    continue;
                }

                // -------------------------
                // Finish validation + auto-fix
                // -------------------------
                if (!IsFinishAvailable(baseAvail.FinishesCsv, r.Finish))
                {
                    var availableFinishes = GetAvailableFinishes(baseAvail.FinishesCsv);
                    var requestedFinish = r.Finish;

                    if (availableFinishes.Count == 1)
                    {
                        var only = availableFinishes.First();
                        r.OverwriteFinish(only);
                        r.AddWarning($"Finish '{requestedFinish ?? ""}' is not available; auto-selected '{only}' because it is the only available finish for this card.");
                    }
                    else
                    {
                        MarkUnimportable(r, $"Finish '{r.Finish ?? ""}' is not available for UUID {r.Uuid}.");
                        continue; // Once unimportable, no need to keep validating
                    }
                }

                // -------------------------
                // Language validation + auto-fix
                // -------------------------
                if (!IsLanguageAvailable(r.Uuid, r.Language, baseAvail.BaseLanguage, availability.ForeignLanguagesByUuid))
                {
                    var availableLangs = GetAvailableLanguages(baseAvail.BaseLanguage, r.Uuid, availability.ForeignLanguagesByUuid);
                    var requestedLanguage = r.Language;

                    if (availableLangs.Count == 1)
                    {
                        var only = availableLangs.First();
                        r.OverwriteLanguage(only);
                        r.AddWarning($"Language '{requestedLanguage ?? ""}' is not available; auto-selected '{only}' because it is the only available language for this card.");
                    }
                    else
                    {
                        MarkUnimportable(r, $"Language '{r.Language ?? ""}' is not available for UUID {r.Uuid}.");
                    }
                }
            }
        }

        // Helpers
        private static HashSet<string> GetAvailableFinishes(string? finishesCsv)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(finishesCsv))
            {
                return set;
            }

            foreach (var part in finishesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    set.Add(part);
                }
            }

            return set;
        }
        private static HashSet<string> GetAvailableLanguages(string? baseLanguage, string uuid, IReadOnlyDictionary<string, HashSet<string>> foreignByUuid)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(baseLanguage))
            {
                set.Add(baseLanguage);
            }

            if (foreignByUuid.TryGetValue(uuid, out var foreign) && foreign is { Count: > 0 })
            {
                foreach (var lang in foreign)
                {
                    set.Add(lang);
                }
            }

            return set;
        }
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
        private static bool IsFinishAvailable(string? finishesCsv, string? finish)
        {
            if (string.IsNullOrWhiteSpace(finish) || string.IsNullOrWhiteSpace(finishesCsv))
            {
                return false;
            }

            var parts = finishesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Any(p => string.Equals(p, finish, StringComparison.OrdinalIgnoreCase));
        }
        private static bool IsLanguageAvailable(string uuid, string? language, string? baseLanguage, IReadOnlyDictionary<string, HashSet<string>> foreignByUuid)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return false;
            }

            // English is never in cardForeignData, so English is valid iff baseLanguage is English
            if (string.Equals(language, "English", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(baseLanguage, "English", StringComparison.OrdinalIgnoreCase);
            }

            // Non-English valid if baseLanguage matches OR foreign contains it
            if (!string.IsNullOrWhiteSpace(baseLanguage) &&
                string.Equals(baseLanguage, language, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return foreignByUuid.TryGetValue(uuid, out var langs) && langs.Contains(language);
        }
        private static void MarkUnimportable(ResolvedImportItem item, string warning)
        {
            item.AddWarning(warning);
            item.IsImportable = false;
        }

        // ----------------------
        // Summary construction
        // ----------------------
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
                        : (int?)null,
                    Warnings = item.Warnings?.ToArray() ?? []
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

        // Helpers
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
        public string BuildUnimportableItemsCsv(IReadOnlyList<ResolvedImportItem> resolvedItems, IReadOnlyList<TempCardItem> importItems)
        {
            var sb = new StringBuilder();

            // Build lookup: key -> joined warnings (only for unimportable)
            var warningsByKey = resolvedItems
                .Where(r => !r.IsImportable)
                .ToDictionary(
                    r => r.TempItemImportKey,
                    r => r.Warnings is { Count: > 0 }
                        ? string.Join(" | ", r.Warnings)
                        : string.Empty);

            // Rows to export (original temp rows that are unimportable)
            var rows = importItems
                .Where(i => warningsByKey.ContainsKey(i.TempItemImportKey))
                .ToList();

            if (rows.Count == 0)
            {
                return string.Empty;
            }

            // Determine headers (original CSV headers + our warnings column)           
            var headers = rows.SelectMany(r => r.CsvFields.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Add warnings column (avoid collision if CSV already had that header)
            const string warningsHeaderBase = "CollectaMundoWarnings";
            var warningsHeader = warningsHeaderBase;
            var suffix = 2;
            while (headers.Contains(warningsHeader, StringComparer.OrdinalIgnoreCase))
            {
                warningsHeader = $"{warningsHeaderBase}_{suffix++}";
            }

            headers.Add(warningsHeader);

            // Header row
            sb.AppendLine(string.Join(";", headers.Select(ToCsvCell)));

            // Data rows
            foreach (var row in rows)
            {
                var values = headers.Select(h =>
                {
                    // Our appended warnings column
                    if (string.Equals(h, warningsHeader, StringComparison.OrdinalIgnoreCase))
                    {
                        return warningsByKey.TryGetValue(row.TempItemImportKey, out var w)
                            ? ToCsvCell(w)
                            : string.Empty;
                    }

                    // Original CSV columns
                    return row.CsvFields.TryGetValue(h, out var v)
                        ? ToCsvCell(v)
                        : string.Empty;
                });

                sb.AppendLine(string.Join(";", values));
            }

            return sb.ToString();
        }

        // Helpers
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

        // ----------------------
        // Collapse resolved items into upsert items, summing quantities of identical UUID+variant entries
        // ----------------------
        public IReadOnlyList<CollectionUpsertItem> CollapseResolvedItemsForCollection(IReadOnlyList<ResolvedImportItem> resolvedItems)
        {
            return
            [
                .. resolvedItems
            .Where(r => r.IsImportable)
            .Select(r => new
            {
                Identity = CollectionIdentityFactory.Create(
                    r.Uuid,
                    r.Condition ?? CollectionCardItemDefaults.GetDefaultString(ImportField.Condition),
                    r.Language ?? CollectionCardItemDefaults.GetDefaultString(ImportField.Language),
                    r.Finish ?? CollectionCardItemDefaults.GetDefaultString(ImportField.CardFinish),
                    locationId: null,
                    comment: CollectionCardItemDefaults.GetDefaultString(ImportField.Comment)),
                r.CardsOwned,
                r.CardsForTrade
            })
            .GroupBy(x => x.Identity)
            .Select(g =>
            {
                var ownedTotal = g.Sum(x => x.CardsOwned);
                var tradeTotal = g.Sum(x => x.CardsForTrade);

                if (tradeTotal > ownedTotal)
                {
                    tradeTotal = ownedTotal;
                }

                return new CollectionUpsertItem(
                    Uuid: g.Key.Uuid,
                    Language: g.Key.Language,
                    Finish: g.Key.Finish,
                    Condition: g.Key.Condition,
                    LocationId: g.Key.LocationId,
                    Comment: g.Key.Comment,
                    CardsOwned: ownedTotal,
                    CardsForTrade: tradeTotal
                );
            })
            ];
        }

        #endregion

    }
}
