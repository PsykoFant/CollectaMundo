namespace CollectaMundo.ApplicationServices.CardIcons
{
    public interface IManaCostImageProvider : IImageProvider<string> { }
    public sealed class ManaCostImageService(CollectaMundo.DomainLogic.CardIcons.IImageBytesLogic<string> bytes) : ImageProvider<string>(bytes), IManaCostImageProvider
    {
    }
}
