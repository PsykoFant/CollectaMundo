using CollectaMundo.Data.CardIcons;
using CollectaMundo.DomainLogic.CardIcons;
using CollectaMundo.DomainLogic.CardLists.Models;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardIcons
{
    public sealed class CardIconsService(ICardIconsRepo repo) : ICardIconsService
    {
        private readonly ICardIconsRepo _repo = repo;

        private readonly object _initLock = new();
        private bool _initialized;

        public IImageProvider<string>? ManaCostImages { get; private set; }
        public IImageProvider<string>? SetIconImages { get; private set; } // future

        public async Task InitializeAsync(SQLiteConnection conn)
        {
            if (_initialized)
            {
                return;
            }

            lock (_initLock)
            {
                if (_initialized)
                {
                    return;
                }
            }

            // Read using the supplied connection
            var manaMap = await _repo.ReadManaCostImagesAsync(conn);
            var manaBytes = new ManaCostBytesLogic(manaMap);
            var manaImgs = new ManaCostImageService(manaBytes);

            CardSet.ManaCostImages = manaImgs;
            ManaCostImages = manaImgs;

            // (future: set icons use same conn)

            lock (_initLock) { _initialized = true; }
        }

    }
}
