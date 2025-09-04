using CollectaMundo.ApplicationServices.CardLists.Lookups.Providers;
using CollectaMundo.Data.CardLists;
using CollectaMundo.DomainLogic.CardLists.Lookups;
using CollectaMundo.DomainLogic.CardLists.Models;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardLists.Lookups
{

    public sealed class CardLookupsService(CardLookupsRepo repo, CardLookupBuilder builder, Func<string> getRetailer) : ICardLookupsService
    {
        private readonly CardLookupsRepo _repo = repo;
        private readonly CardLookupBuilder _builder = builder;
        private readonly Func<string> _getRetailer = getRetailer;
        public async Task<CardLookupPackage> LoadLookupDataAsync(SQLiteConnection conn, CardLookupsOptions opts)
        {
            IReadOnlyDictionary<string, byte[]> manaIcons = new Dictionary<string, byte[]>();
            IReadOnlyDictionary<string, byte[]> setIcons = new Dictionary<string, byte[]>();
            IReadOnlyDictionary<string, SetDto> sets = new Dictionary<string, SetDto>();
            IReadOnlyDictionary<string, PriceDto> prices = new Dictionary<string, PriceDto>();


            if (opts.HasFlag(CardLookupsOptions.Icons))
            {
                manaIcons = await _repo.ReadManaCostImagesAsync(conn);
                setIcons = await _repo.ReadSetIconImagesAsync(conn);
            }

            if (opts.HasFlag(CardLookupsOptions.Sets))
            {
                sets = await _repo.ReadSetsAsync(conn);
            }

            if (opts.HasFlag(CardLookupsOptions.Prices))
            {
                var retailerKey = _getRetailer();
                prices = await _repo.ReadPricesAsync(conn, retailerKey);
            }

            return _builder.Build(manaIcons, setIcons, sets, prices);
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
