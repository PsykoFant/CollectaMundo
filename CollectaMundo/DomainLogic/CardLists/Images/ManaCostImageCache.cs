using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CollectaMundo.DomainLogic.CardLists.Images
{
    public sealed class ManaCostImageCache(IReadOnlyDictionary<string, byte[]> bytesByKey) : IManaCostImageProvider
    {
        private readonly IReadOnlyDictionary<string, byte[]> _bytesByKey = bytesByKey;
        private readonly ConcurrentDictionary<string, ImageSource?> _imgByKey = new();

        public byte[]? GetBytes(string? manaCostRaw) => manaCostRaw != null && _bytesByKey.TryGetValue(manaCostRaw, out var b) ? b : null;
        public ImageSource? GetImage(string? manaCostRaw)
        {
            if (manaCostRaw is null)
            {
                return null;
            }

            return _imgByKey.GetOrAdd(manaCostRaw, key =>
            {
                if (!_bytesByKey.TryGetValue(key, out var bytes) || bytes.Length == 0)
                {
                    return null;
                }

                try
                {
                    using var ms = new MemoryStream(bytes);
                    var bmp = new BitmapImage();
                    ms.Position = 0;
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
                catch { return null; }
            });
        }
    }
}
