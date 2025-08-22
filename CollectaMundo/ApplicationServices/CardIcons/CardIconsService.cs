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
        public IImageProvider<string>? SetIconImages { get; private set; }

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

            // --- Mana cost ---
            var manaMap = await _repo.ReadManaCostImagesAsync(conn);
            var manaBytes = new ManaCostBytesLogic(manaMap);
            var manaImgs = new ManaCostImageService(manaBytes);
            CardSet.ManaCostImages = manaImgs;
            ManaCostImages = manaImgs;

            // --- Set icons ---
            var setMap = await _repo.ReadSetIconImagesAsync(conn);
            var setBytes = new SetIconBytesLogic(setMap);
            var setImgs = new SetIconImageService(setBytes);
            CardSet.SetIconImages = setImgs;
            SetIconImages = setImgs;

            lock (_initLock) { _initialized = true; }
        }

    }
}
