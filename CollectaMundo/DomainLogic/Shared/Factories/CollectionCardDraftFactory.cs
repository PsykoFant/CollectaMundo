using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CollectionMutations.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.Infrastructure.CardLists.Models;

namespace CollectaMundo.DomainLogic.Shared.Factories
{
    public static class CollectionCardDraftFactory
    {
        // From CollectionCard to CollectionCardDraft - full 
        public static CollectionCardDraft FromCollectionCard(CollectionCard source)
        {
            return new CollectionCardDraft
            {
                CardId = source.CardId,
                Uuid = source.Uuid,
                Name = source.Name,
                SetName = source.SetName,

                SelectedCondition = source.SelectedCondition,
                Language = source.Language,
                SelectedFinish = source.SelectedFinish,
                SellPrice = source.SellPrice,
                SellPriceCurrency = source.SellPriceCurrency,
                SelectedLocationId = source.SelectedLocationId,
                CardLocationProvider = source.CardLocationProvider,
                Comment = source.Comment,

                CardsOwned = source.CardsOwned,
                CardsForTrade = source.CardsForTrade
            };
        }

        // From CollectionCard to CollectionCardDraft - partial for specific operations
        public static CollectionCardDraft FromCollectionCardForDelete(CollectionCard source)
        {
            var draft = FromCollectionCard(source);

            draft.CardsOwned = 0;
            draft.CardsForTrade = 0;

            return draft;
        }
        public static CollectionCardDraft FromCollectionCardForTradeAll(CollectionCard source)
        {
            var draft = FromCollectionCard(source);

            draft.CardsForTrade = draft.CardsOwned;

            return draft;
        }
        public static CollectionCardDraft FromCollectionCardForTradeNone(CollectionCard source)
        {
            var draft = FromCollectionCard(source);

            draft.CardsForTrade = 0;

            return draft;
        }
        public static CollectionCardDraft FromCollectionCardWithLocation(CollectionCard source, int? locationId)
        {
            var draft = FromCollectionCard(source);

            draft.SelectedLocationId = locationId;

            return draft;
        }
        public static CollectionCardDraft FromCollectionCardWithSellPrice(CollectionCard source, decimal sellPrice, string currency)
        {
            ArgumentNullException.ThrowIfNull(source);

            var normalizedCurrency = NormalizeSellPriceCurrency(currency);

            return CreateDraftWithSellPrice(source, sellPrice, normalizedCurrency);
        }
        public static CollectionCardDraft? FromCollectionCardWithRetailPriceDiscount(CollectionCard source, decimal discountRate, string currency)
        {
            ArgumentNullException.ThrowIfNull(source);

            if (discountRate is < 0m or > 1m)
            {
                throw new ArgumentOutOfRangeException(nameof(discountRate));
            }

            var normalizedCurrency = NormalizeSellPriceCurrency(currency);

            if (source.CardInCollectionPrice is not decimal retailPrice || retailPrice <= 0m)
            {
                return null;
            }

            var discountedPrice = decimal.Round(retailPrice * (1m - discountRate), 2, MidpointRounding.AwayFromZero);

            return CreateDraftWithSellPrice(source, discountedPrice, normalizedCurrency);
        }
        private static string NormalizeSellPriceCurrency(string currency)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(currency);

            var normalizedCurrency = currency.ToUpperInvariant();

            if (normalizedCurrency is not ("USD" or "EUR"))
            {
                throw new ArgumentException($"Unsupported currency '{currency}'.", nameof(currency));
            }

            return normalizedCurrency;
        }
        private static CollectionCardDraft CreateDraftWithSellPrice(CollectionCard source, decimal sellPrice, string normalizedCurrency)
        {
            var draft = FromCollectionCard(source);
            draft.SellPrice = sellPrice;
            draft.SellPriceCurrency = normalizedCurrency;

            return draft;
        }

        // From CollectionCardDbRow to CollectionCardDraft - full
        public static CollectionCardDraft FromDbRow(CollectionCardDbRow row)
        {
            return new CollectionCardDraft
            {
                CardId = row.CardId,
                Uuid = row.Identity.Uuid,
                SelectedCondition = row.Identity.Condition,
                Language = row.Identity.Language,
                SelectedFinish = row.Identity.Finish,
                SelectedLocationId = row.Identity.LocationId,
                Comment = row.Identity.Comment,
                SellPrice = row.SellPrice,
                SellPriceCurrency = row.SellPriceCurrency,
                CardsOwned = row.CardsOwned,
                CardsForTrade = row.CardsForTrade
            };
        }

        // From CollectionCardDbRow to CollectionCardDraft - partial for specific operations
        public static CollectionCardDraft FromDbRowWithClearedLocation(CollectionCardDbRow row)
        {
            var draft = FromDbRow(row);

            draft.SelectedLocationId = null;

            return draft;
        }


        // From PrintingCard to CollectionCardDraft - partial for creating new collection entries with default values
        public static CollectionCardDraft FromPrintingCard(PrintingCard source)
        {
            return new CollectionCardDraft
            {
                Uuid = source.Uuid,
                Name = source.Name,
                SetName = source.SetName,
                CardsOwned = 1,
                CardsForTrade = 0
            };
        }

        // From CollectionCardDraft to CollectionCardDraft - for splitting cards in the collection
        public static CollectionCardDraft FromSplit(CollectionCardDraft source)
        {
            return new CollectionCardDraft
            {
                CardId = null,

                Uuid = source.Uuid,
                Name = source.Name,
                SetName = source.SetName,

                CardsOwned = 1,
                CardsForTrade = source.CardsOwned > source.CardsForTrade ? 0 : 1,

                Conditions = source.Conditions,
                FinishOptions = source.FinishOptions,
                OtherLanguages = source.OtherLanguages,

                SelectedCondition = source.SelectedCondition,
                Language = source.Language,
                SelectedFinish = source.SelectedFinish,
                SellPrice = source.SellPrice,
                SellPriceCurrency = source.SellPriceCurrency,
                SelectedLocationId = source.SelectedLocationId,
                Comment = source.Comment
            };
        }
    }
}
