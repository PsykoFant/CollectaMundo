using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardPrices
{
    public interface ICardPriceService
    {
        Task ImportPricesFromJsonAsync(string jsonPath, SQLiteConnection conn);
    }
}

