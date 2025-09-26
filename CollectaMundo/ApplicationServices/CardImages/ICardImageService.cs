using CollectaMundo.DomainLogic.CardImages.Models;
using CollectaMundo.DomainLogic.CardLists.Models;

namespace CollectaMundo.ApplicationServices.CardImages
{
    public interface ICardImageService
    {
        Task<CardImageDto?> GetImageForCardAsync(CardSet card);
    }
}
