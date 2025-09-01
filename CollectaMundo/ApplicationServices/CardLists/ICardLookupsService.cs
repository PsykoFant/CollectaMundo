using CollectaMundo.ApplicationServices.Utilities;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardLists
{
    public interface ICardLookupsService
    {
        /// Ensures data providers exist (no-op if already initialized).
        Task InitializeAsync(SQLiteConnection conn, CardLookupsOptions opts);

        // Optional exposure if someone else wants direct access:
        IImageProvider<string>? ManaCostImages { get; }
        IImageProvider<string>? SetIconImages { get; }
    }
}
