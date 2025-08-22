using CollectaMundo.DomainLogic.CardIcons;
using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CollectaMundo.ApplicationServices.CardIcons
{
    public sealed class ImageProvider<TKey>(IImageBytesLogic<TKey> bytes) : IImageProvider<TKey> where TKey : notnull
    {
        private readonly IImageBytesLogic<TKey> _bytes = bytes;
        private readonly ConcurrentDictionary<TKey, ImageSource?> _cache = new();

        public ImageSource? GetImage(TKey key)
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
                    bmp.Freeze();
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
