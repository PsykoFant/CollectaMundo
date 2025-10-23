using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.CardImages;
using CollectaMundo.ApplicationServices.CardLists;
using CollectaMundo.ApplicationServices.CardLists.CardLookups;
using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.EditCollection;
using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.Import;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.Data.Filtering;
using CollectaMundo.DomainLogic.CardImages;
using CollectaMundo.DomainLogic.CardLists;
using CollectaMundo.DomainLogic.EditCollection;
using CollectaMundo.DomainLogic.EditCollection.Models;
using CollectaMundo.DomainLogic.Filtering;
using CollectaMundo.DomainLogic.Filtering.Enums;
using CollectaMundo.DomainLogic.GenerateMissingPng;
using CollectaMundo.Infrastructure.CardDatabaseManagement;
using CollectaMundo.Infrastructure.CardImages;
using CollectaMundo.Infrastructure.CardLists;
using CollectaMundo.Infrastructure.CardPrices;
using CollectaMundo.Infrastructure.Common;
using CollectaMundo.Infrastructure.EditCollection;
using CollectaMundo.Infrastructure.GenerateMissingPng;
using CollectaMundo.Infrastructure.Import;
using CollectaMundo.Infrastructure.RemoteLookups;
using CollectaMundo.ViewModels;
using System.Windows;

namespace CollectaMundo.Tests.TestUtils;

public static class TestAppBuilder
{
    public static async Task<(MainWindowViewModel VM, StatusViewModel Status)> BuildAsync(
    InMemoryDatabaseFixture fixture,
    IDbConnectionFactory dbFactory,
    List<CardChangeEventArgs>? eventSink = null)
    {
        await fixture.InitializeAsync();

        var statusVM = new StatusViewModel(new UserPromptService());
        var settings = new ApplicationServices.Shared.AppSettings();

        string getRetailer() => settings.PriceInfo.Retailer;
        var remoteLookups = new RemoteLookups();

        var missingPngSvc = new GenerateMissingPngService(
            new GenerateMissingPngRepo(),
            remoteLookups,
            new GenerateMissingPngLogic());

        var priceService = new CardPriceService(settings, new CardPriceRepository());

        var prepService = new CardDatabaseManagementService(
            settings,
            dbFactory,
            CreateProgressSinks(statusVM),
            new CardDatabaseManagementRepo(),
            priceService,
            missingPngSvc,
            remoteLookups);

        var cardLookupsService = new CardLookupsService(
            dbFactory,
            new CardLookupsRepo(),
            getRetailer);

        var cardListService = new CardListService(
            dbFactory,
            new CardListRepo(),
            new FilterDefaultsLogic(),
            cardLookupsService,
            new CardCoreAggregator());

        var editService = new EditCollectionService(dbFactory, new EditCollectionLogic(new EditCollectionRepo()));

        var cardImageService = new CardImageService(
            dbFactory, remoteLookups, new CardImageLogic(),
            new CardImageRepo(), new CardImageDownloader(settings));

        var importService = new ImportService(new ImportRepo(), settings);
        var filteringService = new FilteringService();
        var scheduler = new ImmediateScheduler();

        var mainVM = await MainWindowViewModel.CreateAsync(
            filteringService,
            editService,
            cardImageService,
            importService,
            prepService,
            statusVM,
            new UserPromptService(),
            cardListService,
            settings,
            scheduler);

        if (eventSink is not null)
        {
            mainVM.AddCardsVM.CardChanged += (_, e) => eventSink.Add(e);
            mainVM.EditCardsVM.CardChanged += (_, e) => eventSink.Add(e);
        }

        var searchLogic = new FilterItemSearchLogic();

        foreach (var kvp in mainVM.FilterVM.Filters.ToList())
        {
            var old = kvp.Value;

            if (old.FilterCategory == FilterType.Single)
            {
                var testable = new TestableFilterItemViewModel(
                    old.CriteriaKey,
                    old.FilterOptions,
                    old.DefaultText,
                    old.ReadableLabel ?? old.CriteriaKey,
                    mainVM.FilterVM,
                    searchLogic,
                    numericOptions: null)
                {
                    OperatorSelection = old.OperatorSelection
                };

                mainVM.FilterVM.Filters[kvp.Key] = testable;
            }
        }

        mainVM.FilterVM.NotifyFilterChanged();
        mainVM.SideMenuVisibility = Visibility.Visible;
        mainVM.ContentSectionVisibility = Visibility.Visible;
        mainVM.MainGridVisibility = Visibility.Visible;

        SpinWait.SpinUntil(() =>
            mainVM.AllCardsVM.Cards.Count >= 61 &&
            mainVM.MyCollectionVM.Cards.Count >= 22,
            millisecondsTimeout: 500);

        return (mainVM, statusVM);
    }


    private static ProgressSinks CreateProgressSinks(StatusViewModel vm) => new()
    {
        Headline = new Progress<string>(s => vm.StatusLabel1 = s),
        Detail = new Progress<string>(s => vm.StatusLabel2 = s),
        Step = new Progress<string>(s => vm.StatusLabel3 = s),
        Percent = new Progress<int>(p => vm.ProgressValue = p),
        ProgressBarVisible = new Progress<bool>(v =>
            vm.ProgressVisibility = v ? Visibility.Visible : Visibility.Collapsed)
    };
}
