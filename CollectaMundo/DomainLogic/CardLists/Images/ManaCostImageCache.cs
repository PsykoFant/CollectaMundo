using System.Collections.Concurrent;
using System.Data.SQLite;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CollectaMundo.DomainLogic.CardLists.Images
{
    public sealed class ManaCostImageCache : IManaCostImageProvider
    {
        private readonly IReadOnlyDictionary<string, byte[]> _bytesByKey;
        private readonly ConcurrentDictionary<string, ImageSource?> _imgByKey = new();

        private ManaCostImageCache(IReadOnlyDictionary<string, byte[]> bytesByKey)
        {
            _bytesByKey = bytesByKey;
        }

        public static async Task<ManaCostImageCache> LoadAsync(SQLiteConnection conn)
        {
            // Load all unique bytes once. Keys match TEXT in uniqueManaCostImages.uniqueManaCost
            using var cmd = new SQLiteCommand(
                "SELECT uniqueManaCost, manaCostImage FROM uniqueManaCostImages", conn);

            var map = new Dictionary<string, byte[]>(capacity: 4096, comparer: System.StringComparer.Ordinal);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var key = rdr["uniqueManaCost"]?.ToString();
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (rdr["manaCostImage"] is byte[] blob)
                {
                    map[key] = blob; // share same reference for all cards with this key
                }
            }
            return new ManaCostImageCache(map);
        }

        public byte[]? GetBytes(string? manaCostRaw)
        {
            if (manaCostRaw is null) return null;
            return _bytesByKey.TryGetValue(manaCostRaw, out var b) ? b : null;
        }

        public ImageSource? GetImage(string? manaCostRaw)
        {
            if (manaCostRaw is null) return null;

            return _imgByKey.GetOrAdd(manaCostRaw, key =>
            {
                if (!_bytesByKey.TryGetValue(key, out var bytes) || bytes.Length == 0) return null;

                try
                {
                    using var ms = new MemoryStream(bytes);
                    var bmp = new BitmapImage();
                    ms.Position = 0;
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze(); // share across threads/UI
                    return bmp;
                }
                catch
                {
                    return null;
                }
            });
        }
    }
}
