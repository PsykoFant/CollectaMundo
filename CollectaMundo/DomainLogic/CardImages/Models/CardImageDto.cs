namespace CollectaMundo.DomainLogic.CardImages.Models
{
    public sealed class CardImageDto
    {
        public string? Uuid { get; init; }
        public string? FrontImageUrl { get; init; }
        public string? BackImageUrl { get; init; }
        public string? PromoLabel { get; init; }
        public string? SetLabel { get; init; }
    }

}
