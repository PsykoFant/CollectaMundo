using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.Data.CardLists;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardLookups;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardLists
{

    public sealed class CardLookupsService(ICardLookupsRepo repo) : ICardLookupsService
    {
        private readonly ICardLookupsRepo _repo = repo;
        private readonly object _initLock = new();
        private bool _initialized;

        public IImageProvider<string>? ManaCostImages { get; private set; }
        public IImageProvider<string>? SetIconImages { get; private set; }

        public async Task InitializeAsync(SQLiteConnection conn, CardLookupsOptions opts)
        {
            if (_initialized) return;
            lock (_initLock) if (_initialized) return;

            // --- Icons  ---
            if (opts.HasFlag(CardLookupsOptions.Icons))
            {
                var manaMap = await _repo.ReadManaCostImagesAsync(conn);
                var manaBytes = new DictionaryBytesLogic<string>(manaMap);
                var manaImgs = new ImageProvider<string>(manaBytes);
                CardSet.ManaCostImages = manaImgs;
                ManaCostImages = manaImgs;

                var setMap = await _repo.ReadSetIconImagesAsync(conn);
                var setBytes = new DictionaryBytesLogic<string>(setMap);
                var setImgs = new ImageProvider<string>(setBytes);
                CardSet.SetIconImages = setImgs;
                SetIconImages = setImgs;
            }

            // --- Sets ---
            if (opts.HasFlag(CardLookupsOptions.Sets))
            {
                var sets = await _repo.ReadSetsAsync(conn); // code -> SetMeta
                var provider = new ValueProvider<string, SetMeta>(sets);
                CardSet.SetMetaProvider = provider;
                //SetMetaProvider = provider;
            }

            lock (_initLock) _initialized = true;
        }
    }
}
