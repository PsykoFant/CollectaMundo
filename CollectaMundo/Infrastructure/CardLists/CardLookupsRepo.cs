using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardPrices;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;

namespace CollectaMundo.Infrastructure.CardLists
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
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (rdr["keyruneImage"] is byte[] blob && blob.Length > 0)
                {
                    map[key] = blob;
                }
            }
            return map;
        }
        public async Task<IReadOnlyDictionary<string, SetDto>> ReadSetsAsync(SQLiteConnection conn)
        {
            var map = new Dictionary<string, SetDto>(capacity: 1024, StringComparer.OrdinalIgnoreCase);
            const string sql = "SELECT code, tokenSetCode, name, releaseDate FROM sets";

            using var cmd = new SQLiteCommand(sql, conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var code = rdr["code"]?.ToString();
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                var tokenCode = rdr["tokenSetCode"]?.ToString() ?? "";

                var name = rdr["name"]?.ToString() ?? "";
                DateTime? release = null;
                if (DateTime.TryParse(rdr["releaseDate"]?.ToString(), out var dt))
                {
                    release = dt;
                }

                map[code] = new SetDto { Code = code, TokenCode = tokenCode, Name = name, ReleaseDate = release };
            }

            Debug.WriteLine($"Loaded {map.Count} sets from database.");

            return map;
        }
        public async Task<IReadOnlyDictionary<string, PriceDto>> ReadPricesAsync(SQLiteConnection conn, string retailer, string format = "paper")
        {
            ArgumentNullException.ThrowIfNull(conn);
            if (string.IsNullOrWhiteSpace(retailer))
            {
                throw new ArgumentException("Retailer required.", nameof(retailer));
            }

            // 1) Normalize & validate retailer against your known set for the format
            var normalized = retailer.Trim().ToLowerInvariant();

            if (!CardPriceDefinitions.RetailersByFormat.TryGetValue(format, out var allowed))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(retailer),
                    retailer,
                    $"Unsupported format '{format}'. Known formats: {string.Join(", ", CardPriceDefinitions.RetailersByFormat.Keys)}"
                );
            }

            // 2) Build validated column identifiers and alias them
            string prefix = normalized; // columns are like "cardmarketNormal", "cardmarketFoil", "cardmarketEtched"
            string sql = $@"
            SELECT uuid,
                {prefix}Normal AS Normal,
                {prefix}Foil   AS Foil,
                {prefix}Etched AS Etched
            FROM cardPrices;";

            var map = new Dictionary<string, PriceDto>(capacity: 1024, StringComparer.OrdinalIgnoreCase);

            using var cmd = new SQLiteCommand(sql, conn);
            using var rdr = await cmd.ExecuteReaderAsync();

            int ordUuid = rdr.GetOrdinal("uuid");
            int ordNormal = rdr.GetOrdinal("Normal");
            int ordFoil = rdr.GetOrdinal("Foil");
            int ordEtched = rdr.GetOrdinal("Etched");

            while (await rdr.ReadAsync())
            {
                if (rdr.IsDBNull(ordUuid))
                {
                    continue;
                }

                var uuid = rdr.GetString(ordUuid);
                if (string.IsNullOrWhiteSpace(uuid))
                {
                    continue;
                }

                var dto = new PriceDto
                {
                    Uuid = uuid,
                    NormalPrice = ReadNullableDecimal(rdr, ordNormal),
                    FoilPrice = ReadNullableDecimal(rdr, ordFoil),
                    EtchedPrice = ReadNullableDecimal(rdr, ordEtched),
                };

                map[uuid] = dto;
            }

            return map;
        }
        private static decimal? ReadNullableDecimal(DbDataReader rdr, int ordinal)
        {
            if (ordinal < 0 || rdr.IsDBNull(ordinal))
            {
                return null;
            }

            object v = rdr.GetValue(ordinal);

            return v switch
            {
                decimal d => d,
                double d => (decimal)d,
                float f => (decimal)f,
                long l => l,
                int i => i,
                string s => decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null,
                _ => null
            };
        }

    }
}

