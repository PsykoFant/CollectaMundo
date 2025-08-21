using CollectaMundo.DomainLogic.CardIcons;
using System.Windows.Media;

namespace CollectaMundo.ApplicationServices.CardIcons
{
    public interface IManaCostImageProvider : IImageProvider<string> { }
    public sealed class ManaCostImageService(IImageBytesLogic<string> bytes) : IManaCostImageProvider
    {
        private readonly IImageProvider<string> _inner = new ImageProvider<string>(bytes);

        public ImageSource? GetImage(string key) => _inner.GetImage(key);
    }
}
