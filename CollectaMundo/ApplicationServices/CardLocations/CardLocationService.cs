using CollectaMundo.ApplicationServices.CardLocations.Models;
using CollectaMundo.ApplicationServices.CollectionMutations;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardLocations;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.CollectionMutations;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.CardLocations;
using CollectaMundo.Infrastructure.Shared;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardLocations
{
    public sealed class CardLocationService(IDbConnectionFactory dbFactory, ICardLocationRepo cardLocationRepo, ICardLocationLogic cardLocationLogic, ICardLocationLookupStore cardLocationLookupStore, ICollectionMutationsLogic mutationsLogic, ICollectionMutationsService mutationsService) : ICardLocationService
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly ICardLocationRepo _cardLocationRepo = cardLocationRepo;
        private readonly ICardLocationLogic _cardLocationLogic = cardLocationLogic;
        private readonly ICardLocationLookupStore _cardLocationLookupStore = cardLocationLookupStore;
        private readonly ICollectionMutationsLogic _mutationsLogic = mutationsLogic;
        private readonly ICollectionMutationsService _mutationsService = mutationsService;

        // CREATE
        public async Task<CardLocationMutationResult> CreateAsync(string name, CardLocationType type)
        {
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            var result = await CreateCoreAsync(uow.CurrentConnection, uow.CurrentTransaction, name, type);

            if (result.Result.Code != OperationResultCode.Success)
            {
                await uow.RollbackAsync();
                return result;
            }

            await uow.CommitAsync();

            if (result.Location is not null)
            {
                _cardLocationLookupStore.Upsert(result.Location);
            }

            return result;
        }
        public async Task<CardLocationMutationResult> CreateCoreAsync(SQLiteConnection conn, SQLiteTransaction tx, string name, CardLocationType type)
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

            int id = await _cardLocationRepo.InsertAsync(conn, tx, normalizedName, type.ToString());

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

            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {
                var existingLocations = await _cardLocationRepo.GetAllAsync(uow.CurrentConnection);
                var existingNames = existingLocations.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var namesToCreate = validNames.Where(name => !existingNames.Contains(name)).ToList();

                if (namesToCreate.Count == 0)
                {
                    await uow.CommitAsync();
                    return [];
                }

                string dbType = MapTypeToDb(type);

                var recordsToInsert = namesToCreate.Select(name => (Name: name, Type: dbType)).ToList();
                var createdRecords = await _cardLocationRepo.InsertManyAsync(uow.CurrentConnection, uow.CurrentTransaction, recordsToInsert, token);

                await uow.CommitAsync();

                var createdLocations = createdRecords.Select(MapToDomain).ToList();
                _cardLocationLookupStore.UpsertMany(createdLocations);

                return createdLocations;
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }
        }

        // READ
        public async Task<IReadOnlyList<CardLocation>> GetAllAsync()
        {
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginReadOnlyAsync();

            var records = await _cardLocationRepo.GetAllAsync(uow.CurrentConnection);

            return [.. records.Select(MapToDomain)];
        }

        // UPDATE
        public async Task<CardLocationMutationResult> UpdateAsync(int id, string name, CardLocationType type)
        {
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {
                var result = await UpdateCoreAsync(uow.CurrentConnection, uow.CurrentTransaction, id, name, type);

                if (result.Result.Code != OperationResultCode.Success)
                {
                    await uow.RollbackAsync();
                    return result;
                }

                await uow.CommitAsync();

                if (result.Location is not null)
                {
                    _cardLocationLookupStore.Upsert(result.Location);
                }

                return result;
            }
            catch (Exception ex)
            {
                await uow.RollbackAsync();
                return new CardLocationMutationResult(new OperationResult(OperationResultCode.Error, $"Failed to update location: {ex.Message}"), null);
            }
        }
        public async Task<CardLocationMutationResult> UpdateCoreAsync(SQLiteConnection conn, SQLiteTransaction tx, int id, string name, CardLocationType type)
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
        public async Task<CardLocationDeleteResult> DeleteAsync(int id)
        {
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {
                var result = await DeleteCoreAsync(uow.CurrentConnection, uow.CurrentTransaction, id);

                if (result.Result.Code != OperationResultCode.Success)
                {
                    await uow.RollbackAsync();
                    return result;
                }

                await uow.CommitAsync();

                _cardLocationLookupStore.Remove(id);

                return result;
            }
            catch (Exception ex)
            {
                await uow.RollbackAsync();
                return new CardLocationDeleteResult(new OperationResult(OperationResultCode.Error, $"Failed to delete location: {ex.Message}"), new CollectionChangeSet<CardSet>());
            }
        }
        public async Task<CardLocationDeleteResult> DeleteCoreAsync(SQLiteConnection conn, SQLiteTransaction tx, int id)
        {
            var validation = _cardLocationLogic.ValidateId(id);

            if (validation.Code != OperationResultCode.Success)
            {
                return new CardLocationDeleteResult(
                    validation,
                    new CollectionChangeSet<CardSet>());
            }

            var snapshotRows = await _cardLocationRepo.GetAllCollectionRowsAsync(conn, tx);
            var affectedRows = await _cardLocationRepo.GetCollectionRowsByLocationIdAsync(conn, tx, id);

            var snapshot = CollectionSnapshot.FromRows(snapshotRows);

            var editedCards = affectedRows
                .Select(CreateCardWithClearedLocation)
                .ToList();

            var plan = _mutationsLogic.PlanIdentityRewriteBatch(editedCards, snapshot);

            await _mutationsService.ExecutePlanAsync(plan, conn, tx);

            int rowsAffected = await _cardLocationRepo.DeleteAsync(conn, tx, id);

            if (rowsAffected == 0)
            {
                return new CardLocationDeleteResult(new OperationResult(OperationResultCode.NotFound, $"No location with id {id} was found."), new CollectionChangeSet<CardSet>());
            }

            return new CardLocationDeleteResult(new OperationResult(OperationResultCode.Success, "Location deleted successfully."), plan.ChangeSet);
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
