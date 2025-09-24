using CollectaMundo.DomainLogic.CardImages.Models;

namespace CollectaMundo.ApplicationServices.CardImages
{
    public interface ICardImageService
    {
        Task<CardImageDto?> GetImageForCardAsync(string? uuid, string? name);
    }
}
