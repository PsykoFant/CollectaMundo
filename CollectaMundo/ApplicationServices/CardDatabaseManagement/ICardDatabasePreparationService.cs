using CollectaMundo.ApplicationServices.Utilities;
namespace CollectaMundo.ApplicationServices.CardDatabaseManagement
{
    public interface ICardDatabasePreparationService
    {
        Task<OperationResult> FirstTimeDbPrepOrchetrator(int defaultDelay = 3000);
        Task<OperationResult> CheckForDbUpdatesAsync();
        Task<OperationResult> UpdateDbPrepOrchetrator(int defaultDelay = 3000);
    }

}
