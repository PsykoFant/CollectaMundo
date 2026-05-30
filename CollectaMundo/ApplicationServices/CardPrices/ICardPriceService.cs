using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardPrices
{
    public interface ICardPriceService
    {
        Task<PriceImportResult?> ImportPricesFromJsonAsync(string jsonPath, SQLiteConnection conn, SQLiteTransaction tx, IProgress<string>? statusProgress = null, IProgress<int>? percentProgress = null);
    }
}

