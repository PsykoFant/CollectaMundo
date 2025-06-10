using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices
{
    public interface ICardPriceImporter
    {
        Task ImportPricesFromJsonAsync(string jsonPath, SQLiteConnection conn);
    }
}

