namespace CollectaMundo.ApplicationServices.CardImages.Models
{
    public interface ICardImageSourceCard
    {
        string? Uuid { get; }
        string? Name { get; }
        string? Side { get; }
    }
}
