using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.Data.CardLists;
using CollectaMundo.DomainLogic.CardLists.Models;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardLists
{

    public sealed class CardLookupsService(ICardLookupsRepo repo) : ICardLookupsService
    {
        private readonly ICardLookupsRepo _repo = repo;
        private readonly object _initLock = new();
        private bool _initialized;

        public async Task InitializeAsync(SQLiteConnection conn, CardLookupsOptions opts)
        {
            if (_initialized) return;
            lock (_initLock) if (_initialized) return;

            // --- Icons ---
            if (opts.HasFlag(CardLookupsOptions.Icons))
            {
                // Mana symbols
                var manaMap = await _repo.ReadManaCostImagesAsync(conn);
                CardSet.ManaCostImages = new ImageProvider<string>(new DictionaryByteSource<string>(manaMap));

                // Set icons
                var setMap = await _repo.ReadSetIconImagesAsync(conn);
                CardSet.SetIconImages = new ImageProvider<string>(new DictionaryByteSource<string>(setMap));

            }

            // --- Sets (metadata to avoid DB join in view) ---
            if (opts.HasFlag(CardLookupsOptions.Sets))
            {
                var setsDict = await _repo.ReadSetsAsync(conn);               // code -> SetMeta
                CardSet.SetMetaProvider = new ValueProvider<string, SetMeta>(setsDict);
            }

            lock (_initLock) _initialized = true;
        }
    }
}
