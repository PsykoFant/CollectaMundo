using CollectaMundo.ApplicationServices.CardLocations.Models;
using CollectaMundo.ApplicationServices.CollectionMutations;
using CollectaMundo.ApplicationServices.Decks.Models;
using CollectaMundo.ApplicationServices.Shared.Operation;
using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.DomainLogic.CardLocations;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.Shared.CollectionSnapshot;
using CollectaMundo.DomainLogic.Shared.Factories;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.CardLocations;
using CollectaMundo.Infrastructure.Shared.Models;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardLocations
{
    public sealed class CardLocationService(IUnitOfWorkRunner uowRunner, ICardLocationRepo cardLocationRepo, ICardLocationLogic cardLocationLogic, ICardLocationLookupStore cardLocationLookupStore, ICollectionMutationsService mutationsService) : ICardLocationService
    {
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly ICardLocationRepo _cardLocationRepo = cardLocationRepo;
        private readonly ICardLocationLogic _cardLocationLogic = cardLocationLogic;
        private readonly ICardLocationLookupStore _cardLocationLookupStore = cardLocationLookupStore;
        private readonly ICollectionMutationsService _mutationsService = mutationsService;

        // CREATE
        public async Task<MutationResult<CardLocation>> CreateLocationAsync(string name, CardLocationType type)
        {
            try
            {
                var result = await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
                {
                    var locationMutation = await CreateAsync(conn, tx, name, type);

                    return (
                        Result: locationMutation,
                        Commit: locationMutation.Result.Code == OperationResultCode.Success
                    );
                });

                if (result.Entity is not null)
                {
                    _cardLocationLookupStore.Upsert(result.Entity);
                }

                return result;
            }
            catch (Exception ex)
            {
                return new MutationResult<CardLocation>(new OperationResult(OperationResultCode.Error, $"Failed to create location: {ex.Message}"), null);
            }
        }
        public async Task<MutationResult<DeckManagementRecord>> CreateDeckAsync(DeckManagementInput input)
        {
            try
            {
                var result = await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
                {
                    var locationMutation = await CreateAsync(conn, tx, input.Name, CardLocationType.Deck);

                    if (locationMutation.Result.Code != OperationResultCode.Success || locationMutation.Entity is null)
                    {
                        return (
                            Result: new MutationResult<DeckManagementRecord>(locationMutation.Result, null),
                            Commit: false
                        );
                    }

                    await _cardLocationRepo.UpsertMetadataAsync(conn, tx, locationMutation.Entity.Id, input.Format, input.Description);

                    var deck = CreateDeckRecord(locationMutation.Entity.Id, locationMutation.Entity.Name, input);

                    return (Result: new MutationResult<DeckManagementRecord>(new OperationResult(OperationResultCode.Success, "Deck created successfully."), deck), Commit: true);
                });

                if (result.Entity is not null)
                {
                    var deck = result.Entity;
                    _cardLocationLookupStore.Upsert(CreateLocationObject(deck.LocationId, deck.Name, CardLocationType.Deck));
                }

                return result;
            }
            catch (Exception ex)
            {
                return new MutationResult<DeckManagementRecord>(new OperationResult(OperationResultCode.Error, $"Failed to create deck: {ex.Message}"), null);
            }
        }
        public async Task<IReadOnlyList<CardLocation>> CreateMissingLocationsAsStorageAsync(IReadOnlyList<string> names, CancellationToken token)
        {
            var type = CardLocationType.Storage;

            var normalizedNames = names.Select(_cardLocationLogic.NormalizeName).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (normalizedNames.Count == 0)
            {
                return [];
            }

            var validNames = new List<string>();

            foreach (var name in normalizedNames)
            {
                var validation = _cardLocationLogic.ValidateNameAndType(name, type);

                if (validation.Code == OperationResultCode.Success)
                {
                    validNames.Add(name);
                }
            }

            if (validNames.Count == 0)
            {
                return [];
            }

            var createdLocations = await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
            {
                var existingLocations = await _cardLocationRepo.GetAllLocationsAsync(conn, tx);

                var existingNames = existingLocations.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var namesToCreate = validNames.Where(name => !existingNames.Contains(name)).ToList();

                if (namesToCreate.Count == 0)
                {
                    return (
                        Result: (IReadOnlyList<CardLocation>)[],
                        Commit: false
                    );
                }

                string dbType = MapTypeToDb(type);

                var recordsToInsert = namesToCreate.Select(name => (Name: name, Type: dbType)).ToList();

                var createdRecords = await _cardLocationRepo.CreateLocationsAsync(conn, tx, recordsToInsert, token);

                var createdLocations = createdRecords.Select(MapToDomain).ToList();

                return (Result: (IReadOnlyList<CardLocation>)createdLocations, Commit: true);
            });

            if (createdLocations.Count > 0)
            {
                _cardLocationLookupStore.UpsertMany(createdLocations);
            }

            return createdLocations;
        }

        private async Task<MutationResult<CardLocation>> CreateAsync(SQLiteConnection conn, SQLiteTransaction tx, string name, CardLocationType type)
        {
            var validation = _cardLocationLogic.ValidateNameAndType(name, type);

            if (validation.Code != OperationResultCode.Success)
            {
                return new MutationResult<CardLocation>(validation, null);
            }

            string normalizedName = _cardLocationLogic.NormalizeName(name);

            try
            {
                bool exists = await _cardLocationRepo.ExistsByNameAsync(conn, tx, normalizedName);

                if (exists)
                {
                    return new MutationResult<CardLocation>(new OperationResult(OperationResultCode.AlreadyExists, $"A location named '{normalizedName}' already exists."), null);
                }

                string dbType = MapTypeToDb(type);
                int id = await _cardLocationRepo.CreateLocationAsync(conn, tx, normalizedName, dbType);

                var location = CreateLocationObject(id, normalizedName, type);

                return new MutationResult<CardLocation>(new OperationResult(OperationResultCode.Success, "Card location created successfully."), location);
            }
            catch (SQLiteException ex) when (IsDuplicateLocationNameViolation(ex))
            {
                return new MutationResult<CardLocation>(new OperationResult(OperationResultCode.AlreadyExists, $"A location named '{normalizedName}' already exists."), null);
            }
        }

        // READ
        public async Task<IReadOnlyList<CardLocation>> GetAllLocationsAsync()
        {
            var records = await _uowRunner.ExecuteReadOnlyAsync(conn => _cardLocationRepo.GetAllLocationsAsync(conn));

            return [.. records.Select(MapToDomain)];
        }
        public async Task<IReadOnlyList<DeckManagementRecord>> GetAllDecksAsync()
        {
            return await _uowRunner.ExecuteReadOnlyAsync(conn => _cardLocationRepo.GetAllDecksAsync(conn));
        }

        // UPDATE
        public async Task<MutationResult<CardLocation>> UpdateLocationAsync(int id, string name, CardLocationType type)
        {
            try
            {
                var result = await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
                {
                    var coreResult = await UpdateAsync(conn, tx, id, name, type);

                    return (
                        Result: coreResult,
                        Commit: coreResult.Result.Code == OperationResultCode.Success
                    );
                });

                if (result.Entity is not null)
                {
                    _cardLocationLookupStore.Upsert(result.Entity);
                }

                return result;
            }
            catch (Exception ex)
            {
                return new MutationResult<CardLocation>(new OperationResult(OperationResultCode.Error, $"Failed to update location: {ex.Message}"), null);
            }
        }
        public async Task<MutationResult<DeckManagementRecord>> UpdateDeckAsync(int locationId, DeckManagementInput input)
        {
            try
            {
                var result = await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
                {
                    var locationMutation = await UpdateAsync(conn, tx, locationId, input.Name, CardLocationType.Deck);

                    if (locationMutation.Result.Code != OperationResultCode.Success || locationMutation.Entity is null)
                    {
                        return (Result: new MutationResult<DeckManagementRecord>(locationMutation.Result, null), Commit: false);
                    }

                    await _cardLocationRepo.UpsertMetadataAsync(conn, tx, locationId, input.Format, input.Description);

                    var deck = CreateDeckRecord(locationId, locationMutation.Entity.Name, input);

                    return (Result: new MutationResult<DeckManagementRecord>(new OperationResult(OperationResultCode.Success, "Deck updated successfully."), deck), Commit: true);
                });

                if (result.Entity is not null)
                {
                    var deck = result.Entity;
                    _cardLocationLookupStore.Upsert(CreateLocationObject(deck.LocationId, deck.Name, CardLocationType.Deck));
                }

                return result;
            }
            catch (Exception ex)
            {
                return new MutationResult<DeckManagementRecord>(new OperationResult(OperationResultCode.Error, $"Failed to update deck: {ex.Message}"), null);
            }
        }
        public async Task<IReadOnlyList<CardLocation>> UpdateLocationTypesAsync(IReadOnlyList<int> ids, CardLocationType type, CancellationToken token = default)
        {
            var distinctIds = ids.Distinct().ToList();

            if (distinctIds.Count == 0)
            {
                return [];
            }

            string dbType = MapTypeToDb(type);

            var updatedLocations = await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
            {
                var updatedRecords = await _cardLocationRepo.UpdateLocationTypesAsync(conn, tx, distinctIds, dbType, token);

                var updatedLocations = updatedRecords.Select(MapToDomain).ToList();

                return (
                Result: (IReadOnlyList<CardLocation>)updatedLocations,
                    Commit: updatedLocations.Count > 0
                );
            });

            if (updatedLocations.Count > 0)
            {
                _cardLocationLookupStore.UpsertMany(updatedLocations);
            }

            return updatedLocations;
        }
        public async Task<IReadOnlyList<DeckManagementRecord>> UpdateDeckFormatsAsync(IReadOnlyList<DeckManagementRecord> decks, string format, CancellationToken token = default)
        {
            var distinctDecks = decks.GroupBy(deck => deck.LocationId).Select(group => group.First()).ToList();

            if (distinctDecks.Count == 0)
            {
                return [];
            }

            var updatedDecks = await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
            {
                var ids = distinctDecks.Select(deck => deck.LocationId).ToList();

                var existingIds = await _cardLocationRepo.GetExistingLocationIdsAsync(conn, tx, ids, token);

                if (existingIds.Count != ids.Count)
                {
                    return (
                        Result: (IReadOnlyList<DeckManagementRecord>)[],
                        Commit: false
                    );
                }

                await _cardLocationRepo.UpdateDeckFormatsAsync(conn, tx, ids, format, token);

                var result = distinctDecks
                    .Select(deck => new DeckManagementRecord
                    {
                        LocationId = deck.LocationId,
                        Name = deck.Name,
                        Format = format,
                        Description = deck.Description
                    })
                    .ToList();

                return (
                    Result: (IReadOnlyList<DeckManagementRecord>)result,
                    Commit: result.Count > 0
                );
            });

            return updatedDecks;
        }
        private async Task<MutationResult<CardLocation>> UpdateAsync(SQLiteConnection conn, SQLiteTransaction tx, int id, string name, CardLocationType type)
        {
            string normalizedName = _cardLocationLogic.NormalizeName(name);

            var validation = _cardLocationLogic.ValidateNameAndType(normalizedName, type);

            if (validation.Code is not OperationResultCode.Success)
            {
                return new MutationResult<CardLocation>(validation, null);
            }

            try
            {
                bool alreadyExists = await _cardLocationRepo.ExistsByNameAsync(conn, tx, normalizedName, excludingId: id);

                if (alreadyExists)
                {
                    return new MutationResult<CardLocation>(new OperationResult(OperationResultCode.AlreadyExists, $"A location named '{normalizedName}' already exists."), null);
                }

                string dbType = MapTypeToDb(type);

                int rowsAffected = await _cardLocationRepo.UpdateLocationAsync(conn, tx, id, normalizedName, dbType);

                if (rowsAffected == 0)
                {
                    return new MutationResult<CardLocation>(new OperationResult(OperationResultCode.NotFound, $"No location with id {id} was found."), null);
                }

                var updatedLocation = CreateLocationObject(id, normalizedName, type);

                return new MutationResult<CardLocation>(new OperationResult(OperationResultCode.Success, "Location updated successfully."), updatedLocation);
            }
            catch (SQLiteException ex) when (IsDuplicateLocationNameViolation(ex))
            {
                return new MutationResult<CardLocation>(new OperationResult(OperationResultCode.AlreadyExists, $"A location named '{normalizedName}' already exists."), null);
            }
        }


        // DELETE
        public async Task<CardLocationDeleteResult> DeleteLocationsAsync(IReadOnlyList<int> ids, string entityName, CancellationToken token = default)
        {
            var distinctIds = ids.Distinct().ToList();

            try
            {
                var result = await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
                {
                    if (distinctIds.Count == 0)
                    {
                        var emptyResult = new CardLocationDeleteResult(
                            new OperationResult(
                                OperationResultCode.Success,
                                $"No {entityName} selected."),
                            new CollectionChangeSet<CollectionCardDbRow>());
                        return (Result: emptyResult, Commit: false);
                    }

                    // Step 1: validate that all requested locations still exist.
                    var existingIds = await _cardLocationRepo.GetExistingLocationIdsAsync(conn, tx, distinctIds, token);

                    if (existingIds.Count != distinctIds.Count)
                    {
                        var missingIds = distinctIds.Except(existingIds).ToList();
                        var notFoundResult = new CardLocationDeleteResult(new OperationResult(
                            OperationResultCode.NotFound, $"Could not find {entityName}: {string.Join(", ", missingIds)}."),
                            new CollectionChangeSet<CollectionCardDbRow>());

                        return (Result: notFoundResult, Commit: false);
                    }

                    // Step 2: read collection state once and affected rows once.
                    var snapshotRows = await _cardLocationRepo.GetAllCollectionRowsAsync(conn, tx);
                    var affectedRows = await _cardLocationRepo.GetCollectionRowsByLocationIdsAsync(conn, tx, distinctIds, token);

                    // Step 3: build and execute one collection mutation plan for all affected cards.
                    var snapshot = CollectionIdentitySnapshot.FromRows(snapshotRows);
                    var editedCards = affectedRows.Select(CollectionCardDraftFactory.FromDbRowWithClearedLocation).ToList();

                    var changeSet = await _mutationsService.SubmitBatchAsync(editedCards, snapshot, conn, tx);

                    // Step 4: delete desired deck card entries for all deleted deck locations.
                    await _cardLocationRepo.DeleteDeckCardsAsync(conn, tx, distinctIds, token);

                    // Step 5: delete deck metadata for all deleted locations.
                    await _cardLocationRepo.DeleteDecksMetadataAsync(conn, tx, distinctIds, token);

                    // Step 6: delete all selected locations.
                    int deletedLocationCount = await _cardLocationRepo.DeleteLocationsAsync(conn, tx, distinctIds, token);

                    var successResult = new CardLocationDeleteResult(
                        new OperationResult(
                            OperationResultCode.Success,
                            deletedLocationCount == 1
                                ? $"{entityName} deleted successfully."
                                : $"{deletedLocationCount} {entityName} deleted successfully."),
                        changeSet);

                    return (Result: successResult, Commit: deletedLocationCount > 0);
                });

                if (result.Result.Code is OperationResultCode.Success)
                {
                    _cardLocationLookupStore.RemoveMany(distinctIds);
                }

                return result;
            }
            catch (Exception ex)
            {
                return new CardLocationDeleteResult(
                    new OperationResult(
                        OperationResultCode.Error,
                        $"Failed to delete {entityName}: {ex.Message}"),
                    new CollectionChangeSet<CollectionCardDbRow>());
            }
        }


        // Helpers
        private static DeckManagementRecord CreateDeckRecord(int locationId, string name, DeckManagementInput input)
        {
            return new DeckManagementRecord
            {
                LocationId = locationId,
                Name = name,
                Format = input.Format,
                Description = input.Description
            };
        }
        private static CardLocation CreateLocationObject(int id, string name, CardLocationType type)
        {
            return new CardLocation
            {
                Id = id,
                Name = name,
                Type = type
            };
        }
        private static CardLocation MapToDomain(CardLocationDbRow record)
        {
            return CreateLocationObject(record.Id, record.Name, MapTypeFromDb(record.Type));

            static CardLocationType MapTypeFromDb(string dbType)
            {
                return dbType switch
                {
                    "Storage" => CardLocationType.Storage,
                    "Deck" => CardLocationType.Deck,
                    _ => throw new InvalidOperationException($"Unsupported card location type in database: '{dbType}'.")
                };
            }
        }
        private static string MapTypeToDb(CardLocationType type)
        {
            return type switch
            {
                CardLocationType.Storage => "Storage",
                CardLocationType.Deck => "Deck",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported card location type.")
            };
        }
        private static bool IsDuplicateLocationNameViolation(SQLiteException ex)
        {
            if (ex.ResultCode == SQLiteErrorCode.Constraint || ex.ResultCode == SQLiteErrorCode.Constraint_Unique)
            {
                return ex.Message.Contains("cardLocations", StringComparison.OrdinalIgnoreCase) && ex.Message.Contains("name", StringComparison.OrdinalIgnoreCase);
            }

            return ex.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) && ex.Message.Contains("cardLocations", StringComparison.OrdinalIgnoreCase);
        }

    }
}
