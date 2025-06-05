using CollectaMundo.ViewModels;

namespace CollectaMundo.ApplicationServices
{
    public interface ICardDatabasePreparationService
    {
        Task FirstTimeDbSetup(StatusViewModel statusVm);
        Task UpdateDb(StatusViewModel statusVm);             // Future
        Task UpdateCardPrices(StatusViewModel statusVm);     // Future
    }

}
