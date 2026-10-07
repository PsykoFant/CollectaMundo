using CollectaMundo.DomainLogic.Shared.Factories;
using CollectaMundo.Infrastructure.Shared.Models;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.Infrastructure.CardLists
{
    public class CardListRepo : ICardListRepo
    {
        public async Task<IReadOnlyList<PrintingCardDbRow>> ReadAllCardPrintingDbRowsAsync(SQLiteConnection conn)
        {
            const string query = """
                                 SELECT 
                                     ci.scryfallOracleId AS ScryfallOracleId,
                                     c.name              AS Name,
                                     c.setCode           AS SetCode,
                                     c.manaCost          AS ManaCost,
                                     c.types             AS Types,
                                     c.colors            AS Colors,
                                     c.supertypes        AS SuperTypes,
                                     c.subtypes          AS SubTypes,
                                     c.type              AS Type,
                                     c.keywords          AS Keywords,
                                     c.text              AS RulesText,
                                     c.manaValue         AS ManaValue,
                                     c.language          AS Language,
                                     c.uuid              AS Uuid,
                                     c.otherFaceIds      AS OtherFaceIds,
                                     c.availability      AS Availability,
                                     1                   AS GameplayCard,
                                     c.finishes          AS Finishes,
                                     c.side              AS Side,
                                     c.isPromo           AS IsPromo,
                                     c.rarity            AS Rarity
                                 FROM cards c
                                 LEFT JOIN cardIdentifiers ci
                                     ON ci.uuid = c.uuid

                                 UNION ALL

                                 SELECT 
                                     ti.scryfallOracleId AS ScryfallOracleId,
                                     t.name              AS Name,
                                     t.setCode           AS SetCode,
                                     t.manaCost          AS ManaCost,
                                     t.types             AS Types,
                                     t.colors            AS Colors,
                                     t.supertypes        AS SuperTypes,
                                     t.subtypes          AS SubTypes,
                                     t.type              AS Type,
                                     t.keywords          AS Keywords,
                                     t.text              AS RulesText,
                                     NULL                AS ManaValue,
                                     t.language          AS Language,
                                     t.uuid              AS Uuid,
                                     t.otherFaceIds      AS OtherFaceIds,
                                     t.availability      AS Availability,
                                     0                   AS GameplayCard,
                                     t.finishes          AS Finishes,
                                     t.side              AS Side,
                                     t.isPromo           AS IsPromo,
                                     NULL                AS Rarity
                                 FROM tokens t
                                 LEFT JOIN tokenIdentifiers ti
                                     ON ti.uuid = t.uuid
                                 """;

            using var cmd = new SQLiteCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var ordinals = PrintingCardOrdinals.FromReader(reader);
            var list = new List<PrintingCardDbRow>(capacity: 122000);

            var allocatedBefore =
    GC.GetAllocatedBytesForCurrentThread();

            long availabilityChars = 0;
            long colorsChars = 0;
            long finishesChars = 0;
            long keywordsChars = 0;
            long languageChars = 0;
            long manaCostRawChars = 0;
            long nameChars = 0;
            long otherFaceIdsChars = 0;
            long rarityChars = 0;
            long rulesTextChars = 0;
            long scryfallOracleIdChars = 0;
            long setCodeChars = 0;
            long sideChars = 0;
            long subTypesChars = 0;
            long superTypesChars = 0;
            long typeChars = 0;
            long typesChars = 0;
            long uuidChars = 0;

            var rowCount = 0;

            var readSw = Stopwatch.StartNew();

            while (reader.Read())
            {
                var row =
                    CardPrintingDbRowFromReader(
                        reader,
                        ordinals);

                list.Add(row);

                availabilityChars += row.Availability?.Length ?? 0;
                colorsChars += row.Colors?.Length ?? 0;
                finishesChars += row.Finishes?.Length ?? 0;
                keywordsChars += row.Keywords?.Length ?? 0;
                languageChars += row.Language?.Length ?? 0;
                manaCostRawChars += row.ManaCostRaw?.Length ?? 0;
                nameChars += row.Name?.Length ?? 0;
                otherFaceIdsChars += row.OtherFaceIds?.Length ?? 0;
                rarityChars += row.Rarity?.Length ?? 0;
                rulesTextChars += row.RulesText?.Length ?? 0;
                scryfallOracleIdChars += row.ScryfallOracleId?.Length ?? 0;
                setCodeChars += row.SetCode?.Length ?? 0;
                sideChars += row.Side?.Length ?? 0;
                subTypesChars += row.SubTypes?.Length ?? 0;
                superTypesChars += row.SuperTypes?.Length ?? 0;
                typeChars += row.Type?.Length ?? 0;
                typesChars += row.Types?.Length ?? 0;
                uuidChars += row.Uuid?.Length ?? 0;

                rowCount++;
            }

            readSw.Stop();

            var allocatedBytes =
                GC.GetAllocatedBytesForCurrentThread() -
                allocatedBefore;

            var allocatedMb =
                allocatedBytes / 1024d / 1024d;

            var bytesPerRow =
                rowCount == 0
                    ? 0
                    : allocatedBytes / rowCount;

            Debug.WriteLine(
                $"[Printing rows] {rowCount} rows: " +
                $"{readSw.ElapsedMilliseconds} ms, " +
                $"{allocatedMb:F1} MB allocated, " +
                $"{bytesPerRow:N0} bytes/row");

            Debug.WriteLine(
                $"[Printing payload] " +
                $"Availability={availabilityChars:N0}, " +
                $"Colors={colorsChars:N0}, " +
                $"Finishes={finishesChars:N0}, " +
                $"Keywords={keywordsChars:N0}, " +
                $"Language={languageChars:N0}, " +
                $"ManaCostRaw={manaCostRawChars:N0}, " +
                $"Name={nameChars:N0}, " +
                $"OtherFaceIds={otherFaceIdsChars:N0}, " +
                $"Rarity={rarityChars:N0}, " +
                $"RulesText={rulesTextChars:N0}, " +
                $"ScryfallOracleId={scryfallOracleIdChars:N0}, " +
                $"SetCode={setCodeChars:N0}, " +
                $"Side={sideChars:N0}, " +
                $"SubTypes={subTypesChars:N0}, " +
                $"SuperTypes={superTypesChars:N0}, " +
                $"Type={typeChars:N0}, " +
                $"Types={typesChars:N0}, " +
                $"Uuid={uuidChars:N0}");

            DebugPrintingStringCardinality(list);
            DebugOracleFieldConsistency(list);
            DebugOracleConflicts(list);
            DebugValueConsistency(
                list.GroupBy(row => (
                    OracleId: row.ScryfallOracleId ?? string.Empty,
                    Side: row.Side ?? string.Empty)),
                row => row.ManaValue,
                "ManaValue");

            return list;
        }
        private static void DebugValueConsistency<T>(IEnumerable<IGrouping<(string OracleId, string Side), PrintingCardDbRow>> groups, Func<PrintingCardDbRow, T> selector, string fieldName)
        {
            var conflictingGroups = 0;

            foreach (var group in groups)
            {
                var hasFirst = false;
                T? firstValue = default;

                foreach (var row in group)
                {
                    var value = selector(row);

                    if (!hasFirst)
                    {
                        firstValue = value;
                        hasFirst = true;
                        continue;
                    }

                    if (!EqualityComparer<T>.Default.Equals(
                            firstValue,
                            value))
                    {
                        conflictingGroups++;
                        break;
                    }
                }
            }

            Debug.WriteLine(
                $"[Oracle consistency] {fieldName}: " +
                $"conflicting groups={conflictingGroups:N0}");
        }
        private static void DebugOracleConflicts(IReadOnlyList<PrintingCardDbRow> rows)
        {
            var groups = rows.GroupBy(row => (
                OracleId: row.ScryfallOracleId ?? string.Empty,
                Side: row.Side ?? string.Empty));

            DebugConflicts(
                groups,
                row => row.Name,
                "Name");

            DebugConflicts(
                groups,
                row => row.RulesText,
                "RulesText");
        }
        private static void DebugConflicts(IEnumerable<IGrouping<(string OracleId, string Side), PrintingCardDbRow>> groups, Func<PrintingCardDbRow, string?> selector, string fieldName)
        {
            foreach (var group in groups)
            {
                var distinctValues = group
                    .Select(selector)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (distinctValues.Count <= 1)
                {
                    continue;
                }

                Debug.WriteLine(
                    $"[Oracle conflict] {fieldName}: " +
                    $"OracleId={group.Key.OracleId}, " +
                    $"Side='{group.Key.Side}', " +
                    $"rows={group.Count()}");

                foreach (var row in group)
                {
                    Debug.WriteLine(
                        $"    Uuid={row.Uuid}, " +
                        $"Set={row.SetCode}, " +
                        $"Language={row.Language}, " +
                        $"Name='{row.Name}', " +
                        $"RulesText='{row.RulesText}'");
                }
            }
        }
        private static void DebugOracleFieldConsistency(IReadOnlyList<PrintingCardDbRow> rows)
        {
            var byOracle = rows
                .GroupBy(row =>
                    row.ScryfallOracleId ?? string.Empty)
                .ToList();

            var byOracleAndSide = rows
                .GroupBy(row => (
                    OracleId: row.ScryfallOracleId ?? string.Empty,
                    Side: row.Side ?? string.Empty))
                .ToList();

            Debug.WriteLine(
                $"[Oracle consistency] " +
                $"ScryfallOracleId groups={byOracle.Count:N0}");

            DebugConsistency(
                byOracle,
                row => row.Name,
                "Name");

            DebugConsistency(
                byOracle,
                row => row.ManaCostRaw,
                "ManaCostRaw");

            DebugConsistency(
                byOracle,
                row => row.Colors,
                "Colors");

            DebugConsistency(
                byOracle,
                row => row.Keywords,
                "Keywords");

            DebugConsistency(
                byOracle,
                row => row.RulesText,
                "RulesText");

            DebugConsistency(
                byOracle,
                row => row.SuperTypes,
                "SuperTypes");

            DebugConsistency(
                byOracle,
                row => row.Types,
                "Types");

            DebugConsistency(
                byOracle,
                row => row.SubTypes,
                "SubTypes");

            DebugConsistency(
                byOracle,
                row => row.Type,
                "Type");


            Debug.WriteLine(
                $"[Oracle consistency] " +
                $"ScryfallOracleId+Side groups={byOracleAndSide.Count:N0}");

            DebugConsistency(
                byOracleAndSide,
                row => row.Name,
                "Name");

            DebugConsistency(
                byOracleAndSide,
                row => row.ManaCostRaw,
                "ManaCostRaw");

            DebugConsistency(
                byOracleAndSide,
                row => row.Colors,
                "Colors");

            DebugConsistency(
                byOracleAndSide,
                row => row.Keywords,
                "Keywords");

            DebugConsistency(
                byOracleAndSide,
                row => row.RulesText,
                "RulesText");

            DebugConsistency(
                byOracleAndSide,
                row => row.SuperTypes,
                "SuperTypes");

            DebugConsistency(
                byOracleAndSide,
                row => row.Types,
                "Types");

            DebugConsistency(
                byOracleAndSide,
                row => row.SubTypes,
                "SubTypes");

            DebugConsistency(
                byOracleAndSide,
                row => row.Type,
                "Type");
        }
        private static void DebugConsistency<TKey>(IEnumerable<IGrouping<TKey, PrintingCardDbRow>> groups, Func<PrintingCardDbRow, string?> selector, string fieldName)
        {
            var conflictingGroups = 0;

            foreach (var group in groups)
            {
                var hasFirst = false;
                string? firstValue = null;

                foreach (var row in group)
                {
                    var value = selector(row);

                    if (!hasFirst)
                    {
                        firstValue = value;
                        hasFirst = true;
                        continue;
                    }

                    if (!string.Equals(
                            firstValue,
                            value,
                            StringComparison.Ordinal))
                    {
                        conflictingGroups++;
                        break;
                    }
                }
            }

            Debug.WriteLine(
                $"[Oracle consistency] {fieldName}: " +
                $"conflicting groups={conflictingGroups:N0}");
        }
        private static void DebugPrintingStringCardinality(IReadOnlyList<PrintingCardDbRow> rows)
        {
            DebugField("Availability", rows, x => x.Availability);
            DebugField("Colors", rows, x => x.Colors);
            DebugField("Finishes", rows, x => x.Finishes);
            DebugField("Keywords", rows, x => x.Keywords);
            DebugField("Language", rows, x => x.Language);
            DebugField("ManaCostRaw", rows, x => x.ManaCostRaw);
            DebugField("Name", rows, x => x.Name);
            DebugField("OtherFaceIds", rows, x => x.OtherFaceIds);
            DebugField("Rarity", rows, x => x.Rarity);
            DebugField("RulesText", rows, x => x.RulesText);
            DebugField("ScryfallOracleId", rows, x => x.ScryfallOracleId);
            DebugField("SetCode", rows, x => x.SetCode);
            DebugField("Side", rows, x => x.Side);
            DebugField("SubTypes", rows, x => x.SubTypes);
            DebugField("SuperTypes", rows, x => x.SuperTypes);
            DebugField("Type", rows, x => x.Type);
            DebugField("Types", rows, x => x.Types);

            static void DebugField(
                string name,
                IReadOnlyList<PrintingCardDbRow> rows,
                Func<PrintingCardDbRow, string?> selector)
            {
                var populated = 0;

                var distinct =
                    new HashSet<string>(
                        StringComparer.Ordinal);

                foreach (var row in rows)
                {
                    var value = selector(row);

                    if (string.IsNullOrEmpty(value))
                    {
                        continue;
                    }

                    populated++;
                    distinct.Add(value);
                }

                Debug.WriteLine(
                    $"[Printing cardinality] {name}: " +
                    $"populated={populated:N0}, " +
                    $"distinct={distinct.Count:N0}, " +
                    $"duplicates={populated - distinct.Count:N0}");
            }
        }
        private readonly record struct PrintingCardOrdinals(
            int ScryfallOracleId,
            int Name,
            int SetCode,
            int ManaCost,
            int Types,
            int Colors,
            int SuperTypes,
            int SubTypes,
            int Type,
            int Keywords,
            int RulesText,
            int ManaValue,
            int Language,
            int Uuid,
            int OtherFaceIds,
            int Availability,
            int GameplayCard,
            int Finishes,
            int Side,
            int IsPromo,
            int Rarity)
        {
            public static PrintingCardOrdinals FromReader(DbDataReader reader)
            {
                return new PrintingCardOrdinals(
                    reader.GetOrdinal("ScryfallOracleId"),
                    reader.GetOrdinal("Name"),
                    reader.GetOrdinal("SetCode"),
                    reader.GetOrdinal("ManaCost"),
                    reader.GetOrdinal("Types"),
                    reader.GetOrdinal("Colors"),
                    reader.GetOrdinal("SuperTypes"),
                    reader.GetOrdinal("SubTypes"),
                    reader.GetOrdinal("Type"),
                    reader.GetOrdinal("Keywords"),
                    reader.GetOrdinal("RulesText"),
                    reader.GetOrdinal("ManaValue"),
                    reader.GetOrdinal("Language"),
                    reader.GetOrdinal("Uuid"),
                    reader.GetOrdinal("OtherFaceIds"),
                    reader.GetOrdinal("Availability"),
                    reader.GetOrdinal("GameplayCard"),
                    reader.GetOrdinal("Finishes"),
                    reader.GetOrdinal("Side"),
                    reader.GetOrdinal("IsPromo"),
                    reader.GetOrdinal("Rarity"));
            }
        }
        private static PrintingCardDbRow CardPrintingDbRowFromReader(DbDataReader reader, PrintingCardOrdinals ordinals)
        {
            return new PrintingCardDbRow
            {
                ScryfallOracleId = GetFieldValue<string>(reader, ordinals.ScryfallOracleId),
                Name = GetFieldValue<string>(reader, ordinals.Name),
                ManaCostRaw = GetFieldValue<string>(reader, ordinals.ManaCost),
                Colors = GetFieldValue<string>(reader, ordinals.Colors),
                Type = GetFieldValue<string>(reader, ordinals.Type),
                Types = GetFieldValue<string>(reader, ordinals.Types),
                SuperTypes = GetFieldValue<string>(reader, ordinals.SuperTypes),
                SubTypes = GetFieldValue<string>(reader, ordinals.SubTypes),
                Keywords = GetFieldValue<string>(reader, ordinals.Keywords),
                RulesText = GetFieldValue<string>(reader, ordinals.RulesText),
                Side = GetFieldValue<string>(reader, ordinals.Side),
                IsPromo = GetBooleanValue(reader, ordinals.IsPromo),
                OtherFaceIds = GetFieldValue<string>(reader, ordinals.OtherFaceIds),
                Availability = GetFieldValue<string>(reader, ordinals.Availability),
                GamePlayCard = GetFieldValue<int>(reader, ordinals.GameplayCard),
                ManaValue = GetFieldValue<double?>(reader, ordinals.ManaValue),
                Uuid = GetFieldValue<string>(reader, ordinals.Uuid),
                Language = GetFieldValue<string>(reader, ordinals.Language),
                SetCode = GetFieldValue<string>(reader, ordinals.SetCode),
                Rarity = GetFieldValue<string>(reader, ordinals.Rarity),
                Finishes = GetFieldValue<string>(reader, ordinals.Finishes)
            };
        }
        private static T? GetFieldValue<T>(DbDataReader reader, int ordinal)
        {
            var value = reader.GetValue(ordinal);

            if (value == DBNull.Value)
            {
                return default;
            }

            if (typeof(T) == typeof(int) &&
                value is long longValue)
            {
                return (T)(object)(int)longValue;
            }

            if (typeof(T) == typeof(int?) &&
                value is long nullableLongValue)
            {
                return (T)(object)(int?)nullableLongValue;
            }

            if (typeof(T) == typeof(double?) &&
                value is double doubleValue)
            {
                return (T)(object)(double?)doubleValue;
            }

            return (T)value;
        }
        private static bool GetBooleanValue(DbDataReader reader, int ordinal)
        {
            var value = reader.GetValue(ordinal);

            if (value == DBNull.Value)
            {
                return false;
            }

            if (value is bool boolValue)
            {
                return boolValue;
            }

            if (value is long longValue)
            {
                return longValue != 0;
            }

            return Convert.ToBoolean(value);
        }

        public async Task<List<CollectionCardDbRow>> ReadMyCollectionAsync(SQLiteConnection conn)
        {
            const string sql = """
                               SELECT
                                   id,
                                   uuid,
                                   cardsOwned,
                                   cardsForTrade,
                                   condition,
                                   language,
                                   finish,
                                   locationId,
                                   comment
                               FROM myCollection;
                               """;

            using var cmd = new SQLiteCommand(sql, conn);

            var list = new List<CollectionCardDbRow>();
            using var rdr = await cmd.ExecuteReaderAsync();

            while (await rdr.ReadAsync())
            {
                var uuid = rdr["uuid"]?.ToString() ?? throw new InvalidOperationException("uuid must not be null");
                var condition = rdr["condition"]?.ToString() ?? throw new InvalidOperationException("condition must not be null");
                var language = rdr["language"]?.ToString() ?? throw new InvalidOperationException("language must not be null");
                var finish = rdr["finish"]?.ToString() ?? throw new InvalidOperationException("finish must not be null");
                int? locationId = rdr["locationId"] == DBNull.Value ? null : rdr["locationId"] is long locationLong ? (int)locationLong : Convert.ToInt32(rdr["locationId"]);
                string? comment = rdr["comment"] == DBNull.Value ? null : rdr["comment"]?.ToString();
                list.Add(new CollectionCardDbRow
                {
                    CardId = rdr["id"] is long idLong
                        ? (int)idLong
                        : Convert.ToInt32(rdr["id"]),

                    Identity = CollectionIdentityFactory.Create(uuid, condition, language, finish, locationId, comment),

                    CardsOwned = rdr["cardsOwned"] is long ownedLong
                        ? (int)ownedLong
                        : Convert.ToInt32(rdr["cardsOwned"]),

                    CardsForTrade = rdr["cardsForTrade"] is long tradeLong
                        ? (int)tradeLong
                        : Convert.ToInt32(rdr["cardsForTrade"])
                });
            }

            return list;
        }
    }
}
