using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.CardLists;
using CollectaMundo.ApplicationServices.CardLists.CardLookups;
using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.EditCollection;
using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.Import;
using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.Data;
using CollectaMundo.Data.CardDatabaseManagement;
using CollectaMundo.Data.CardLists;
using CollectaMundo.Data.CardPrices;
using CollectaMundo.Data.EditCollection;
using CollectaMundo.Data.Filtering;
using CollectaMundo.Data.GenerateMissingPng;
using CollectaMundo.Data.Import;
using CollectaMundo.Data.RemoteLookups;
using CollectaMundo.DomainLogic.CardLists;
using CollectaMundo.DomainLogic.CardLists.CardLookups;
using CollectaMundo.DomainLogic.EditCollection;
using CollectaMundo.DomainLogic.EditCollection.Models;
using CollectaMundo.DomainLogic.GenerateMissingPng;
using CollectaMundo.ViewModels;
using System.Windows;

namespace CollectaMundo.Tests.TestUtils;

public static class TestAppBuilder
{
    public static async Task<(MainWindowViewModel VM, StatusViewModel Status)> BuildAsync(InMemoryDatabaseFixture fixture, IDbConnectionFactory dbFactory, List<CardChangeEventArgs>? eventSink = null)

    {
        await fixture.InitializeAsync(); // ensure schema/seed

        var statusVM = new StatusViewModel();
        var settings = new ApplicationServices.Shared.AppSettings();

        string getRetailer() => settings.PriceInfo.Retailer;

        var remoteLookups = new RemoteLookups();

        var missingPngSvc = new GenerateMissingPngService(
            new GenerateMissingPngRepository(),
            remoteLookups,
            new GenerateMissingPngLogic());

        var priceService = new CardPriceService(settings, new CardPriceRepository());

        var prepService = new CardDatabaseManagementService(
            settings,
            dbFactory, // <- passed in explicitly
            CreateProgressSinks(statusVM),
            new CardDatabaseManagementRepo(),
            priceService,
            missingPngSvc,
            remoteLookups);

        var cardLookupsService = new CardLookupsService(
            dbFactory,
            new CardLookupsRepo(),
            new CardLookupBuilder(),
            getRetailer);

        var cardListService = new CardListService(
            dbFactory,
            new CardListRepository(),
            new FilterDefaultsLogic(),
            cardLookupsService,
            new CardCoreAggregator());

        var editService = new EditCollectionService(dbFactory, new EditCollectionLogic(new EditCollectionRepository()));
        var importService = new ImportService(new ImportRepo(), settings);
        var filteringService = new FilteringService();
        var scheduler = new ImmediateScheduler();

        var mainVM = await MainWindowViewModel.CreateAsync(
            filteringService,
            editService,
            importService,
            prepService,
            statusVM,
            cardListService,
            settings,          // also passed directly
            scheduler);

        if (eventSink is not null)
        {
            mainVM.AddCardsVM.CardChanged += (_, e) => eventSink.Add(e);
            mainVM.EditCardsVM.CardChanged += (_, e) => eventSink.Add(e);
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
