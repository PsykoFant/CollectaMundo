using CollectaMundo.DomainLogic.CardLists.Models;
using System.Data.SQLite;

namespace CollectaMundo.Data.CardLists
{
    public class CardLookupsRepo : ICardLookupsRepo
    {
        public async Task<IReadOnlyDictionary<string, byte[]>> ReadManaCostImagesAsync(SQLiteConnection conn)
        {
            using var cmd = new SQLiteCommand("SELECT uniqueManaCost, manaCostImage FROM uniqueManaCostImages", conn);

            var map = new Dictionary<string, byte[]>(capacity: 4096, comparer: StringComparer.Ordinal);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var key = rdr["uniqueManaCost"]?.ToString();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (rdr["manaCostImage"] is byte[] blob)
                {
                    map[key] = blob;
                }
            }
            return map;
        }
        public async Task<IReadOnlyDictionary<string, byte[]>> ReadSetIconImagesAsync(SQLiteConnection conn)
        {
            var map = new Dictionary<string, byte[]>(capacity: 4096, comparer: StringComparer.Ordinal);
            const string sql = "SELECT setCode, keyruneImage FROM keyruneImages";

            using var cmd = new SQLiteCommand(sql, conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var key = rdr["setCode"]?.ToString();
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (rdr["keyruneImage"] is byte[] blob && blob.Length > 0)
                    map[key] = blob;
            }
            return map;
        }
        public async Task<IReadOnlyDictionary<string, SetMeta>> ReadSetsAsync(SQLiteConnection conn)
        {
            var map = new Dictionary<string, SetMeta>(capacity: 1024, StringComparer.OrdinalIgnoreCase);
            const string sql = "SELECT code, name, releaseDate FROM sets";

            using var cmd = new SQLiteCommand(sql, conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var code = rdr["code"]?.ToString();
                if (string.IsNullOrWhiteSpace(code)) continue;

                var name = rdr["name"]?.ToString() ?? "";
                DateTime? release = null;
                if (DateTime.TryParse(rdr["releaseDate"]?.ToString(), out var dt))
                    release = dt;

                map[code] = new SetMeta { Code = code, Name = name, ReleaseDate = release };
            }

            return map;
        }
    }
}

