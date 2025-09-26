using System.Windows.Media;

namespace CollectaMundo.DomainLogic.CardImages.Models
{
    public sealed class CardImageDto
    {
        public string? FrontImageUrl { get; init; }
        public string? BackImageUrl { get; init; }
        public ImageSource? FrontImageSource { get; init; }
        public ImageSource? BackImageSource { get; init; }
        public string? PromoLabel { get; init; }
        public string? SetLabel { get; init; }
    }

}
