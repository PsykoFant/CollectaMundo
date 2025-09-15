using CollectaMundo.ApplicationServices;
using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.CardLists.CardLookups.Providers;
using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.ApplicationServices.Utilities.Progress;
using CollectaMundo.Data;
using CollectaMundo.Data.CardDatabaseManagement;
using CollectaMundo.Data.RemoteLookups;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.Utilities;
using CollectaMundo.ViewModels;
using Moq;
using System.Data.SQLite;
using System.IO;
using System.Net.Http;
using System.Reflection;

namespace CollectaMundo.Tests
{
    public class TestUtilities
    {
        public static void SeedSetMetaForTests(IEnumerable<CardSet> cards)
        {
            var dict = cards
                .Select(c => c.SetCode)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    keySelector: s => s!,
                    elementSelector: s => new SetDto
                    {
                        Code = s!,            // use SetCode as both code and display name for tests
                        Name = s!,
                        ReleaseDate = null
                    },
                    comparer: StringComparer.OrdinalIgnoreCase);

            CardSet.SetMetaProvider = new ValueProvider<string, SetDto>(dict);
        }

        public static List<CardSet> GetTestCards()
        {
            return
            [
                new() {
                    Name = "Davros, Dalek Creator",SetCode = "WHO",ManaCost = "1,U,B,R",Types = "Artifact, Creature",
                    Colors = "B,R,U",
                    SuperTypes = "Legendary",SubTypes = "Alien, Scientist",Type = "Legendary Artifact Creature — Alien Scientist",Keywords = "Menace",
                    Text = "Menace\nAt the beginning of your end step, create a 3/3 black Dalek artifact creature token with menace if an opponent lost 3 or more life this turn. Then each opponent who lost 3 or more life this turn faces a villainous choice — You draw a card, or that player discards a card.",
                    ManaValue = 4,Language = "English",Finishes = "nonfoil, foil",Rarity = "mythic",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new() {
                    Name = "Skeletal Swarming",SetCode = "PRM",ManaCost = "3,B,G",Types = "Enchantment",
                    Colors = "B,G",
                    SuperTypes = "",SubTypes = "",Type = "Enchantment",
                    Keywords = "",
                    Text = "Each Skeleton you control has trample, attacks each combat if able, and gets +X/+0, where X is the number of other Skeletons you control.\nAt the beginning of your end step, create a tapped 1/1 black Skeleton creature token. If a creature died this turn, create two of those tokens instead.",
                    ManaValue = 5,Language = "English",Finishes = "nonfoil, foil",Rarity = "rare",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new() {
                    Name = "Lumbering Laundry",SetCode = "MKM",ManaCost = "5",Types = "Artifact, Creature",
                    Colors = "",
                    SuperTypes = "",SubTypes = "Golem",Type = "Artifact Creature — Golem",Keywords = "Disguise",
                    Text = "{2}: Until end of turn, you may look at face-down creatures you don't control any time.\nDisguise {5} (You may cast this card face down for {3} as a 2/2 creature with ward {2}. Turn it face up any time for its disguise cost.)",
                    ManaValue = 5,Language = "English",Finishes = "nonfoil, foil",Rarity = "uncommon",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new() {
                    Name = "Olivia Voldaren",SetCode = "INR",ManaCost = "2,B,R",Types = "Creature",
                    Colors = "B,R",
                    SuperTypes = "Legendary",SubTypes = "Vampire",Type = "Legendary Creature — Vampire",Keywords = "Flying",
                    Text = "Flying\n{1}{R}: Olivia Voldaren deals 1 damage to another target creature. That creature becomes a Vampire in addition to its other types. Put a +1/+1 counter on Olivia Voldaren.\n{3}{B}{B}: Gain control of target Vampire for as long as you control Olivia Voldaren.",
                    ManaValue = 4,Language = "English",Finishes = "nonfoil, foil",Rarity = "mythic",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new() {
                    Name = "Rock Hydra",SetCode = "30A",ManaCost = "X,R,R",Types = "Creature",
                    Colors = "R",
                    SuperTypes = "",SubTypes = "Hydra",Type = "Creature — Hydra",Keywords = "",
                    Text = "This creature enters with X +1/+1 counters on it.\nFor each 1 damage that would be dealt to this creature, if it has a +1/+1 counter on it, remove a +1/+1 counter from it and prevent that 1 damage.\n{R}: Prevent the next 1 damage that would be dealt to this creature this turn.\n{R}{R}{R}: Put a +1/+1 counter on this creature. Activate only during your upkeep.",
                    ManaValue = 2,Language = "English",Finishes = "nonfoil",Rarity = "rare",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new() {
                    Name = "Time Walk",SetCode = "30A",ManaCost = "1,U",Types = "Sorcery",
                    Colors = "U",
                    SuperTypes = "",SubTypes = "",Type = "Sorcery",Keywords = "",
                    Text = "Take an extra turn after this one.",
                    ManaValue = 2,Language = "English",Finishes = "nonfoil",Rarity = "rare",CardsOwned=1,CardsForTrade=0, SelectedCondition="Poor", SelectedFinish="nonfoil"
                },
                new() {
                    Name = "Struggle // Survive",SetCode = "MOC",ManaCost = "2,R",Types = "Instant",
                    Colors = "R",
                    SuperTypes = "",SubTypes = "",Type = "Instant",Keywords = "Aftermath",
                    Text = "Struggle deals damage to target creature equal to the number of lands you control.",
                    ManaValue = 5,Language = "English",Finishes = "nonfoil",Rarity = "uncommon",CardsOwned=3,CardsForTrade=1, SelectedCondition="Near Mint", SelectedFinish="nonfoil"
                },
                new() {
                    Name = "Lovestruck Beast // Heart's Desire",SetCode = "CLB",ManaCost = "2,G",Types = "Creature",
                    Colors = "G",
                    SuperTypes = "",SubTypes = "Beast, Noble",Type = "Creature — Beast Noble",Keywords = "",
                    Text = "This creature can't attack unless you control a 1/1 creature.",
                    ManaValue = 3,Language = "English",Finishes = "nonfoil",Rarity = "rare",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new() {
                    Name = "Garruk Relentless // Garruk, the Veil-Cursed",SetCode = "INR",ManaCost = "3,G",Types = "Planeswalker",
                    Colors = "G,B",
                    SuperTypes = "Legendary",SubTypes = "Garruk",Type = "Legendary Planeswalker — Garruk",Keywords = "Transform",
                    Text = "When Garruk has two or fewer loyalty counters on him, transform him.\n[0]: Garruk deals 3 damage to target creature. That creature deals damage equal to its power to him.\n[0]: Create a 2/2 green Wolf creature token.",
                    ManaValue = 4,Language = "English",Finishes = "nonfoil, foil",Rarity = "mythic",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new() {
                    Name = "Kozilek's Command",SetCode = "MH3",ManaCost = "X,C,C",Types = "Kindred, Instant",
                    Colors = "",
                    SuperTypes = "",SubTypes = "Eldrazi",Type = "Kindred Instant — Eldrazi",Keywords = "",
                    Text = "Choose two —\n• Target player creates X 0/1 colorless Eldrazi Spawn creature tokens with \"Sacrifice this creature: Add {C}.\"\n• Target player scries X, then draws a card.\n• Exile target creature with mana value X or less.\n• Exile up to X target cards from graveyards.",
                    ManaValue = 2,Language = "English",Finishes = "nonfoil, foil",Rarity = "rare",CardsOwned=15,CardsForTrade=14, SelectedCondition="Excellent", SelectedFinish="foil"
                },
                new() {
                    Name = "Propagator Drone",SetCode = "MH3",ManaCost = "1,G",Types = "Creature",
                    Colors = "",
                    SuperTypes = "",SubTypes = "Eldrazi, Drone",Type = "Creature — Eldrazi Drone",Keywords = "Devoid",
                    Text = "Devoid (This card has no color.)\nCreature tokens you control have evolve. (They have \"Whenever a creature you control enters, if it has greater power or toughness than this token, put a +1/+1 counter on this token.\" They see this creature enter.)\n{3}{G}: Create a 0/1 colorless Eldrazi Spawn creature token with \"Sacrifice this token: Add {C}.\"",
                    ManaValue = 2,Language = "English",Finishes = "nonfoil, foil",Rarity = "uncommon",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new() {
                    Name = "Fire // Ice",SetCode = "INV",ManaCost = "1,R",Types = "Instant",
                    Colors = "R,U",
                    SuperTypes = "",SubTypes = "",Type = "Instant",Keywords = "",
                    Text = "Fire deals 2 damage divided as you choose among one or two targets.",
                    ManaValue = 4,Language = "English",Finishes = "nonfoil, foil",Rarity = "uncommon",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new() {
                    Name = "Tarfire",SetCode = "PLST",ManaCost = "R",Types = "Kindred, Instant",
                    Colors = "R",
                    SuperTypes = "",SubTypes = "Goblin",Type = "Kindred Instant — Goblin",Keywords = "",
                    Text = "Tarfire deals 2 damage to any target.",
                    ManaValue = 1,Language = "English",Finishes = "nonfoil",Rarity = "common",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new() {
                    Name = "Begin the Invasion",SetCode = "MOC",ManaCost = "X,W,U,B,R,G",Types = "Sorcery",
                    Colors = "B,G,R,U,W",
                    SuperTypes = "",SubTypes = "",Type = "Sorcery",Keywords = "",
                    Text = "Search your library for up to X battle cards with different names, put them onto the battlefield, then shuffle.",
                    ManaValue = 5,Language = "English",Finishes = "nonfoil, foil",Rarity = "mythic",CardsOwned=2,CardsForTrade=0, SelectedCondition="Mint", SelectedFinish="foil"
                },
                new() {
                    Name = "Lukka, Bound to Ruin",SetCode = "ONE",ManaCost = "2,R,R/G/P,G",Types = "Planeswalker",
                    Colors = "G,R",
                    SuperTypes = "Legendary",SubTypes = "Lukka",Type = "Legendary Planeswalker — Lukka",Keywords = "Compleated",
                    Text = "Compleated ({R/G/P} can be paid with {R}, {G}, or 2 life. If life was paid, this planeswalker enters with two fewer loyalty counters.)\n[+1]: Add {R}{G}. Spend this mana only to cast creature spells or activate abilities of creatures.\n[−1]: Create a 3/3 green Phyrexian Beast creature token with toxic 1.\n[−4]: Lukka deals X damage divided as you choose among any number of target creatures and/or planeswalkers, where X is the greatest power among creatures you control as you activate this ability.",
                    ManaValue = 5,Language = "English",Finishes = "nonfoil, foil",Rarity = "mythic",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new() {
                    Name = "Cat",SetCode = "Aetherdrift",ManaCost = "",Types = "Token, Creature",
                    Colors = "W",
                    SuperTypes = "",SubTypes = "Cat",Type = "Token Creature — Cat",Keywords = "Lifelink",
                    Text = "Lifelink (Damage dealt by this creature also causes you to gain that much life.)",
                    ManaValue = 0,Language = "English",Finishes = "nonfoil, foil",Rarity = "",CardsOwned=3,CardsForTrade=3, SelectedCondition="Near Mint", SelectedFinish="nonfoil"
                },
                new() {
                    Name = "Bounty: Eriana, Wrecking Ball // Wanted!",SetCode = "OTC",ManaCost = "",Types = "Card",
                    Colors = "",
                    SuperTypes = "",SubTypes = "",Type = "Card",Keywords = "",
                    Text = "At the beginning of your end step, if you committed a crime this turn, collect your reward. (Targeting opponents, anything they control, and/or cards in their graveyards is a crime.)",
                    ManaValue = 0,Language = "English",Finishes = "nonfoil, foil",Rarity = "",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new() {
                    Name = "Tundra",SetCode = "30A",ManaCost = "",Types = "Land",
                    Colors = "",
                    SuperTypes = "",SubTypes = "Plains,Island",Type = "Land — Plains Island",Keywords = "",
                    Text = "{T}: Add {W} or {U}.",
                    ManaValue = 0,Language = "English",Finishes = "nonfoil",Rarity = "rare",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                }
            ];
        }
        public static IDbConnectionFactory CreateInMemoryDbFactory(string dbName)
        {
            // Unique name per test -> isolated in-memory DB
            // URI=True ensures the "file:dbname?..." string is parsed correctly
            var cs = $"Data Source=file:{dbName}?mode=memory&cache=shared;Version=3;URI=True;";
            return new SharedMemoryDbFactory(cs);
        }
        private sealed class SharedMemoryDbFactory : IDbConnectionFactory, IDisposable
        {
            private readonly string _connectionString;
            private readonly SQLiteConnection _persistentConnection;

            public SharedMemoryDbFactory(string connectionString)
            {
                _connectionString = connectionString;
                _persistentConnection = new SQLiteConnection(connectionString);
                _persistentConnection.Open(); // keep the shared in-memory DB alive
            }

            public async Task<SQLiteConnection> OpenConnectionAsync()
            {
                var conn = new SQLiteConnection(_connectionString);
                await conn.OpenAsync();
                return conn;
            }

            public void Dispose()
            {
                try { _persistentConnection?.Dispose(); } catch { /* meh */ }
            }
        }

        public static (TestableUpdateViewModel vm, StatusViewModel statusVM, Mock<ICardDatabaseManagementService> dbService) CreateTestableUpdateViewModel(
        OperationResult? backupResult = null,
        OperationResult? updateResult = null,
        Func<int>? getMyCollectionCount = null)
        {
            var dbService = new Mock<ICardDatabaseManagementService>();

            if (backupResult is not null)
            {
                dbService.Setup(s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()))
                         .ReturnsAsync(backupResult);
            }

            if (updateResult is not null)
            {
                dbService.Setup(s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(updateResult);
            }

            var statusVM = new StatusViewModel();
            var uiState = new Mock<IUiBlockable>();
            var appRefresher = new Mock<IAppRefresher>();

            var updateVM = new TestableUpdateViewModel(
                dbService.Object,
                statusVM,
                uiState.Object,
                appRefresher.Object,
                getMyCollectionCount ?? (() => 5)
            );

            return (updateVM, statusVM, dbService);
        }


        public static void SimulatePrimaryButtonClick(StatusViewModel statusVM)
        {
            var field = typeof(StatusViewModel).GetField("_primaryAction", BindingFlags.NonPublic | BindingFlags.Instance);
            var action = (Action<object?>)field!.GetValue(statusVM)!;
            action.Invoke(null);
        }
    }
    public class TestableUpdateViewModel : UpdateViewModel
    {
        public Task? InternalUpdateTask { get; private set; }

        public TestableUpdateViewModel(
            ICardDatabaseManagementService dbService,
            StatusViewModel statusVM,
            IUiBlockable uiState,
            IAppRefresher appRefresher,
            Func<int> getMyCollectionCount)
            : base(dbService, statusVM, uiState, appRefresher, getMyCollectionCount)
        {
            UpdateDBCommand = new RelayCommand<object>(async _ =>
            {
                InternalUpdateTask = InvokeUpdateDBAsync(); // Calls private UpdateDBAsync()
                await InternalUpdateTask;
            });
        }
        public Task InvokeUpdateDBAsync() => (Task)typeof(UpdateViewModel).GetMethod("UpdateDBAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(this, null)!;
    }
    public sealed class FirstTimeSetupTestContext : IDisposable
    {
        public Mock<ICardDatabaseManagementRepo> SchemaRepo { get; } = new();
        public Mock<ICardPriceService> PriceService { get; } = new();
        public Mock<IGenerateMissingPngService> PngService { get; } = new();
        public Mock<IRemoteLookups> RemoteLookups { get; } = new();
        public Mock<IAppSettings> Settings { get; } = new();
        public Mock<ICardDatabaseDownloader> CardDatabaseDownloader { get; } = new();

        public List<int> PercentSamples { get; } = [];
        public List<bool> VisibleToggles { get; } = [];
        public List<string> Steps { get; } = [];

        private IDisposable? _dbFactoryDisposable;
        private string? _tmpRoot;

        public CardDatabaseManagementService BuildService()
        {
            // db factory (unique in-memory DB)
            var dbName = $"cmtests-{Guid.NewGuid():N}";
            var factory = TestUtilities.CreateInMemoryDbFactory(dbName);
            AppGlobals.DbFactory = factory;
            _dbFactoryDisposable = factory as IDisposable;

            // temp dirs and settings
            _tmpRoot = Path.Combine(Path.GetTempPath(), "cm-tests", dbName);
            Directory.CreateDirectory(_tmpRoot);

            Settings.Setup(s => s.DatabaseSettings).Returns(new CollectaMundo.ApplicationServices.DatabaseSettings
            {
                SQLitePath = _tmpRoot
            });
            Settings.Setup(s => s.UserDownloadsPath).Returns(_tmpRoot);
            Settings.Setup(s => s.CardDatabaseUrl).Returns("http://localhost/dummy.sqlite");
            Settings.Setup(s => s.CardPricesUrl).Returns("http://localhost/dummy.json");

            // progress sinks (plain Progress<T>, no WPF)
            var sinks = new ProgressSinks
            {
                Headline = new InlineProgress<string>(_ => { }),
                Detail = new InlineProgress<string>(_ => { }),
                Step = new InlineProgress<string>(s => Steps.Add(s)),
                Percent = new InlineProgress<int>(p => PercentSamples.Add(p)),
                ProgressBarVisible = new InlineProgress<bool>(v => VisibleToggles.Add(v))
            };

            // Build the service with the **updated** ctor (no InternetService, **with** RemoteLookups)
            return new CardDatabaseManagementService(
                Settings.Object,
                AppGlobals.DbFactory,
                sinks,
                SchemaRepo.Object,
                PriceService.Object,
                PngService.Object,
                RemoteLookups.Object,
                CardDatabaseDownloader.Object
            );
        }
        public void StubAllStepsAsSuccess()
        {
            SchemaRepo.Setup(r => r.CreateTablesAsync(It.IsAny<SQLiteConnection>())).Returns(Task.CompletedTask);
            SchemaRepo.Setup(r => r.CreateViewsAsync(It.IsAny<SQLiteConnection>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            SchemaRepo.Setup(r => r.CreateIndicesAsync(It.IsAny<SQLiteConnection>())).Returns(Task.CompletedTask);
            SchemaRepo.Setup(r => r.OptimizeAsync(It.IsAny<SQLiteConnection>())).Returns(Task.CompletedTask);

            PriceService.Setup(p => p.ImportPricesFromJsonAsync(
                    It.IsAny<string>(), It.IsAny<SQLiteConnection>(),
                    It.IsAny<IProgress<string>>(), It.IsAny<IProgress<int>>()))
                .Returns(Task.CompletedTask);

            PngService.Setup(p => p.GenerateMissingManaSymbolImagesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<IProgress<int>>())).Returns(Task.CompletedTask);
            PngService.Setup(p => p.GenerateMissingManaCostImagesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<IProgress<int>>())).Returns(Task.CompletedTask);
            PngService.Setup(p => p.GenerateMissingKeyRuneImagesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<IProgress<int>>())).Returns(Task.CompletedTask);

        }
        public void Dispose()
        {
            try
            {
                _dbFactoryDisposable?.Dispose();
                if (!string.IsNullOrEmpty(_tmpRoot) && Directory.Exists(_tmpRoot))
                {
                    Directory.Delete(_tmpRoot, recursive: true);
                }
            }
            catch { /* best effort */ }
        }

        // test helper
        sealed class InlineProgress<T> : IProgress<T>
        {
            private readonly Action<T> _onReport;
            public InlineProgress(Action<T> onReport) => _onReport = onReport;
            public void Report(T value) => _onReport(value);
        }
    }
    public class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handlerFunc) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handlerFunc = handlerFunc;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handlerFunc(request, cancellationToken);
    }
    public class NullProgress<T> : IProgress<T>
    {
        public void Report(T value) { }
    }



}
