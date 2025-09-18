using CollectaMundo.ApplicationServices.CardLists.CardLookups.Providers;
using CollectaMundo.Data;
using CollectaMundo.Data.CardLists;
using CollectaMundo.DomainLogic.CardLists.CardLookups;
using CollectaMundo.DomainLogic.CardLists.Models;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardLists.CardLookups
{

    public sealed class CardLookupsService(IDbConnectionFactory dbFactory, CardLookupsRepo repo, CardLookupBuilder builder, Func<string> getRetailer) : ICardLookupsService
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly CardLookupsRepo _cardLookupsRepo = repo;
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
                manaIcons = await _cardLookupsRepo.ReadManaCostImagesAsync(conn);
                setIcons = await _cardLookupsRepo.ReadSetIconImagesAsync(conn);
            }

            if (opts.HasFlag(CardLookupsOptions.Sets))
            {
                sets = await _cardLookupsRepo.ReadSetsAsync(conn);
            }

            if (opts.HasFlag(CardLookupsOptions.Prices))
            {
                var retailerKey = _getRetailer();
                prices = await _cardLookupsRepo.ReadPricesAsync(conn, retailerKey);
            }

            return _builder.Build(manaIcons, setIcons, sets, prices);
        }
        public async Task ResetPricesMetaProviderAsync(string retailerKey)
        {
            // Load the new map for the requested retailer
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginReadOnlyAsync();
            var dict = await _cardLookupsRepo.ReadPricesAsync(uow.CurrentConnection, retailerKey);
            await uow.CommitAsync();

            // Swap the static provider (all CardSet getters read through this)
            CardSet.PriceMetaProvider = new ValueProvider<string, PriceDto>(dict);
        }
    }
}
