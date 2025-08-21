using System.Windows.Media;

namespace CollectaMundo.ApplicationServices.CardIcons
{
    public interface IImageProvider<TKey>
    {
        ImageSource? GetImage(TKey key);
    }
}
