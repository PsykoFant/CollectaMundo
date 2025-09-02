using CollectaMundo.ApplicationServices.CardLists.Lookups.Providers;
using CollectaMundo.ApplicationServices.CardLists.Lookups.Sources;
using CollectaMundo.Data.CardLists;
using CollectaMundo.DomainLogic.CardLists.Models;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardLists.Lookups
{

    public sealed class CardLookupsService(ICardLookupsRepo repo, Func<string> getRetailer) : ICardLookupsService
    {
        private readonly ICardLookupsRepo _repo = repo;
        private readonly object _initLock = new();
        private bool _initialized;

        // NEW: delegate to read the current retailer key (e.g., "cardmarket")
        private readonly Func<string> _getRetailer = getRetailer;

        public async Task InitializeAsync(SQLiteConnection conn, CardLookupsOptions opts)
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

            if (opts.HasFlag(CardLookupsOptions.Icons))
            {
                var manaMap = await _repo.ReadManaCostImagesAsync(conn);
                CardSet.ManaCostImages = new ImageProvider<string>(new DictionaryByteSource<string>(manaMap));

                var setMap = await _repo.ReadSetIconImagesAsync(conn);
                CardSet.SetIconImages = new ImageProvider<string>(new DictionaryByteSource<string>(setMap));
            }

            if (opts.HasFlag(CardLookupsOptions.Sets))
            {
                var setsDict = await _repo.ReadSetsAsync(conn);
                CardSet.SetMetaProvider = new ValueProvider<string, SetDto>(setsDict);
            }

            if (opts.HasFlag(CardLookupsOptions.Prices))
            {
                var retailerKey = _getRetailer();
                await ReloadPricesAsync(conn, retailerKey);
            }

            lock (_initLock)
            {
                _initialized = true;
            }
        }

        public async Task ReloadPricesAsync(SQLiteConnection conn, string retailerKey)
        {
            // Load the new map for the requested retailer
            var dict = await _repo.ReadPricesAsync(conn, retailerKey);

            // Swap the static provider (all CardSet getters read through this)
            CardSet.PriceMetaProvider = new ValueProvider<string, PriceDto>(dict);
        }
    }
}
