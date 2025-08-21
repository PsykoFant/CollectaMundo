using System.Data.SQLite;

namespace CollectaMundo.Data.CardIcons
{
    public class CardIconsRepo : ICardIconsRepo
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
    }
}
