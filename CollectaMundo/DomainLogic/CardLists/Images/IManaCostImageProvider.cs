using System.Windows.Media;

namespace CollectaMundo.DomainLogic.CardLists.Images
{
    public interface IManaCostImageProvider
    {
        ImageSource? GetImage(string? manaCostRaw);
        byte[]? GetBytes(string? manaCostRaw);
    }
}
