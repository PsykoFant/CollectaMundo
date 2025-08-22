using CollectaMundo.DomainLogic.CardIcons;
using System.Windows.Media;

namespace CollectaMundo.ApplicationServices.CardIcons
{
    public interface IManaCostImageProvider : IImageProvider<string> { }
    public interface ISetIconImageProvider : IImageProvider<string> { }

    // Use composition over inheritance to keep ImageProvider<TKey> sealed
    internal sealed class ManaCostImageService(IImageBytesLogic<string> bytes) : IManaCostImageProvider
    {
        private readonly IImageProvider<string> _inner = new ImageProvider<string>(bytes);

        public ImageSource? GetImage(string key) => _inner.GetImage(key);
    }

    internal sealed class SetIconImageService(IImageBytesLogic<string> bytes) : ISetIconImageProvider
    {
        private readonly IImageProvider<string> _inner = new ImageProvider<string>(bytes);

        public ImageSource? GetImage(string key) => _inner.GetImage(key);
    }
}
