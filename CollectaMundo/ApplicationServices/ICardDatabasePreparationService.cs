namespace CollectaMundo.ApplicationServices
{
    public interface ICardDatabasePreparationService
    {
        Task FirstTimeDbPrepOrchetrator();
        Task UpdateDb();             // Future
        Task UpdateCardPrices();     // Future
    }

}
