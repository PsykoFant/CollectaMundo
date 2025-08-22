using CollectaMundo.DomainLogic.CardIcons;
using System.Windows.Media;

namespace CollectaMundo.ApplicationServices.CardIcons
{
    public interface ISetIconImageProvider : IImageProvider<string> { }
    public sealed class SetIconImageService(IImageBytesLogic<string> bytes) : ISetIconImageProvider
    {
        private readonly IImageProvider<string> _inner = new ImageProvider<string>(bytes);

        public ImageSource? GetImage(string key) => _inner.GetImage(key);
    }
}
