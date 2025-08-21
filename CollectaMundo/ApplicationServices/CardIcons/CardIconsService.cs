using CollectaMundo.Data.CardIcons;
using CollectaMundo.DomainLogic.CardIcons;
using CollectaMundo.DomainLogic.CardLists.Models;

namespace CollectaMundo.ApplicationServices.CardIcons
{
    public sealed class CardIconsService(ICardIconsRepo repo, Func<UnitOfWork> uowFactory) : ICardIconsService
    {
        private readonly ICardIconsRepo _repo = repo;
        private readonly Func<UnitOfWork> _uowFactory = uowFactory; // simple factory (no DI container needed)

        private readonly object _initLock = new();
        private bool _initialized;

        public IImageProvider<string>? ManaCostImages { get; private set; }
        public IImageProvider<string>? SetIconImages { get; private set; } // future

        public CardIconsService(ICardIconsRepo repo)
            : this(repo, () => new UnitOfWork()) { }

        public async Task InitializeAsync()
        {
            if (_initialized)
            {
                return;
            }

            // double-checked locking to avoid races on refresh
            lock (_initLock)
            {
                if (_initialized)
                {
                    return;
                }
            }

            await using var uow = _uowFactory();
            await uow.BeginAsync();
            var conn = uow.CurrentConnection;

            // --- Mana cost icons ---
            var manaMap = await _repo.ReadManaCostImagesAsync(conn);
            var manaBytes = new ManaCostBytesLogic(manaMap);    // Domain bytes
            var manaImgs = new ManaCostImageService(manaBytes); // AppServices decode/cache

            // Hook into CardSet statics once (global providers)
            CardSet.ManaCostImages = manaImgs;

            // Save references (optional public exposure)
            ManaCostImages = manaImgs;

            // --- (future) set icons ---
            // var setMap   = await _repo.ReadSetIconImagesAsync(conn);
            // var setBytes = new SetIconBytesLogic(setMap);
            // var setImgs  = new SetIconImageService(setBytes);
            // CardSet.SetIconImages = setImgs;
            // SetIconImages = setImgs;

            await uow.CommitAsync();

            lock (_initLock)
            {
                _initialized = true;
            }
        }
    }
}
