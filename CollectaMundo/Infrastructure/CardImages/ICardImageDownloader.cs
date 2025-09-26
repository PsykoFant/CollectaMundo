using System.Windows.Media.Imaging;

namespace CollectaMundo.Infrastructure.CardImages
{
    public interface ICardImageDownloader
    {
        Task<BitmapImage?> DownloadAsync(string url);
    }

}
