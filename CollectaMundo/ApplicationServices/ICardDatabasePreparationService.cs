using CollectaMundo.ApplicationServices.Utilities;
namespace CollectaMundo.ApplicationServices
{
    public interface ICardDatabasePreparationService
    {
        Task<OperationResult> FirstTimeDbPrepOrchetrator(int defaultDelay = 3000);
    }

}
