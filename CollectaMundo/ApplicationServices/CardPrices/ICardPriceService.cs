using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardPrices
{
    public interface ICardPriceService
    {
        Task ImportPricesFromJsonAsync(string jsonPath, SQLiteConnection conn, IProgress<string>? statusProgress, IProgress<int>? percentProgress);
    }
}

