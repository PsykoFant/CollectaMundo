using CollectaMundo.ApplicationServices.CardLocations.Models;
using CollectaMundo.ApplicationServices.CollectionMutations;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardLocations;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.CollectionMutations;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.CardLocations;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardLocations
{
    public sealed class CardLocationService(IUnitOfWorkRunner uowRunner, ICardLocationRepo cardLocationRepo, ICardLocationLogic cardLocationLogic, ICardLocationLookupStore cardLocationLookupStore, ICollectionMutationsLogic mutationsLogic, ICollectionMutationsService mutationsService) : ICardLocationService
    {
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly ICardLocationRepo _cardLocationRepo = cardLocationRepo;
        private readonly ICardLocationLogic _cardLocationLogic = cardLocationLogic;
        private readonly ICardLocationLookupStore _cardLocationLookupStore = cardLocationLookupStore;
        private readonly ICollectionMutationsLogic _mutationsLogic = mutationsLogic;
        private readonly ICollectionMutationsService _mutationsService = mutationsService;

        // CREATE
        public async Task<CardLocationMutationResult> CreateLocationAsync(string name, CardLocationType type)
        {
            try
            {
                var result = await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
                    {
                        var result = await CreateCoreAsync(conn, tx, name, type);

                        return (Result: result, Commit: result.Result.Code == OperationResultCode.Success
                        );
                    });

                if (result.Location is not null)
                {
                    _cardLocationLookupStore.Upsert(result.Location);
                }

                return result;
            }
            catch (Exception ex)
            {
                return new CardLocationMutationResult(new OperationResult(OperationResultCode.Error, $"Failed to create location: {ex.Message}"), null);
            }
        }
        public async Task<DeckManagementMutation> CreateDeckAsync(DeckManagementInput input)
        {
            try
            {
                var result = await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
                    {
                        var locationMutation = await CreateCoreAsync(conn, tx, input.Name, CardLocationType.Deck);

                        if (locationMutation.Result.Code != OperationResultCode.Success || locationMutation.Location is null)
                        {
                            return (Result: new DeckManagementMutation { Result = locationMutation.Result }, Commit: false);
                        }

                        await _cardLocationRepo.UpsertMetadataAsync(conn, tx, locationMutation.Location.Id, input.Format, input.Description);

                        return (Result: new DeckManagementMutation
                        {
                            Result = new OperationResult(OperationResultCode.Success, "Deck created successfully."),
                            Deck = CreateDeckRecord(locationMutation.Location.Id, locationMutation.Location.Name, input)
                        },
                            Commit: true
                        );
                    });

                if (result.Deck is not null)
                {
                    _cardLocationLookupStore.Upsert(new CardLocation
                    {
                        Id = result.Deck.LocationId,
                        Name = result.Deck.Name,
                        Type = CardLocationType.Deck
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                return new DeckManagementMutation
                {
                    Result = new OperationResult(OperationResultCode.Error, $"Failed to create deck: {ex.Message}")
                };
            }
        }
        private async Task<CardLocationMutationResult> CreateCoreAsync(SQLiteConnection conn, SQLiteTransaction tx, string name, CardLocationType type)
        {
            var validation = _cardLocationLogic.ValidateForCreate(name, type);

            if (validation.Code != OperationResultCode.Success)
            {
                return new CardLocationMutationResult(validation, null);
            }

            string normalizedName = _cardLocationLogic.NormalizeName(name);

            bool exists = await _cardLocationRepo.ExistsByNameAsync(conn, tx, normalizedName);

            if (exists)
            {
                return new CardLocationMutationResult(new OperationResult(OperationResultCode.AlreadyExists, $"A location named '{normalizedName}' already exists."), null);
            }

            string dbType = MapTypeToDb(type);
            int id = await _cardLocationRepo.InsertAsync(conn, tx, normalizedName, dbType);

            var location = new CardLocation
            {
                Id = id,
                Name = normalizedName,
                Type = type
            };

            return new CardLocationMutationResult(new OperationResult(OperationResultCode.Success, "Card location created successfully."), location);
        }
        public async Task<IReadOnlyList<CardLocation>> CreateMissingLocationsAsStorageAsync(IReadOnlyList<string> names, CardLocationType type, CancellationToken token)
        {
            var normalizedNames = names.Select(_cardLocationLogic.NormalizeName).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (normalizedNames.Count == 0)
            {
                return [];
            }

            var validNames = new List<string>();

            foreach (var name in normalizedNames)
            {
                var validation = _cardLocationLogic.ValidateForCreate(name, type);

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

                    var createdRecords = await _cardLocationRepo.InsertManyAsync(conn, tx, recordsToInsert, token);

                    var createdLocations = createdRecords.Select(MapToDomain).ToList();

                    return (Result: (IReadOnlyList<CardLocation>)createdLocations, Commit: true
                    );
                });

            if (createdLocations.Count > 0)
            {
                _cardLocationLookupStore.UpsertMany(createdLocations);
            }

            return createdLocations;
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
        public async Task<CardLocationMutationResult> UpdateLocationAsync(int id, string name, CardLocationType type)
        {
            try
            {
                var result = await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
                    {
                        var result = await UpdateCoreAsync(conn, tx, id, name, type);

                        return (
                            Result: result,
                            Commit: result.Result.Code == OperationResultCode.Success
                        );
                    });

                if (result.Location is not null)
                {
                    _cardLocationLookupStore.Upsert(result.Location);
                }

                return result;
            }
            catch (Exception ex)
            {
                return new CardLocationMutationResult(new OperationResult(OperationResultCode.Error, $"Failed to update location: {ex.Message}"), null);
            }
        }
        public async Task<DeckManagementMutation> UpdateDeckAsync(int locationId, DeckManagementInput input)
        {
            try
            {
                var result = await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
                    {
                        var locationMutation = await UpdateCoreAsync(conn, tx, locationId, input.Name, CardLocationType.Deck);

                        if (locationMutation.Result.Code != OperationResultCode.Success || locationMutation.Location is null)
                        {
                            return (Result: new DeckManagementMutation { Result = locationMutation.Result }, Commit: false);
                        }

                        await _cardLocationRepo.UpsertMetadataAsync(conn, tx, locationId, input.Format, input.Description);

                        return (Result: new DeckManagementMutation
                        {
                            Result = new OperationResult(OperationResultCode.Success, "Deck updated successfully."),
                            Deck = CreateDeckRecord(locationId, locationMutation.Location.Name, input)
                        },
                            Commit: true
                        );
                    });

                if (result.Deck is not null)
                {
                    _cardLocationLookupStore.Upsert(new CardLocation { Id = result.Deck.LocationId, Name = result.Deck.Name, Type = CardLocationType.Deck });
                }

                return result;
            }
            catch (Exception ex)
            {
                return new DeckManagementMutation
                {
                    Result = new OperationResult(OperationResultCode.Error, $"Failed to update deck: {ex.Message}")
                };
            }
        }
        private async Task<CardLocationMutationResult> UpdateCoreAsync(SQLiteConnection conn, SQLiteTransaction tx, int id, string name, CardLocationType type)
        {
            string normalizedName = _cardLocationLogic.NormalizeName(name);
            var validation = _cardLocationLogic.ValidateForUpdate(id, normalizedName, type);

            if (validation.Code != OperationResultCode.Success)
            {
                return new CardLocationMutationResult(validation, null);
            }

            try
            {
                bool alreadyExists = await _cardLocationRepo.ExistsByNameAsync(conn, tx, normalizedName, excludingId: id);

                if (alreadyExists)
                {
                    return new CardLocationMutationResult(new OperationResult(OperationResultCode.AlreadyExists, $"A location named '{normalizedName}' already exists."), null);
                }

                string dbType = MapTypeToDb(type);

                int rowsAffected = await _cardLocationRepo.UpdateAsync(conn, tx, id, normalizedName, dbType);

                if (rowsAffected == 0)
                {
                    return new CardLocationMutationResult(new OperationResult(OperationResultCode.NotFound, $"No location with id {id} was found."), null);
                }

                var updatedLocation = new CardLocation
                {
                    Id = id,
                    Name = normalizedName,
                    Type = type
                };

                return new CardLocationMutationResult(new OperationResult(OperationResultCode.Success, "Location updated successfully."), updatedLocation);
            }
            catch (SQLiteException ex) when (IsDuplicateLocationNameViolation(ex))
            {
                return new CardLocationMutationResult(new OperationResult(OperationResultCode.AlreadyExists, $"A location named '{normalizedName}' already exists."), null);
            }
        }

        // DELETE
        public async Task<CardLocationDeleteResult> DeleteLocationAsync(int id)
        {
            try
            {
                var result = await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
                    {
                        var result = await DeleteCoreAsync(conn, tx, id);

                        return (
                            Result: result,
                            Commit: result.Result.Code == OperationResultCode.Success
                        );
                    });

                if (result.Result.Code == OperationResultCode.Success)
                {
                    _cardLocationLookupStore.Remove(id);
                }

                return result;
            }
            catch (Exception ex)
            {
                return new CardLocationDeleteResult(new OperationResult(OperationResultCode.Error, $"Failed to delete location: {ex.Message}"), new CollectionChangeSet<CardSet>());
            }
        }
        public async Task<DeckManagementDeleteResult> DeleteDeckAsync(int locationId)
        {
            try
            {
                var result = await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
                    {
                        var locationMutation = await DeleteCoreAsync(conn, tx, locationId);

                        return (Result: new DeckManagementDeleteResult(
                            locationMutation.Result,
                            locationMutation.CollectionChangeSet),
                            Commit: locationMutation.Result.Code == OperationResultCode.Success
                        );
                    });

                if (result.Result.Code == OperationResultCode.Success)
                {
                    _cardLocationLookupStore.Remove(locationId);
                }

                return result;
            }
            catch (Exception ex)
            {
                return new DeckManagementDeleteResult(new OperationResult(OperationResultCode.Error, $"Failed to delete deck: {ex.Message}"), new CollectionChangeSet<CardSet>());
            }
        }
        private async Task<CardLocationDeleteResult> DeleteCoreAsync(SQLiteConnection conn, SQLiteTransaction tx, int id)
        {
            var validation = _cardLocationLogic.ValidateId(id);

            if (validation.Code != OperationResultCode.Success)
            {
                return new CardLocationDeleteResult(validation, new CollectionChangeSet<CardSet>());
            }

            bool exists = await _cardLocationRepo.ExistsByIdAsync(conn, tx, id);

            if (!exists)
            {
                return new CardLocationDeleteResult(new OperationResult(OperationResultCode.NotFound, $"No location with id {id} was found."), new CollectionChangeSet<CardSet>());
            }

            var snapshotRows = await _cardLocationRepo.GetAllCollectionRowsAsync(conn, tx);
            var affectedRows = await _cardLocationRepo.GetCollectionRowsByLocationIdAsync(conn, tx, id);

            var snapshot = CollectionSnapshot.FromRows(snapshotRows);
            var editedCards = affectedRows.Select(CreateCardWithClearedLocation).ToList();

            var plan = _mutationsLogic.PlanIdentityRewriteBatch(editedCards, snapshot);

            await _mutationsService.ExecutePlanAsync(plan, conn, tx);
            await _cardLocationRepo.DeleteDeckMetadataAsync(conn, tx, id);
            await _cardLocationRepo.DeleteAsync(conn, tx, id);

            return new CardLocationDeleteResult(new OperationResult(OperationResultCode.Success, "Location deleted successfully."), plan.ChangeSet);
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
        private static CardLocation MapToDomain(CardLocationRecord record)
        {
            return new CardLocation
            {
                Id = record.Id,
                Name = record.Name,
                Type = MapTypeFromDb(record.Type)
            };
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
        private static CardLocationType MapTypeFromDb(string dbType)
        {
            return dbType switch
            {
                "Storage" => CardLocationType.Storage,
                "Deck" => CardLocationType.Deck,
                _ => throw new InvalidOperationException($"Unsupported card location type in database: '{dbType}'.")
            };
        }
        private static bool IsDuplicateLocationNameViolation(SQLiteException ex)
        {
            if (ex.ResultCode == SQLiteErrorCode.Constraint ||
                ex.ResultCode == SQLiteErrorCode.Constraint_Unique)
            {
                return ex.Message.Contains("cardLocations", StringComparison.OrdinalIgnoreCase)
                    && ex.Message.Contains("name", StringComparison.OrdinalIgnoreCase);
            }

            return ex.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                && ex.Message.Contains("cardLocations", StringComparison.OrdinalIgnoreCase);
        }
        private static CardSet CreateCardWithClearedLocation(MyCollectionRow row)
        {
            return new CardSet
            {
                CardId = row.CardId,
                Uuid = row.Identity.Uuid,
                SelectedCondition = row.Identity.Condition,
                Language = row.Identity.Language,
                SelectedFinish = row.Identity.Finish,
                SelectedLocationId = null,
                Comment = row.Identity.Comment,
                CardsOwned = row.CardsOwned,
                CardsForTrade = row.CardsForTrade
            };
        }
    }
}
