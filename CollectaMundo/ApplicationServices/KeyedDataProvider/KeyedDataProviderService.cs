using CollectaMundo.ApplicationServices.KeyedDataProvider.Providers;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.KeyedDataProvider;
using CollectaMundo.Infrastructure.KeyedDataProvider;
using CollectaMundo.Infrastructure.Shared;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.KeyedDataProvider
{

    public sealed class KeyedDataProviderService(IDbConnectionFactory dbFactory, IKeyedDataProviderRepo repo, Func<string> getRetailer) : IKeyedDataProviderService
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly IKeyedDataProviderRepo _repo = repo;
        private readonly Func<string> _getRetailer = getRetailer;
        public async Task<KeyedDataProviderPackage> LoadKeyedDataAsync(SQLiteConnection conn, KeyedDataProviderOptions opts)
        {
            IReadOnlyDictionary<string, byte[]> manaIcons = new Dictionary<string, byte[]>();
            IReadOnlyDictionary<string, byte[]> setIcons = new Dictionary<string, byte[]>();
            IReadOnlyDictionary<string, SetDto> sets = new Dictionary<string, SetDto>();
            IReadOnlyDictionary<string, PriceDto> prices = new Dictionary<string, PriceDto>();
            IReadOnlyDictionary<int, CardLocation> locations = new Dictionary<int, CardLocation>();

            if (opts.HasFlag(KeyedDataProviderOptions.Icons))
            {
                manaIcons = await _repo.ReadManaCostImagesAsync(conn);
                setIcons = await _repo.ReadSetIconImagesAsync(conn);
            }

            if (opts.HasFlag(KeyedDataProviderOptions.Sets))
            {
                sets = await _repo.ReadSetsAsync(conn);
            }

            if (opts.HasFlag(KeyedDataProviderOptions.Prices))
            {
                var retailerKey = _getRetailer();
                prices = await _repo.ReadPricesAsync(conn, retailerKey);
            }

            if (opts.HasFlag(KeyedDataProviderOptions.Locations))
            {
                locations = await _repo.ReadLocationsAsync(conn);
            }

            return KeyedDataProviderBuilder.Build(manaIcons, setIcons, sets, prices, locations);
        }
        public async Task ResetPricesMetaProviderAsync(string retailerKey)
        {
            // Load the new map for the requested retailer
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginReadOnlyAsync();
            var dict = await _repo.ReadPricesAsync(uow.CurrentConnection, retailerKey);
            await uow.CommitAsync();

            // Swap the static provider (all CardSet getters read through this)
            CardSet.PriceMetaProvider = new ValueProvider<string, PriceDto>(dict);
        }
        public async Task ResetCardLocationProviderAsync()
        {
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginReadOnlyAsync();

            var dict = await _repo.ReadLocationsAsync(uow.CurrentConnection);

            await uow.CommitAsync();

            CardSet.CardLocationProvider = new ValueProvider<int, CardLocation>(dict);
        }
    }
}
