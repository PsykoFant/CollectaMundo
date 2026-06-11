using CollectaMundo.DomainLogic.CardImages.Models;
using CollectaMundo.DomainLogic.CardImages.Models.CollectaMundo.DomainLogic.CardImages.Models;

namespace CollectaMundo.ApplicationServices.CardImages
{
    public interface ICardImageService
    {
        Task<CardImageDto?> GetImageForCardAsync(CardImageRequest request);
    }
}
