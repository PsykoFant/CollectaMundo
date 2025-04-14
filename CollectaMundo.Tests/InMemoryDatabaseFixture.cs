using CsvHelper;
using CsvHelper.Configuration;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace CollectaMundo.Tests
{
    // A fixture class to set up an in‑memory SQLite database and seed it from CSV strings.
    public class InMemoryDatabaseFixture : IDisposable
    {
        public SQLiteConnection Connection { get; private set; }

        // CSV data for each table.
        // Replace these sample rows with the full CSV data from your files.
        private const string CardsCsv = @"
artist;artistIds;asciiName;attractionLights;availability;boosterTypes;borderColor;cardParts;colorIdentity;colorIndicator;colors;defense;duelDeck;edhrecRank;edhrecSaltiness;faceConvertedManaCost;faceFlavorName;faceManaValue;faceName;finishes;flavorName;flavorText;frameEffects;frameVersion;hand;hasAlternativeDeckLimit;hasContentWarning;hasFoil;hasNonFoil;isAlternative;isFullArt;isFunny;isOnlineOnly;isOversized;isPromo;isRebalanced;isReprint;isReserved;isStarter;isStorySpotlight;isTextless;isTimeshifted;keywords;language;layout;leadershipSkills;life;loyalty;manaCost;manaValue;name;number;originalPrintings;originalReleaseDate;originalText;originalType;otherFaceIds;power;printings;promoTypes;rarity;rebalancedPrintings;relatedCards;securityStamp;setCode;side;signature;sourceProducts;subsets;subtypes;supertypes;text;toughness;type;types;uuid;variations;watermark
Alan Pollack;70c20ea3-5ad6-4082-a337-6e994ae5828e;;;paper;default;black;;W;;W;;;18430;;;;;;nonfoil;;;;1997;;;;0;1;;;;;;;;1;;;;;;Provoke;English;normal;;;;{W};1.0;Deftblade Elite;LGN-12;;02-08-2024;;;;1;LGN, PLST, VMA;;common;;;;PLST;;;""{""nonfoil"": [""1db93605-0a4e-5e5b-80a2-18a6e177f51d""]}"";;Human, Soldier;;Provoke (Whenever this creature attacks, you may have target creature defending player controls untap and block it if able.)\n{1}{W}: Prevent all combat damage that would be dealt to and dealt by Deftblade Elite this turn.;1;Creature — Human Soldier;Creature;a8b3fc41-5f35-5fe2-bc74-7e015a836fd3;;
Bastien L. Deharme;798f63ec-41ad-474e-9708-df08193345c6;;;mtgo;;black;;B;;B;;;2517;0.21;;;;;nonfoil, foil;;;;2015;;;;1;1;;;;1;;1;;1;;;;;;;English;normal;;;;{3}{B};4.0;Gixian Puppeteer;105702;;03-01-2023;;;;4;BRO, PBRO, PRM;;rare;;;oval;PRM;;;;;Phyrexian, Warlock;;Whenever you draw your second card each turn, each opponent loses 2 life and you gain 2 life.\nWhen Gixian Puppeteer dies, return another target creature card with mana value 3 or less from your graveyard to the battlefield.;3;Creature — Phyrexian Warlock;Creature;1c1aafc6-e078-5951-a22e-4d386aef8140;;
Eric Deschamps;37970e22-9cee-44c1-af44-5ee27cf26b76;;;mtgo, paper;default;black;;;;;;;14701;0.11;;;;;nonfoil, foil;;;;2015;;;;1;1;;;;;;;;1;;;;;;;English;normal;;;;{2};2.0;Bubbling Cauldron;213;;;{1}, {T}, Sacrifice a creature: You gain 4 life.\n{1}, {T}, Sacrifice a creature named Festering Newt: Each opponent loses 4 life. You gain life equal to the life lost this way.;Artifact;;;IMA, JMP, M14;;uncommon;;;;IMA;;;""{""foil"": [""6dea76a3-dab9-5707-9181-635f24535a5b""], ""nonfoil"": [""6dea76a3-dab9-5707-9181-635f24535a5b""]}"";;;;{1}, {T}, Sacrifice a creature: You gain 4 life.\n{1}, {T}, Sacrifice a creature named Festering Newt: Each opponent loses 4 life. You gain life equal to the life lost this way.;;Artifact;Artifact;9ce99de8-bbd0-5043-9670-7f69ec41c1eb;;
James Ryman;3852bbc9-11c0-4fe3-8722-a06ad7e2bcc5;;;paper;;black;;W;;W;;;4829;0.18;;;;;foil;;""Justice isn't done until undeath is undone."";;2003;;;;1;0;;;;;;1;;1;;1;;;;Flying;English;normal;;;;{5}{W}{W};7.0;Angel of Glory's Rise;A9;;01-02-2013;;;;4;AVR, MIC, PAVR;setpromo, resale;rare;;;;PAVR;;;;;Angel;;Flying\nWhen Angel of Glory's Rise enters, exile all Zombies, then return all Human creature cards from your graveyard to the battlefield.;6;Creature — Angel;Creature;aff7557c-2e85-5bda-8231-d8f1e46b43c8;;colorpie
Lenka Šimečková;c9e04828-8fcb-47ba-aadc-4005d43a79f4;;;paper;;borderless;;G, U;;G, U;;;5226;0.15;;;;;nonfoil, foil;;;showcase, legendary, inverted;2015;;;;1;1;;;;;;;;;;;;;;Paradox, Team TARDIS;English;normal;""{""brawl"": false, ""commander"": true, ""oathbreaker"": false}"";;;{1}{G}{U};3.0;The Thirteenth Doctor;564;;;;;;2;WHO;boosterfun;mythic;;;triangle;WHO;;;""{""foil"": [""2d252e89-9965-5f23-907e-7a98726d7fac"", ""c3727661-aaa8-5fc8-985e-93b619c3ef05""], ""nonfoil"": [""2d252e89-9965-5f23-907e-7a98726d7fac"", ""c3727661-aaa8-5fc8-985e-93b619c3ef05""]}"";;Time Lord, Doctor;Legendary;Paradox — Whenever you cast a spell from anywhere other than your hand, put a +1/+1 counter on target creature.\nTeam TARDIS — At the beginning of your end step, untap each creature you control with a counter on it.;2;Legendary Creature — Time Lord Doctor;Creature;a4072135-41e9-54e1-8905-4a853e6cf9ff;845adf01-30b9-59f5-940b-2200b70f713d, a4d8b55a-7761-5685-a768-d6bcae2809b0, be61e9c2-20bf-59b8-bfae-3a5f64a7fc28, 4f3671e6-6faa-533a-9ba9-f05f5bf09e8b, 47adf329-3c8e-5af4-801a-c361a3e647f9, 6129fe0e-e952-5b75-b5f5-8cb46cc58b77, 882be6aa-fac2-52b8-9b47-a0980d475a11;
Scott Hampton;1675947d-e663-4200-a6ce-5ad7bb3c83b1;;;mtgo, paper;;black;;G;;G;;a;7057;;;;;;nonfoil;;It hardly weighs anything, but it takes all day to remove.;;2003;;;;0;1;;;;;;;;1;;1;;;;;English;normal;;;;{2}{G};3.0;Spidersilk Armor;32;;;Creatures you control get +0/+1 and have reach. (They can block creatures with flying.);Enchantment;;;DDG, MMQ, PLST;;common;;;;DDG;;;""{""nonfoil"": [""c77159bf-6de4-5324-9226-d0142f6c8b9a""]}"";;;;Creatures you control get +0/+1 and have reach. (They can block creatures with flying.);;Enchantment;Enchantment;e5be8d1c-7e4f-5ee2-a86a-4c4706c5ea9e;;
Edward P. Beard, Jr.;b845b8ee-aeea-4822-bcf9-7230625ac95c;;;paper;;white;;B;;B;;;17915;;;;;;nonfoil;;;;2003;;;;0;1;1;;;;;;;1;;;;;;;Spanish;normal;;;;{B};1.0;Carrion Rats;A41;;;;;;2;PHUK, PSAL, TOR;mediainsert;common;;;;PSAL;;;;;Rat;;Whenever Carrion Rats attacks or blocks, any player may exile a card from their graveyard. If a player does, Carrion Rats assigns no combat damage this turn.;1;Creature — Rat;Creature;0439faae-bab3-581b-add1-37a73bb1e62c;d01089e0-f1c8-5c96-b20a-a811600573f5, fae79ec1-d110-59c3-909d-f2ee7720331c;
Camille Alquier;a0670296-0225-4630-95c6-feab68d14df4;;;arena, mtgo, paper;;borderless;;B;;B;;;1171;1.02;5.0;;5.0;Sheoldred;foil;;;legendary;2015;;;;1;0;;1;;;;;;;;1;;;;Menace, Transform;English;transform;""{""brawl"": true, ""commander"": true, ""oathbreaker"": false}"";;;{3}{B}{B};5.0;Sheoldred // The True Scriptures;340;;;;;c153f8a5-0537-5e54-bddd-ef806fbd0af9;4;MOM, PMOM;serialized, doublerainbow, boosterfun;mythic;;;oval;MOM;a;;""{""foil"": [""78441b62-b849-5262-a30f-9039ac252373""]}"";;Phyrexian, Praetor;Legendary;Menace\nWhen Sheoldred enters, each opponent sacrifices a nontoken creature or planeswalker of their choice.\n{4}{B}: Exile Sheoldred, then return it to the battlefield transformed under its owner's control. Activate only as a sorcery and only if an opponent has eight or more cards in their graveyard.;5;Legendary Creature — Phyrexian Praetor;Creature;5fb4cbe9-b370-5205-bb89-101c677eab7e;a08e899a-7d8a-5ee1-932d-e880bcf8ee51, 552ea12c-68a4-5bc6-abcc-7c1356bccca9;
Pascal Quidault;aabbf32e-5330-403a-8a99-a653c050e263;;;arena;;black;;R;;R;;;16438;;;;;;nonfoil;;;;2015;;;;0;1;;;;1;;;;1;;1;;;;;English;normal;;;;{1}{R};2.0;Boundary Lands Ranger;126;;;;;;2;PIO, WOE;;common;;;;PIO;;;;;Human, Ranger;;At the beginning of combat on your turn, if you control a creature with power 4 or greater, you may discard a card. If you do, draw a card.;2;Creature — Human Ranger;Creature;bf2540bd-5984-569c-b6ab-c5d77db981bd;;
Grzegorz Rutkowski;b5f49d0d-8056-48e3-b614-090e656b4f9c;;;arena, paper;;black;;G;;G;;;720;0.72;;;;;nonfoil;;;;2015;;;;0;1;;;;;;;;1;;;;;;Reach;English;normal;;;;{4}{G}{G};6.0;Ancient Greenwarden;627;;;Reach (This creature can block creatures with flying.)\nYou may play lands from your graveyard.\nIf a land entering the battlefield causes a triggered ability of a permanent you control to trigger, that ability triggers an additional time.;Creature — Elemental;;5;J25, OTC, PRM, PZNR, ZNR;;mythic;;;oval;J25;;;""{""nonfoil"": [""4498a9bc-294c-5036-91e5-031880296db8""]}"";;Elemental;;Reach (This creature can block creatures with flying.)\nYou may play lands from your graveyard.\nIf a land entering the battlefield causes a triggered ability of a permanent you control to trigger, that ability triggers an additional time.;7;Creature — Elemental;Creature;83ff4169-cecd-5fe5-9fe0-721f276c6f2c;;
He Jiancheng;0b1dee8b-d30a-488d-9cc5-27bddff2c30b;;;mtgo;default;black;;W;;;;;;0.45;;;;;nonfoil, foil;;;;1997;;;;1;1;;;;1;;;;1;;;;;;;English;normal;;;;;0.0;Plains;217;;;W;Basic Land — Plains;;;10E, 2ED, 2XM, 30A, 3ED, 40K, 4BB, 4ED, 5ED, 6ED, 7ED, 8ED, 9ED, ACR, AFR, AKH, AKR, ALA, ANA, ANB, ARC, ATH, AVR, BBD, BFZ, BLB, BRB, BRC, BRO, C13, C14, C15, C16, C17, C18, C19, CED, CEI, CHK, CLB, CLU, CM2, CMA, CMD, CMM, CMR, CST, DDC, DDE, DDF, DDG, DDH, DDI, DDK, DDL, DDN, DDO, DDP, DDQ, DFT, DMR, DMU, DOM, DSK, DTK, DVD, E01, ELD, FBB, FDN, FRF, G17, GK1, GK2, GN2, GN3, GNT, GRN, GS1, H09, HBG, HOP, HOU, ICE, IKO, INV, ISD, ITP, J14, J22, J25, JMP, KHM, KLD, KLR, KTK, LCI, LEA, LEB, LRW, LTR, M10, M11, M12, M13, M14, M15, M19, M20, M21, MBS, MD1, ME1, ME3, MH2, MH3, MID, MIR, MKM, MMQ, MOM, MRD, NEO, NPH, ODY, OLEP, ONE, ONS, ORI, OTJ, P02, P23, PAL00, PAL01, PAL03, PAL04, PAL05, PAL06, PAL99, PALP, PANA, PARL, PC2, PCA, PDGM, PELP, PF19, PF20, PGPX, PGRU, PIP, PLST, PMPS, PMPS06, PMPS07, PMPS08, PMPS09, PMPS10, PMPS11, POR, PPP1, PRM, PRW2, PRWK, PS11, PSAL, PSS2, PSS3, PSS4, PTC, PTK, PZ2, RAV, REX, RIX, RNA, ROE, RQS, RTR, S99, SCD, SHM, SIR, SLD, SLP, SNC, SOI, SOM, STX, SUM, TD0, TD2, THB, THS, TMP, TPR, TSP, UGL, UND, UNF, UNH, USG, UST, VOW, WAR, WC00, WC02, WC03, WC04, WC97, WC98, WHO, WOE, XANA, XLN, ZEN, ZNR;;common;;;;ME3;;;;;Plains;Basic;({T}: Add {W}.);;Basic Land — Plains;Land;d54b922f-006e-59ff-bfeb-f96f3278fc7b;81789ad4-aed8-5a47-a03c-41bae1ec088c, a7627021-b739-583e-95be-68c2814578f7;
Andreas Rocha;d084e74b-f63a-4107-b72c-ed6250ecc93b;;;arena, mtgo, paper;;black;;G;;;;;;0.38;;;;;nonfoil, foil;;;;2015;;;;1;1;;;;;;;;1;;1;;;;;English;normal;;;;;0.0;Forest;384;;;;;;;10E, 2ED, 2XM, 30A, 3ED, 40K, 4BB, 4ED, 5ED, 6ED, 7ED, 8ED, 9ED, ACR, AFR, AKH, AKR, ALA, ANA, ANB, ARC, ATH, AVR, BBD, BFZ, BLB, BRB, BRO, BTD, C13, C14, C15, C16, C17, C18, C19, CED, CEI, CHK, CLB, CLU, CM2, CMA, CMD, CMM, CMR, CST, DD1, DDD, DDE, DDG, DDH, DDJ, DDL, DDM, DDO, DDP, DDR, DDS, DDU, DFT, DKM, DMR, DMU, DOM, DPA, DSK, DTK, E01, ELD, EVG, FBB, FDN, FRF, G17, GK1, GK2, GN2, GN3, GNT, GRN, GS1, GVL, H09, HBG, HOP, HOU, ICE, IKO, INV, ISD, ITP, J14, J22, J25, JMP, KHM, KLD, KLR, KTK, LCI, LEA, LEB, LRW, LTR, M10, M11, M12, M13, M14, M15, M19, M20, M21, MBS, ME1, ME3, MH2, MH3, MID, MIR, MKM, MMQ, MOM, MRD, NEO, NPH, ODY, OLEP, ONE, ONS, ORI, OTJ, P02, P23, PAL00, PAL01, PAL03, PAL04, PAL05, PAL06, PAL99, PALP, PANA, PARL, PC2, PCA, PELP, PF19, PF20, PGPX, PGRU, PIP, PLST, PMPS, PMPS06, PMPS07, PMPS08, PMPS09, PMPS10, PMPS11, POR, PPP1, PRM, PRW2, PRWK, PS11, PSAL, PSS2, PSS3, PSS4, PTC, PTK, PZ2, RAV, REX, RIX, RNA, ROE, RQS, RTR, S99, SCD, SHM, SIR, SLD, SNC, SOI, SOM, STX, SUM, TD0, TD2, THB, THS, TMP, TPR, TSP, UGL, UND, UNF, UNH, USG, UST, VOW, WAR, WC00, WC01, WC02, WC03, WC04, WC97, WC98, WC99, WHO, WOE, XANA, XLN, ZEN, ZNR;;common;;;;MID;;;""{""nonfoil"": [""1eb46285-39db-5049-be86-bc2d50b91d77"", ""57be20bf-5950-5969-b16b-047aaf6e5010"", ""89a4fe76-d836-5b5c-bde4-49358f23810c"", ""c654b999-069f-5af8-8e41-87d6282f9dfd""]}"";;Forest;Basic;({T}: Add {G}.);;Basic Land — Forest;Land;a499b95b-8f0f-56e2-ba4e-077e5bc6979e;ea2a2ba8-dec2-55c7-af4a-2de52d404cc0, 0c1b7f25-d6cd-5897-bebf-003b5c97bb87;
Carl Critchlow;17948f16-611a-44b8-8d10-9895a0bdfff1;;;mtgo, paper;default;black;;W;;W;;;19678;;;;;;nonfoil, foil;;""Gerrard offered no defense to Orim's condemnation; the mission was under his command, and he was responsible."";;1997;;;;1;1;;;;;;;;;;;;;;;English;normal;;;;{1}{W};2.0;Renounce;42;;;Sacrifice any number of permanents. You gain 2 life for each one sacrificed this way.;Instant;;;MMQ;;uncommon;;;;MMQ;;;""{""foil"": [""65bc1540-791e-5561-b050-658c4e65e28f""], ""nonfoil"": [""1c7870eb-5ce3-5be0-b0fb-644bfe832d14"", ""65bc1540-791e-5561-b050-658c4e65e28f""]}"";;;;Sacrifice any number of permanents. You gain 2 life for each permanent sacrificed this way.;;Instant;Instant;f4198239-6754-5db7-9229-4cc9e264151e;;
Sam Burley;f89f4b78-cefb-41f7-b7cb-4f4d28de0c4f;;;arena, mtgo, paper;;borderless;;;;;;;170;0.14;;;;;nonfoil, foil;;When Kellan let his eyes wander to the horizon, he saw a land whose destiny was as uncertain as his own.;inverted;2015;;;;1;1;;1;;;;;;1;;1;;;;;English;normal;;;;;0.0;Prismatic Vista;38;;19-04-2024;{T}, Pay 1 life, Sacrifice Prismatic Vista: Search your library for a basic land card, put it onto the battlefield, then shuffle.;Land;;;H1R, MH1, PRM, SPG, ZNE;boosterfun;mythic;;;oval;SPG;;;""{""foil"": [""6b307a92-0a52-5ecc-aaeb-1fa4180ec7ad""], ""nonfoil"": [""7c02f937-ddc8-57af-9cf7-a09f7c0be1c3""]}"";;;;{T}, Pay 1 life, Sacrifice Prismatic Vista: Search your library for a basic land card, put it onto the battlefield, then shuffle.;;Land;Land;e9325157-a5ff-5ee4-a392-28829db53b10;;
Yigit Koroglu;a24479f8-6d9e-40f9-bede-f29899922b97;;;arena, mtgo, paper;default;black;;U;;U;;;16958;0.33;;;;;nonfoil, foil;;""A great wave crashed, and a mighty wind blew out the stars.""\n—*The Cosmogony*"";;2015;;;;1;1;;;;;;;;;;;;;;;English;normal;;;;{2}{U};3.0;Deny the Divine;47;;;Counter target creature or enchantment spell. If that spell is countered this way, exile it instead of putting it into its owner's graveyard.;Instant;;;THB;;common;;;;THB;;;""{""foil"": [""089080a6-6828-50c3-9101-7dde35781608"", ""ef9b5f95-e25a-5a7f-ade5-a78df463ebe6""], ""nonfoil"": [""089080a6-6828-50c3-9101-7dde35781608"", ""38c6c6f8-2cbe-566e-85b5-6caba302d843""]}"";;;;Counter target creature or enchantment spell. If that spell is countered this way, exile it instead of putting it into its owner's graveyard.;;Instant;Instant;6955a085-32f8-5582-9533-c506ef88213b;;
Richard Sardinha;a3cce4f0-fbf5-4883-aac7-fb28b993b132;;;paper;;silver;;W;;W;;;;;;;;;nonfoil;;Mongo's fleas no longer bothered him. But the family of goblins that had moved in behind his left ear was starting to get really irritating.;;2015;;;;0;1;;;1;;;;;1;;1;;;;;English;normal;;;;{2}{W};3.0;Staying Power;13;;;""Until end of turn"" and ""this turn"" effects don't end."";Enchantment;;;ULST, UND, UNH;;rare;;;oval;UND;;;""{""nonfoil"": [""b966f472-6bd1-55a5-9e13-235320a03bb2""]}"";;;;""Until end of turn"" and ""this turn"" effects don't end."";;Enchantment;Enchantment;b23e355d-5190-5db8-ad5a-5ff9d0fe9bea;;
Zack Stella;17bc7f55-958b-43f4-bb40-09746d05b3f9;;;paper;;black;;G;;G;;;517;0.2;;;;;foil;;;;2015;;;;1;0;1;;;;;1;;1;;1;;;;Changeling;English;normal;;;;{2}{G};3.0;Realmwalker;188s;;;;;;2;CMM, KHM, LCC, PKHM, PLST, PRM;prerelease, datestamped;rare;;;oval;PKHM;;;;;Shapeshifter;;Changeling (This card is every creature type.)\nAs Realmwalker enters, choose a creature type.\nYou may look at the top card of your library any time.\nYou may cast creature spells of the chosen type from the top of your library.;3;Creature — Shapeshifter;Creature;66124810-2a79-5c4f-a43f-181400aa8c4f;31669a91-4e69-5bbf-b1a9-33c7ce9eab0b;
Lake Hurwitz;3677c64b-55e6-4a0d-a952-bdbb05531220;;;arena, mtgo, paper;default;black;;B;;B;;;12645;0.75;;;;;nonfoil, foil;;All things considered, his first day on patrol could have gone better.;;2015;;;;1;1;;;;;;;;1;;;;;;Enchant;English;normal;;;;{B};1.0;Dead Weight;67;;;Enchant creature\nEnchanted creature gets -2/-2.;Enchantment — Aura;;;GRN, IKO, ISD, J22, LCI, PLST, SIR, SOI;;common;;;;GRN;;;""{""foil"": [""dbecee60-807c-5627-9090-d2114a897fcd""], ""nonfoil"": [""236711ef-2574-5a52-ada8-9b1f7f80c9a7"", ""378dd83c-121e-5b5a-b303-2bd7ad263313"", ""b5495422-e733-5142-8e84-902bc81e62ca"", ""ceb9dc37-df20-5ea6-884d-c41ecf4b7a4d"", ""dbecee60-807c-5627-9090-d2114a897fcd""]}"";;Aura;;Enchant creature\nEnchanted creature gets -2/-2.;;Enchantment — Aura;Enchantment;43b41a9c-56c7-5aa9-80bf-dc90211351ab;;
Mark Brill;46dc4b5e-e42c-4d65-a4f3-ad75b0f6f6dd;;;paper;default;black;;R;;R;;;22514;0.2;;;;;nonfoil;;;;1997;;;;0;1;;;;;;;;1;;;;;;;English;normal;;;;{3}{R};4.0;Flameshot;PCY-90;;07-11-2019;;;;;PCY, PLST, WC00;;uncommon;;;;PLST;;;""{""nonfoil"": [""82697abd-23fa-5ab8-8fd4-c835c77bdc7c"", ""abb81fe0-95ce-5abf-b4a3-a09beea5732b"", ""cbd26a74-d0fd-5a8e-8f28-61f759dc3675""]}"";;;;You may discard a Mountain card rather than pay this spell's mana cost.\nFlameshot deals 3 damage divided as you choose among one, two, or three target creatures.;;Sorcery;Sorcery;b473e6ca-2c93-5244-b821-a8bc41061ddb;;
Howard Lyon;6dd06426-59fe-4b9c-aad5-6da8446a5c3d;;;arena;default;black;;G, U;;G, U;;;4677;0.04;;;;;nonfoil;;;;2015;;;;0;1;;;;1;;;;1;;;;;;Scry;English;normal;""{""brawl"": false, ""commander"": false, ""oathbreaker"": true}"";;X;{X}{G}{U};2.0;Nissa, Steward of Elements;248;;;+2: Scry 2.\n0: Look at the top card of your library. If it's a land card or a creature card with converted mana cost less than or equal to the number of loyalty counters on Nissa, Steward of Elements, you may put that card onto the battlefield.\n−6: Untap up to two target lands you control. They become 5/5 Elemental creatures with flying and haste until end of turn. They're still lands.;Legendary Planeswalker — Nissa;;;AKH, AKR, C20, M3C, PAKH, PS17;;mythic;;;;AKR;;;;;Nissa;Legendary;[+2]: Scry 2.\n[0]: Look at the top card of your library. If it's a land card or a creature card with mana value less than or equal to the number of loyalty counters on Nissa, Steward of Elements, you may put that card onto the battlefield.\n[−6]: Untap up to two target lands you control. They become 5/5 Elemental creatures with flying and haste until end of turn. They're still lands.;;Legendary Planeswalker — Nissa;Planeswalker;fcdd9620-9c54-5a56-adc4-13a277babc20;;
";

        private const string TokensCsv = @"
artist;artistIds;asciiName;availability;boosterTypes;borderColor;colorIdentity;colors;edhrecSaltiness;faceName;finishes;flavorName;flavorText;frameEffects;frameVersion;hasFoil;hasNonFoil;isFullArt;isFunny;isOversized;isPromo;isReprint;isTextless;keywords;language;layout;manaCost;name;number;orientation;originalText;originalType;otherFaceIds;power;promoTypes;relatedCards;reverseRelated;securityStamp;setCode;side;signature;subtypes;supertypes;text;toughness;type;types;uuid;watermark
XiaoDi Jin;28f8a8a9-c5da-46c7-9cfb-9bb18f3a6309;;paper;default;black;R;R;;;nonfoil;;;legendary;2015;0;1;;;;;;;Flying;English;token;;Karox Bladewing;10;;;;;4;;{""reverseRelated"": [""Verix Bladewing""]};Verix Bladewing;;TDOM;;;Dragon;Legendary;Flying;4;Token Legendary Creature — Dragon;Token, Creature;e4dcfe4f-8441-5eec-9f74-a7b3672e90e0;
Bud Cook;4c3be2d4-73e6-4005-b897-8ac65b9c8660;;paper;default;black;B;B;;;nonfoil;;;;2003;0;1;1;;;;1;;;English;token;;Zombie;3;;;;;2;setpromo;{""reverseRelated"": [""Acererak the Archlich"", ""Aphemia, the Cacophony"", ""Archdemon of Unx"", ""Army of the Damned"", ""Assemble the Rank and Vile"", ""Awaken the Erstwhile"", ""Bone Miser"", ""Boneclad Necromancer"", ""Bridge from Below"", ""Captive Audience"", ""Cellar Door"", ""Cemetery Reaper"", ""Cradle of the Accursed"", ""Cryptbreaker"", ""Curse of Disturbance"", ""Curse of Shallow Graves"", ""Dark Salvation"", ""Death Tyrant"", ""Diregraf Colossus"", ""Doomed Dissenter"", ""Drana's Chosen"", ""Dread Summons"", ""Drunau Corpse Trawler"", ""Dunes of the Dead"", ""Dying to Serve"", ""Empty the Pits"", ""Endless Ranks of the Dead"", ""Feast or Famine"", ""Field of the Dead"", ""From Under the Floorboards"", ""Ghoulcaller Gisa"", ""Ghoulcaller's Accomplice"", ""Gisa's Bidding"", ""Graf Harvest"", ""Grave Titan"", ""Gravedig"", ""Graveyard Marshal"", ""Grixis Slavedriver"", ""Havengul Runebinder"", ""Headless Rider"", ""Hour of Promise"", ""Invasion of Innistrad // Deluge of the Dead"", ""Kalitas, Traitor of Ghet"", ""Lair of the Ashen Idol"", ""Liliana's Devotee"", ""Liliana's Mastery"", ""Liliana's Reaver"", ""Liliana, Death's Majesty"", ""Liliana, Dreadhorde General"", ""Liliana, Heretical Healer // Liliana, Defiant Necromancer"", ""Liliana, the Last Hope"", ""Liliana, the Last Hope Emblem"", ""Maalfeld Twins"", ""Magus of the Bridge"", ""Midnight Ritual"", ""Moan of the Unhallowed"", ""Necromancer's Covenant"", ""Necromancer's Stockpile"", ""Necromaster Dragon"", ""Necromentia"", ""Necrotic Hex"", ""Never // Return"", ""Nevinyrral, Urborg Tyrant"", ""Noosegraf Mob"", ""Null Caller"", ""Oath of Liliana"", ""Oglor, Devoted Assistant"", ""Open the Graves"", ""Overseer of the Damned"", ""Rakshasa Gravecaller"", ""Rank Officer"", ""Reap the Seagraf"", ""Rise from the Tides"", ""Rotlung Reanimator"", ""Rotted Ones, Lay Siege"", ""Sarcomancy"", ""Shamble Back"", ""Sidisi, Brood Tyrant"", ""Skull Skaab"", ""Stir the Sands"", ""Suspicious Shambler"", ""Syphon Flesh"", ""The Book of Vile Darkness"", ""The Fourth Sphere"", ""The Necrobloom"", ""Tobias, Doomed Conqueror"", ""Tombstone Stairwell"", ""Tormod, the Desecrator"", ""Tymaret Calls the Dead"", ""Undead Alchemist"", ""Undead Servant"", ""Unscythe, Killer of Kings"", ""Varina, Lich Queen"", ""Vile Rebirth"", ""Wakedancer"", ""Wand of Orcus"", ""Waste Not"", ""Wight"", ""Xathrid Necromancer"", ""Zombie Infestation""]};Acererak the Archlich, Aphemia, the Cacophony, Archdemon of Unx, Army of the Damned, Assemble the Rank and Vile, Awaken the Erstwhile, Bone Miser, Boneclad Necromancer, Bridge from Below, Captive Audience, Cellar Door, Cemetery Reaper, Cradle of the Accursed, Cryptbreaker, Curse of Disturbance, Curse of Shallow Graves, Dark Salvation, Death Tyrant, Diregraf Colossus, Doomed Dissenter, Drana's Chosen, Dread Summons, Drunau Corpse Trawler, Dunes of the Dead, Dying to Serve, Empty the Pits, Endless Ranks of the Dead, Feast or Famine, Field of the Dead, From Under the Floorboards, Ghoulcaller Gisa, Ghoulcaller's Accomplice, Gisa's Bidding, Graf Harvest, Grave Titan, Gravedig, Graveyard Marshal, Grixis Slavedriver, Havengul Runebinder, Headless Rider, Hour of Promise, Invasion of Innistrad // Deluge of the Dead, Kalitas, Traitor of Ghet, Lair of the Ashen Idol, Liliana's Devotee, Liliana's Mastery, Liliana's Reaver, Liliana, Death's Majesty, Liliana, Dreadhorde General, Liliana, Heretical Healer // Liliana, Defiant Necromancer, Liliana, the Last Hope, Liliana, the Last Hope Emblem, Maalfeld Twins, Magus of the Bridge, Midnight Ritual, Moan of the Unhallowed, Necromancer's Covenant, Necromancer's Stockpile, Necromaster Dragon, Necromentia, Necrotic Hex, Never // Return, Nevinyrral, Urborg Tyrant, Noosegraf Mob, Null Caller, Oath of Liliana, Oglor, Devoted Assistant, Open the Graves, Overseer of the Damned, Rakshasa Gravecaller, Rank Officer, Reap the Seagraf, Rise from the Tides, Rotlung Reanimator, Rotted Ones, Lay Siege, Sarcomancy, Shamble Back, Sidisi, Brood Tyrant, Skull Skaab, Stir the Sands, Suspicious Shambler, Syphon Flesh, The Book of Vile Darkness, The Fourth Sphere, The Necrobloom, Tobias, Doomed Conqueror, Tombstone Stairwell, Tormod, the Desecrator, Tymaret Calls the Dead, Undead Alchemist, Undead Servant, Unscythe, Killer of Kings, Varina, Lich Queen, Vile Rebirth, Wakedancer, Wand of Orcus, Waste Not, Wight, Xathrid Necromancer, Zombie Infestation;;TM11;;;Zombie;;;2;Token Creature — Zombie;Token, Creature;011a9246-7f7c-50c7-ab99-3fc13469c13b;
;;;paper;;borderless;;;0.64;Island;nonfoil, foil, signed;;;;2015;1;1;;;;;;;;English;art_series;;Island // Island;22;;;;f565a88d-d448-5c12-90b3-23922d47b9f9;;stamped;;;;AFDN;b;;;;;;Card;Card;5105c4cc-3589-555b-8e96-645e6744a7a5;
Chris Rahn;7742047e-0f80-4c0f-a530-d07460165e86;;paper;;borderless;;;0.79;All Will Be One;nonfoil, foil, signed;;;;2015;1;1;;;;;;;;English;art_series;;All Will Be One // All Will Be One;25;horizontal;;;75e2741e-f3e4-5f9d-bebd-c591e0c36430;;;;;;AONE;b;Chris Rahn;;;;;Card;Card;9c96fa13-76b8-5d5e-a647-7bfded2528ed;
Seb McKinnon;ad4caca0-8d89-44ce-a1a6-d5ca905bd6fb;;paper;;borderless;;;0.03;Silent Clearing;nonfoil, signed;;;;2015;0;1;;;;;;;;English;art_series;;Silent Clearing // Silent Clearing;51;;;;0b952d69-5db0-59c2-810b-d4b10d452872;;;;;;AMH1;b;Seb McKinnon;;;;;Card;Card;73c978c2-e1ec-59b5-9341-de3ea776839b;
Maxime Minard;a9065769-afcd-4e54-a3c0-5809e7b4108b;;paper;;black;R;R;;;nonfoil, foil;;;;2015;1;1;;;;;1;;;English;token;;Devil;7;;;;;1;;{""reverseRelated"": [""Burn Down the House"", ""Dance with Devils"", ""Devils' Playground"", ""I Call for Slaughter"", ""Maestros Diabolist"", ""Make Mischief"", ""Ob Nixilis, the Adversary"", ""Pugnacious Pugilist"", ""Raphael, Fiendish Savior"", ""Spiked Corridor // Torture Pit"", ""Tibalt, Rakish Instigator"", ""Tibalt, Wicked Tormentor"", ""You Exist Only to Amuse"", ""Zariel, Archduke of Avernus"", ""Zurzoth, Chaos Rider""]};Burn Down the House, Dance with Devils, Devils' Playground, I Call for Slaughter, Maestros Diabolist, Make Mischief, Ob Nixilis, the Adversary, Pugnacious Pugilist, Raphael, Fiendish Savior, Spiked Corridor // Torture Pit, Tibalt, Rakish Instigator, Tibalt, Wicked Tormentor, You Exist Only to Amuse, Zariel, Archduke of Avernus, Zurzoth, Chaos Rider;;TDSC;;;Devil;;When this creature dies, it deals 1 damage to any target.;1;Token Creature — Devil;Token, Creature;db939e9b-b7c1-5084-9880-1856f29766c5;
April Prime;266f773b-5c80-4803-9b95-5a985af90548;;paper;;black;W;W;;;nonfoil, foil;;;;2015;1;1;;;;;1;;;English;token;;Cat;5;;;;;2;;{""reverseRelated"": [""Ajani's Chosen"", ""Ajani, Caller of the Pride"", ""Kemba, Kha Enduring"", ""Kemba, Kha Regent"", ""White Sun's Zenith""]};Ajani's Chosen, Ajani, Caller of the Pride, Kemba, Kha Enduring, Kemba, Kha Regent, White Sun's Zenith;;TWOC;;;Cat;;;2;Token Creature — Cat;Token, Creature;16d88c3b-e766-50ba-8086-917159167368;
Jeff Dee;623129e5-3984-4d9b-b412-9b283a51b81d;;paper;;borderless;;;0.05;Jan Jansen, Chaos Crafter;nonfoil, foil, signed;;;;2015;1;1;;;;;;;;English;art_series;;Jan Jansen, Chaos Crafter // Jan Jansen, Chaos Crafter;66;vertical;;;975e6e71-0680-5a0d-ac59-03df36f67dda;;;;;;ACLB;a;Jeff Dee;;;;;Card;Card;f7161b69-1d04-5f7b-92fe-ea2fd8bf8e88;
Ryan Pancoast;89cc9475-dda2-4d13-bf88-54b92867a25c;;paper;;borderless;;;0.64;Ranger-Captain of Eos;nonfoil, signed;;;;2015;0;1;;;;;;;;English;art_series;;Ranger-Captain of Eos // Ranger-Captain of Eos;11;;;;3972aae0-a52d-5593-914d-0fd42d24ee3d;;;;;;AMH1;a;Ryan Pancoast;;;;;Card;Card;701dcbc1-659a-5b77-8eda-2157fec85d8d;
Filipe Pagliuso;64c537b4-a864-4051-9a24-fbdd22cb40b4;;paper;;borderless;;;0.21;Rampant Frogantua;nonfoil, foil, signed;;;;2015;1;1;;;;;;;;English;art_series;;Rampant Frogantua // Rampant Frogantua;32;;;;3951c090-e5dd-589e-b74b-ab8e03a895a7;;stamped;;;;AMH3;a;Filipe Pagliuso;;;;;Card;Card;aaf9b28b-4552-5665-b719-b8711215002b;
Ryan Yee;8955dca7-3e37-42b4-83a9-167c78a2178f;;paper;;borderless;;;1.0;Sythis, Harvest's Hand;nonfoil, foil, signed;;;;2015;1;1;;;;;;;;English;art_series;;Sythis, Harvest's Hand // Sythis, Harvest's Hand;59;horizontal;;;62118980-b180-5006-80c4-cdc91d1ca44a;;;;;;AMH2;b;Ryan Yee;;;;;Card;Card;e3b480fe-e63e-5af7-bd02-da93b5e9d87e;
Kekai Kotaki;4b771085-c049-4308-930d-ec9665f803a4;;paper;;borderless;;;;Bloodvial Purveyor;nonfoil, foil, signed;;;;2015;1;1;;;;;;;;English;art_series;;Bloodvial Purveyor // Bloodvial Purveyor;72;horizontal;;;4f374ba7-d810-5f2f-945b-2f8fd7d600ad;;;;;;AVOW;a;Kekai Kotaki;;;;;Card;Card;078d4e89-af80-58ae-9be8-7efd5eab2269;
Raymond Swanland;e956bacc-077d-4c12-b6bc-ba798b718af9;;paper;;borderless;;;0.26;Shadrix Silverquill;nonfoil, foil, signed;;;;2015;1;1;;;;;;;;English;art_series;;Shadrix Silverquill // Shadrix Silverquill;59;horizontal;;;4767463a-62d8-5706-89e5-6e1155cd4d3e;;;;;;ASTX;b;Raymond Swanland;;;;;Card;Card;a3d9c702-510c-5d47-a0d4-9cce72d29d66;
Julia Griffin;35d4a4a6-ac7d-4c48-8060-97325f1a2a85;;paper;;black;R, U;R, U;;;nonfoil, foil;;;;2015;1;1;;;;;;;Prowess;English;token;;Otter;25;;;;;1;;{""reverseRelated"": [""Otterball Antics"", ""Ral, Crackling Wit"", ""Stormchaser's Talent""]};Otterball Antics, Ral, Crackling Wit, Stormchaser's Talent;;TBLB;;;Otter;;Prowess (Whenever you cast a noncreature spell, this creature gets +1/+1 until end of turn.);1;Token Creature — Otter;Token, Creature;49481296-5e87-500b-9d95-8011f432466a;
Esad Ribic;86884c1c-7d4b-4543-9141-a2701d9e09a5;;paper;;black;;;;;nonfoil;;;;2015;0;1;;;;;;;;English;token;;Warriors;35;;;;;;;;;;FJ25;;;;;(Theme color: {R}.);;Card;Card;0fc5756d-2258-5d14-b4c4-65aa4efa8f3e;
Michael C. Hayes;d119119f-9ee1-4cf1-a01f-e90b6a042155;;paper;;borderless;;;0.09;Blossoming Calm;nonfoil, signed;;;;2015;0;1;;;;;;;;English;art_series;;Blossoming Calm // Blossoming Calm;1s;horizontalstamped;;;978e07d3-d388-5379-8c20-f67c6af0936d;;stamped;;;;AMH2;b;Michael C. Hayes;;;;;Card;Card;e9f0c596-9d75-59e1-bebb-ea3b441e9cd6;
Filip Burburan;66082c3b-a623-4d34-be51-2475214b85d3;;paper;;borderless;;;0.31;Unblinking Observer;nonfoil, foil, signed;;;;2015;1;1;;;;;;;;English;art_series;;Unblinking Observer // Unblinking Observer;31;horizontal;;;fcfa94d1-04fc-5ef5-bee3-a1296627c4c1;;;;;;AMID;b;Filip Burburan;;;;;Card;Card;174fab69-feac-5f8a-849e-d4171d1a5fbd;
Alayna Danner;bb677b1a-ce51-4888-83d6-5a94de461ff9;;paper;;black;W;W;;;nonfoil, foil;;;;2015;1;1;;;;;1;;;English;token;;Dog;1;;;;;1;;{""reverseRelated"": [""Dog Walker"", ""Hunted Bonebrute"", ""Krovod Haunch"", ""Release the Dogs"", ""Rin and Seri, Inseparable // Rin and Seri, Inseparable""]};Dog Walker, Hunted Bonebrute, Krovod Haunch, Release the Dogs, Rin and Seri, Inseparable // Rin and Seri, Inseparable;;TMKM;;;Dog;;;1;Token Creature — Dog;Token, Creature;6f222117-73e0-5b15-8f8d-529bfa2cdbc1;
;;;paper;;borderless;;;0.37;Season of Weaving;nonfoil, foil, signed;;;;2015;1;1;;;;;;;;English;art_series;;Season of Weaving // Season of Weaving;12;;;;135a9490-d909-58cf-b069-fda758ec14ea;;;;;;ABLB;a;;;;;;Card;Card;b3f7e41e-3606-56d0-88c5-82360063aecd;
Kev Walker;f366a0ee-a0cd-466d-ba6a-90058c7a31a6;;paper;;black;R;R;;;nonfoil, foil;;;;2015;1;1;;;;;1;;;English;token;;Goblin;13;;;;;1;;{""reverseRelated"": [""A Killer Among Us"", ""Ardoz, Cobbler of War"", ""Battle Cry Goblin"", ""Beetleback Chief"", ""Blast from the Past"", ""Box of Free-Range Goblins"", ""Den of the Bugbear"", ""Dragon Fodder"", ""Empty the Warrens"", ""Garbage Elemental"", ""General Kreat, the Boltbringer"", ""Gift Horse"", ""Goblin Assault"", ""Goblin Gang Leader"", ""Goblin Gathering"", ""Goblin Goliath"", ""Goblin Instigator"", ""Goblin Marshal"", ""Goblin Morningstar"", ""Goblin Negotiation"", ""Goblin Offensive"", ""Goblin Rabblemaster"", ""Goblin Rally"", ""Goblin Surprise"", ""Goblin Traprunner"", ""Goblin War Party"", ""Goblin Warrens"", ""Goblinslide"", ""Hordeling Outburst"", ""Hunted Phantasm"", ""Ib Halfheart, Goblin Tactician"", ""Jund"", ""Kathari Bomber"", ""Krenko's Command"", ""Krenko, Baron of Tin Street"", ""Krenko, Mob Boss"", ""Krenko, Tin Street Kingpin"", ""Kuldotha Rebirth"", ""Legion Warboss"", ""Lost Mine of Phandelver"", ""Mardu Ascendancy"", ""Mogg Alarm"", ""Mogg Infestation"", ""Mogg War Marshal"", ""Pashalik Mons"", ""Ponyback Brigade"", ""Rasputin, the Oneiromancer"", ""Rulik Mons, Warren Chief"", ""Sarpadian Empires, Vol. VII"", ""Searslicer Goblin"", ""Siege-Gang Commander"", ""Siege-Gang Lieutenant"", ""Sling-Gang Lieutenant"", ""Squee, Dubious Monarch"", ""Survey the Wreckage"", ""Swarming Goblins"", ""Tin Street Cadet"", ""Warbreak Trumpeter"", ""You See a Pair of Goblins""]};A Killer Among Us, Ardoz, Cobbler of War, Battle Cry Goblin, Beetleback Chief, Blast from the Past, Box of Free-Range Goblins, Den of the Bugbear, Dragon Fodder, Empty the Warrens, Garbage Elemental, General Kreat, the Boltbringer, Gift Horse, Goblin Assault, Goblin Gang Leader, Goblin Gathering, Goblin Goliath, Goblin Instigator, Goblin Marshal, Goblin Morningstar, Goblin Negotiation, Goblin Offensive, Goblin Rabblemaster, Goblin Rally, Goblin Surprise, Goblin Traprunner, Goblin War Party, Goblin Warrens, Goblinslide, Hordeling Outburst, Hunted Phantasm, Ib Halfheart, Goblin Tactician, Jund, Kathari Bomber, Krenko's Command, Krenko, Baron of Tin Street, Krenko, Mob Boss, Krenko, Tin Street Kingpin, Kuldotha Rebirth, Legion Warboss, Lost Mine of Phandelver, Mardu Ascendancy, Mogg Alarm, Mogg Infestation, Mogg War Marshal, Pashalik Mons, Ponyback Brigade, Rasputin, the Oneiromancer, Rulik Mons, Warren Chief, Sarpadian Empires, Vol. VII, Searslicer Goblin, Siege-Gang Commander, Siege-Gang Lieutenant, Sling-Gang Lieutenant, Squee, Dubious Monarch, Survey the Wreckage, Swarming Goblins, Tin Street Cadet, Warbreak Trumpeter, You See a Pair of Goblins;;TM3C;;;Goblin;;;1;Token Creature — Goblin;Token, Creature;f0bb7e06-9698-5952-b890-442096073675;
"; // Add additional rows as needed.

        private const string SetsCsv = @"
baseSetSize;block;cardsphereSetId;code;isFoilOnly;isForeignOnly;isNonFoilOnly;isOnlineOnly;isPartialPreview;keyruneCode;languages;mcmId;mcmIdExtras;mcmName;mtgoCode;name;parentCode;releaseDate;tcgplayerGroupId;tokenSetCode;totalSetSize;type
350;Judge Gift Cards;;G99;1;;;0;;DEFAULT;English;;;;;Judge Gift Cards 1999;;01-01-1999;62;;1;promo
117;;;WC98;0;;1;0;;DEFAULT;English;;;;;World Championship Decks 1998;;12-08-1998;;WC98;107;memorabilia
18;Commander;792;CM1;1;;;0;;CM1;English;1418;;Commander's Arsenal;;Commander's Arsenal;;02-11-2012;568;;18;arsenal
4;Khans of Tarkir;805;PTKDF;0;;1;0;;DTK;English;;;;;Tarkir Dragonfury;DTK;03-04-2015;1520;;4;promo
64;Alchemy 2022;;YMID;0;;1;1;;Y22;English;;;;;Alchemy: Innistrad;;09-12-2021;;;64;alchemy
243;Portal;900;POR;0;;1;0;;POR;Chinese Simplified, Chinese Traditional, English, French, German, Japanese, Spanish;25;;Portal;;Portal;;01-05-1997;86;;257;starter
4;Commander;;OC18;1;;;0;;C18;English;;;;;Commander 2018 Oversized;C18;09-08-2018;;;4;memorabilia
143;Odyssey;933;TOR;0;;;0;;TOR;Chinese Simplified, Chinese Traditional, English, French, German, Italian, Japanese, Portuguese (Brazil), Spanish;39;;Torment;TOR;Torment;;04-02-2002;112;;143;expansion
350;Tempest;927;TMP;0;;1;0;;TMP;Chinese Traditional, English, French, German, Italian, Japanese, Korean, Portuguese (Brazil), Spanish;19;;Tempest;TE;Tempest;;14-10-1997;108;;350;expansion
1;;;PMIC;0;;1;0;;PAST;English;;;;;MicroProse Promos;PAST;01-04-1997;;;1;memorabilia
8;Judge Gift Cards;;G11;1;;;0;;PARL;English;;;;;Judge Gift Cards 2011;;01-01-2011;62;;8;promo
248;Zendikar;914;ROE;0;;;0;;ROE;Chinese Simplified, English, French, German, Italian, Japanese, Portuguese (Brazil), Russian, Spanish;120;;Rise of the Eldrazi;ROE;Rise of the Eldrazi;;23-04-2010;98;TROE;248;expansion
5;Innistrad;;PDKA;0;;;0;;DKA;English;;;;;Dark Ascension Promos;DKA;28-01-2012;;;7;promo
83;Kaladesh;;PKLD;0;;;0;;KLD;English;;;;;Kaladesh Promos;KLD;30-09-2016;;;83;promo
88;;817;DDM;0;;;0;;DDM;English, Japanese;1477;;Duel Decks: Jace vs. Vraska;DDM;Duel Decks: Jace vs. Vraska;;14-03-2014;1166;TDDM;88;duel_deck
456;Alchemy 2022;;HBG;0;;1;1;;HBG;English;;;;;Alchemy Horizons: Baldur's Gate;;07-07-2022;;;424;alchemy
180;Ravnica;802;DIS;0;;;0;;DIS;Chinese Simplified, English, French, German, Italian, Japanese, Portuguese (Brazil), Russian, Spanish;53;;Dissension;DIS;Dissension;;05-05-2006;28;;190;expansion
229;;885;MMA;0;;;0;;MMA;English;1444;;Modern Masters;MMA;Modern Masters;;07-06-2013;1111;TMMA;229;masters
306;Mirrodin;881;MRD;0;;;0;;MRD;Chinese Simplified, Chinese Traditional, English, French, German, Italian, Japanese, Portuguese (Brazil), Spanish;45;;Mirrodin;MRD;Mirrodin;;02-10-2003;75;;306;expansion
0;;;PTDMU;0;;1;0;;DMU;English;;;;;Dominaria United Southeast Asia Tokens;DMU;09-09-2022;;PTDMU;0;token
";

        private const string MyCollectionCsv = @"
id;uuid;count;trade;condition;language;finish
2249;f1e4acc1-1bb7-57ea-9d61-edb3e803ab5c;1;0;Near Mint;English;nonfoil
4454;28a21701-487e-5d54-9bb2-22f862734499;1;0;Near Mint;French;nonfoil
7546;0add0930-720f-5bf5-bcf5-ee208eeb9040;1;0;Near Mint;English;foil
11817;aa480225-6f03-5f0f-85af-41af75e515aa;2;0;Near Mint;English;nonfoil
10772;9c015664-e6e8-53a4-ad48-276138b18098;3;0;Played;Japanese;nonfoil
10001;154a09f3-65e3-5821-bc02-bd972b3be676;1;0;Near Mint;English;nonfoil
5388;413e11a5-35a1-51c7-928b-219b4453a094;1;0;Near Mint;English;nonfoil
6245;5e6a3099-2597-5755-8a6f-67f1569a3b8a;4;1;Near Mint;English;nonfoil
5339;91556c5f-b11e-573d-a3bb-627dfa6c2926;2;0;Mint;English;nonfoil
4798;5ce32715-8dee-5c47-986a-88d00e87c506;1;0;Near Mint;English;foil
2337;fd1183ad-ae98-5c4b-b93d-97d32b74d999;4;0;Near Mint;English;nonfoil
4933;d0bcc932-d1f2-509c-90de-42512b6fef75;1;0;Near Mint;Korean;nonfoil
8052;358c5647-9f47-5f5d-9497-36336d8c7bb9;1;0;Near Mint;Korean;nonfoil
8721;7be5b8a9-0d68-5125-b729-ff1063dd3ed0;2;0;Poor;English;nonfoil
7683;cc6ce5a2-49d0-59de-a17e-362f47b67773;1;0;Near Mint;English;foil
2970;16f1c97a-b896-59d0-80ab-6f00a6ea1d28;1;0;Excellent;English;nonfoil
4025;e7613acb-a554-5a49-aa38-88fff17503fe;4;0;Near Mint;English;nonfoil
11081;1fb6b965-6d0e-536c-8593-63140f3c6e9f;2;0;Good;English;nonfoil
10980;8d6b22fe-8583-5908-b964-05e3174e3154;7;3;Good;English;nonfoil
11808;66dae17d-a742-51b4-ba09-0b37d7c64265;1;0;Near Mint;English;nonfoil
547;d4588e8f-e5a0-53e5-ac90-0a5183f0d118;1;1;Near Mint;English;signed
8163;bafac74c-f4f8-5c71-8a6b-0bd02c536c47;1;1;Near Mint;English;etched
";

        public InMemoryDatabaseFixture()
        {
            // Create an in-memory SQLite database.
            Connection = new SQLiteConnection("Data Source=:memory:;Version=3;");
            Connection.Open();

            // Create tables.
            SetupSchema();

            // Seed tables with CSV data.
            // Synchronously seed tables with CSV data.
            SeedDataAsync().GetAwaiter().GetResult();
        }

        private void SetupSchema()
        {
            using var command = new SQLiteCommand(Connection);
            // Create table: cards
            command.CommandText = @"
                CREATE TABLE cards (
                    artist TEXT,
                    artistIds TEXT,
                    asciiName TEXT,
                    attractionLights TEXT,
                    availability TEXT,
                    boosterTypes TEXT,
                    borderColor TEXT,
                    cardParts TEXT,
                    colorIdentity TEXT,
                    colorIndicator TEXT,
                    colors TEXT,
                    defense TEXT,
                    duelDeck TEXT,
                    edhrecRank INTEGER,
                    edhrecSaltiness FLOAT,
                    faceConvertedManaCost FLOAT,
                    faceFlavorName TEXT,
                    faceManaValue FLOAT,
                    faceName TEXT,
                    finishes TEXT,
                    flavorName TEXT,
                    flavorText TEXT,
                    frameEffects TEXT,
                    frameVersion TEXT,
                    hand TEXT,
                    hasAlternativeDeckLimit BOOLEAN,
                    hasContentWarning BOOLEAN,
                    hasFoil BOOLEAN,
                    hasNonFoil BOOLEAN,
                    isAlternative BOOLEAN,
                    isFullArt BOOLEAN,
                    isFunny BOOLEAN,
                    isOnlineOnly BOOLEAN,
                    isOversized BOOLEAN,
                    isPromo BOOLEAN,
                    isRebalanced BOOLEAN,
                    isReprint BOOLEAN,
                    isReserved BOOLEAN,
                    isStarter BOOLEAN,
                    isStorySpotlight BOOLEAN,
                    isTextless BOOLEAN,
                    isTimeshifted BOOLEAN,
                    keywords TEXT,
                    language TEXT,
                    layout TEXT,
                    leadershipSkills TEXT,
                    life TEXT,
                    loyalty TEXT,
                    manaCost TEXT,
                    manaValue FLOAT,
                    name TEXT,
                    number TEXT,
                    originalPrintings TEXT,
                    originalReleaseDate TEXT,
                    originalText TEXT,
                    originalType TEXT,
                    otherFaceIds TEXT,
                    power TEXT,
                    printings TEXT,
                    promoTypes TEXT,
                    rarity TEXT,
                    rebalancedPrintings TEXT,
                    relatedCards TEXT,
                    securityStamp TEXT,
                    setCode TEXT,
                    side TEXT,
                    signature TEXT,
                    sourceProducts TEXT,
                    subsets TEXT,
                    subtypes TEXT,
                    supertypes TEXT,
                    text TEXT,
                    toughness TEXT,
                    type TEXT,
                    types TEXT,
                    uuid VARCHAR(36) NOT NULL,
                    variations TEXT,
                    watermark TEXT
                );
            ";
            command.ExecuteNonQuery();

            // Create table: tokens
            command.CommandText = @"
                CREATE TABLE tokens (
                    artist TEXT,
                    artistIds TEXT,
                    asciiName TEXT,
                    availability TEXT,
                    boosterTypes TEXT,
                    borderColor TEXT,
                    colorIdentity TEXT,
                    colors TEXT,
                    edhrecSaltiness FLOAT,
                    faceName TEXT,
                    finishes TEXT,
                    flavorName TEXT,
                    flavorText TEXT,
                    frameEffects TEXT,
                    frameVersion TEXT,
                    hasFoil BOOLEAN,
                    hasNonFoil BOOLEAN,
                    isFullArt BOOLEAN,
                    isFunny BOOLEAN,
                    isOversized BOOLEAN,
                    isPromo BOOLEAN,
                    isReprint BOOLEAN,
                    isTextless BOOLEAN,
                    keywords TEXT,
                    language TEXT,
                    layout TEXT,
                    manaCost TEXT,
                    name TEXT,
                    number TEXT,
                    orientation TEXT,
                    originalText TEXT,
                    originalType TEXT,
                    otherFaceIds TEXT,
                    power TEXT,
                    promoTypes TEXT,
                    relatedCards TEXT,
                    reverseRelated TEXT,
                    securityStamp TEXT,
                    setCode TEXT,
                    side TEXT,
                    signature TEXT,
                    subtypes TEXT,
                    supertypes TEXT,
                    text TEXT,
                    toughness TEXT,
                    type TEXT,
                    types TEXT,
                    uuid VARCHAR(36) NOT NULL,
                    watermark TEXT
                );
            ";
            command.ExecuteNonQuery();

            // Create table: sets
            command.CommandText = @"
                CREATE TABLE sets (
                    baseSetSize INTEGER,
                    block TEXT,
                    cardsphereSetId INTEGER,
                    code VARCHAR(8) UNIQUE NOT NULL,
                    isFoilOnly BOOLEAN,
                    isForeignOnly BOOLEAN,
                    isNonFoilOnly BOOLEAN,
                    isOnlineOnly BOOLEAN,
                    isPartialPreview BOOLEAN,
                    keyruneCode TEXT,
                    languages TEXT,
                    mcmId INTEGER,
                    mcmIdExtras INTEGER,
                    mcmName TEXT,
                    mtgoCode TEXT,
                    name TEXT,
                    parentCode TEXT,
                    releaseDate TEXT,
                    tcgplayerGroupId INTEGER,
                    tokenSetCode TEXT,
                    totalSetSize INTEGER,
                    type TEXT
                );
            ";
            command.ExecuteNonQuery();

            // Create table: myCollection
            command.CommandText = @"
                CREATE TABLE myCollection (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    uuid TEXT,
                    count INTEGER,
                    trade INTEGER,
                    condition TEXT,
                    language TEXT,
                    finish TEXT
                );
            ";
            command.ExecuteNonQuery();
        }

        private async Task SeedDataAsync()
        {
            // Seed each table from its CSV seed string.
            await SeedTableAsync("cards", CardsCsv);
            await SeedTableAsync("tokens", TokensCsv);
            await SeedTableAsync("sets", SetsCsv);
            await SeedTableAsync("myCollection", MyCollectionCsv);
        }

        // A helper method to seed a table from CSV data.
        // Assumes semicolon ';' as delimiter.
        private async Task SeedTableAsync(string tableName, string csvData)
        {
            // Use StringReader to read the CSV data.
            using var reader = new StringReader(csvData);
            // Configure CsvHelper to use semicolon as delimiter.
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HasHeaderRecord = true,
                Quote = '"',
                // Log any bad data using RawRecord only.
                BadDataFound = args => Debug.WriteLine($"Bad data found: {args.RawRecord}")
            };

            using var csv = new CsvReader(reader, config);
            csv.Read();
            csv.ReadHeader();
            var headers = csv.HeaderRecord;
            if (headers == null || headers.Length == 0)
                throw new Exception("CSV file missing headers.");

            // Build parameter names for the INSERT command.
            var parameters = string.Join(", ", headers.Select((h, i) => $"@p{i}"));
            string insertSql = $"INSERT INTO {tableName} ({string.Join(", ", headers)}) VALUES ({parameters});";
            Debug.WriteLine($"Seeding table '{tableName}' using SQL: {insertSql}");

            using var transaction = Connection.BeginTransaction();
            using var cmd = new SQLiteCommand(insertSql, Connection, transaction);

            // Add parameters.
            for (int i = 0; i < headers.Length; i++)
            {
                cmd.Parameters.Add(new SQLiteParameter($"@p{i}"));
            }

            int rowIndex = 1; // starting after header
            while (await csv.ReadAsync())
            {
                // For each header, get the field value.
                for (int i = 0; i < headers.Length; i++)
                {
                    string field = csv.GetField(headers[i]);
                    // Use DBNull if the field is empty.
                    cmd.Parameters[$"@p{i}"].Value = string.IsNullOrWhiteSpace(field) ? (object)DBNull.Value : field;
                }

                try
                {
                    cmd.ExecuteNonQuery();
                    rowIndex++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error inserting row {rowIndex} into table '{tableName}'. Raw row: {csv.Context.Parser.RawRecord}. Exception: {ex.Message}");
                    throw;
                }
            }
            transaction.Commit();
        }


        public void Dispose()
        {
            Connection?.Close();
            Connection?.Dispose();
        }
    }
}
