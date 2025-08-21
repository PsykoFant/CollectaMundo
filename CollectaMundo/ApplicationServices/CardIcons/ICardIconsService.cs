using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardIcons
{
    public interface ICardIconsService
    {
        /// Ensures icon providers exist (no-op if already initialized).
        Task InitializeAsync(SQLiteConnection conn);

        // Optional exposure if someone else wants direct access:
        IImageProvider<string>? ManaCostImages { get; }
        IImageProvider<string>? SetIconImages { get; } // future
    }
}
