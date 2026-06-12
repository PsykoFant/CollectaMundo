using CollectaMundo.ApplicationServices.KeyedDataProvider.Providers;
using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.KeyedDataProvider;
using CollectaMundo.Infrastructure.KeyedDataProvider;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.KeyedDataProvider
{

    public sealed class KeyedDataProviderService(IUnitOfWorkRunner uowRunner, IKeyedDataProviderRepo repo, Func<string> getRetailer) : IKeyedDataProviderService
    {
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly IKeyedDataProviderRepo _repo = repo;
        private readonly Func<string> _getRetailer = getRetailer;
        public async Task<KeyedDataProviderPackage> LoadKeyedDataAsync(SQLiteConnection conn, KeyedDataProviderOptions opts)
        {
            IReadOnlyDictionary<string, byte[]> manaIcons = new Dictionary<string, byte[]>();
            IReadOnlyDictionary<string, byte[]> setIcons = new Dictionary<string, byte[]>();
            IReadOnlyDictionary<string, SetDto> sets = new Dictionary<string, SetDto>();
            IReadOnlyDictionary<string, PriceDto> prices = new Dictionary<string, PriceDto>();

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

            return KeyedDataProviderBuilder.Build(manaIcons, setIcons, sets, prices);
        }
        public async Task ResetPricesMetaProviderAsync(string retailerKey)
        {
            var dict = await _uowRunner.ExecuteReadOnlyAsync(
                conn => _repo.ReadPricesAsync(conn, retailerKey));

            CardDataProviders.PriceMetaProvider = new ValueProvider<string, PriceDto>(dict);
        }
    }
}
