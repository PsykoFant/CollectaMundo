using CollectaMundo.DomainLogic.CardLists;
using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CollectaMundo.ApplicationServices.CardLists.CardLookups.Providers
{
    internal sealed class ImageProvider<TKey>(IByteSource<TKey> bytes) : ILookupProvider<TKey, ImageSource> where TKey : notnull
    {
        private readonly IByteSource<TKey> _bytes = bytes;
        private readonly ConcurrentDictionary<TKey, ImageSource?> _cache = new();
        public ImageSource? Get(TKey key)
        {
            return _cache.GetOrAdd(key, k =>
            {
                var data = _bytes.GetBytes(k);
                if (data is null || data.Length == 0)
                {
                    return null;
                }

                try
                {
                    using var ms = new MemoryStream(data);
                    var bmp = new BitmapImage();
                    ms.Position = 0;
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze(); // WPF requirement for cross-thread usage
                    return bmp;
                }
                catch
                {
                    return null;
                }
            });
        }
        public bool Contains(TKey key)
        {
            if (_cache.ContainsKey(key))
            {
                return true;
            }

            var data = _bytes.GetBytes(key);
            return data is { Length: > 0 };
        }
    }
}
