namespace CollectaMundo.DomainLogic.CardImages.Models
{
    public sealed class CardImageDto
    {
        public string? FrontImageUrl { get; init; }
        public string? BackImageUrl { get; set; }
        public string? PromoLabel { get; init; }
        public string? SetLabel { get; init; }
    }

}
