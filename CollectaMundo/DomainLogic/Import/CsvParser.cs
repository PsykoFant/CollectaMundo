using CollectaMundo.DomainLogic.Import.Models;
using System.IO;
using System.Text;

namespace CollectaMundo.DomainLogic.Import
{
    public class CsvParser : ICsvParser
    {
        public async Task<List<TempCardItem>> ParseCsvFileAsync(string filePath)
        {
            var cardItems = new List<TempCardItem>();
            var delimiter = ',';

            using var reader = new StreamReader(filePath, Encoding.UTF8);
            string? header = await reader.ReadLineAsync();
            if (header == null)
            {
                return cardItems;
            }

            if (header.Contains(';'))
            {
                delimiter = ';';
            }

            var headers = ParseCsvLine(header, delimiter);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
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
                item.Fields["CMImportKey"] = Guid.NewGuid().ToString();
                cardItems.Add(item);
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
    }
}
