namespace CollectaMundo.ApplicationServices
{
    public interface ICardDatabasePreparationService
    {
        Task FirstTimeDbSetup();
        Task UpdateDb();             // Future
        Task UpdateCardPrices();     // Future
    }

}
