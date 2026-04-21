using CollectaMundo.ApplicationServices.CardLocations.Models;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLocations;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.Infrastructure.CardLocations;
using CollectaMundo.Infrastructure.Shared;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardLocations
{
    public sealed class CardLocationService(IDbConnectionFactory dbFactory, ICardLocationRepo cardLocationRepo, ICardLocationLogic cardLocationLogic, ICardLocationLookupStore cardLocationLookupStore) : ICardLocationService
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly ICardLocationRepo _cardLocationRepo = cardLocationRepo;
        private readonly ICardLocationLogic _cardLocationLogic = cardLocationLogic;
        private readonly ICardLocationLookupStore _cardLocationLookupStore = cardLocationLookupStore;
        public async Task<IReadOnlyList<CardLocation>> GetAllAsync()
        {
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginReadOnlyAsync();

            var records = await _cardLocationRepo.GetAllAsync(uow.CurrentConnection);

            return [.. records.Select(MapToDomain)];
        }
        public async Task<CardLocationMutationResult> CreateAsync(string name, CardLocationType type)
        {
            string normalizedName = _cardLocationLogic.NormalizeName(name);
            var validation = _cardLocationLogic.ValidateForCreate(normalizedName, type);

            if (validation.Code != OperationResultCode.Success)
            {
                return new CardLocationMutationResult(validation, null);
            }

            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {
                bool alreadyExists = await _cardLocationRepo.ExistsByNameAsync(
                    uow.CurrentConnection,
                    normalizedName);

                if (alreadyExists)
                {
                    await uow.RollbackAsync();
                    return new CardLocationMutationResult(
                        new OperationResult(
                            OperationResultCode.AlreadyExists,
                            $"A location named '{normalizedName}' already exists."),
                        null);
                }

                string dbType = MapTypeToDb(type);

                int newId = await _cardLocationRepo.InsertAsync(
                    uow.CurrentConnection,
                    normalizedName,
                    dbType);

                await uow.CommitAsync();

                var createdLocation = new CardLocation
                {
                    Id = newId,
                    Name = normalizedName,
                    Type = type
                };

                _cardLocationLookupStore.Upsert(createdLocation);

                return new CardLocationMutationResult(
                    new OperationResult(
                        OperationResultCode.Success,
                        "Location created successfully."),
                    createdLocation);
            }
            catch (SQLiteException ex) when (IsDuplicateLocationNameViolation(ex))
            {
                await uow.RollbackAsync();
                return new CardLocationMutationResult(
                    new OperationResult(
                        OperationResultCode.AlreadyExists,
                        $"A location named '{normalizedName}' already exists."),
                    null);
            }
            catch (Exception ex)
            {
                await uow.RollbackAsync();
                return new CardLocationMutationResult(
                    new OperationResult(
                        OperationResultCode.Error,
                        $"Failed to create location: {ex.Message}"),
                    null);
            }
        }
        public async Task<CardLocationMutationResult> UpdateAsync(int id, string name, CardLocationType type)
        {
            string normalizedName = _cardLocationLogic.NormalizeName(name);
            var validation = _cardLocationLogic.ValidateForUpdate(id, normalizedName, type);

            if (validation.Code != OperationResultCode.Success)
            {
                return new CardLocationMutationResult(validation, null);
            }

            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {
                bool alreadyExists = await _cardLocationRepo.ExistsByNameAsync(
                    uow.CurrentConnection,
                    normalizedName,
                    excludingId: id);

                if (alreadyExists)
                {
                    await uow.RollbackAsync();
                    return new CardLocationMutationResult(
                        new OperationResult(
                            OperationResultCode.AlreadyExists,
                            $"A location named '{normalizedName}' already exists."),
                        null);
                }

                string dbType = MapTypeToDb(type);

                int rowsAffected = await _cardLocationRepo.UpdateAsync(
                    uow.CurrentConnection,
                    id,
                    normalizedName,
                    dbType);

                if (rowsAffected == 0)
                {
                    await uow.RollbackAsync();
                    return new CardLocationMutationResult(
                        new OperationResult(
                            OperationResultCode.NotFound,
                            $"No location with id {id} was found."),
                        null);
                }

                await uow.CommitAsync();

                var updatedLocation = new CardLocation
                {
                    Id = id,
                    Name = normalizedName,
                    Type = type
                };

                _cardLocationLookupStore.Upsert(updatedLocation);

                return new CardLocationMutationResult(
                    new OperationResult(
                        OperationResultCode.Success,
                        "Location updated successfully."),
                    updatedLocation);
            }
            catch (SQLiteException ex) when (IsDuplicateLocationNameViolation(ex))
            {
                await uow.RollbackAsync();
                return new CardLocationMutationResult(
                    new OperationResult(
                        OperationResultCode.AlreadyExists,
                        $"A location named '{normalizedName}' already exists."),
                    null);
            }
            catch (Exception ex)
            {
                await uow.RollbackAsync();
                return new CardLocationMutationResult(
                    new OperationResult(
                        OperationResultCode.Error,
                        $"Failed to update location: {ex.Message}"),
                    null);
            }
        }
        public async Task<OperationResult> DeleteAsync(int id)
        {
            var validation = _cardLocationLogic.ValidateId(id);

            if (validation.Code != OperationResultCode.Success)
            {
                return validation;
            }

            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {
                // Delete cardlocations from mycollectioncards before deleting the location itself to avoid foreign key constraint violations
                await _cardLocationRepo.ClearLocationFromCollectionAsync(uow.CurrentConnection,id);

                int rowsAffected = await _cardLocationRepo.DeleteAsync(uow.CurrentConnection,id);

                if (rowsAffected == 0)
                {
                    await uow.RollbackAsync();
                    return new OperationResult(
                        OperationResultCode.NotFound,
                        $"No location with id {id} was found.");
                }

                await uow.CommitAsync();

                _cardLocationLookupStore.Remove(id);

                return new OperationResult(OperationResultCode.Success,"Location deleted successfully.");
            }
            catch (Exception ex)
            {
                await uow.RollbackAsync();
                return new OperationResult(
                    OperationResultCode.Error,
                    $"Failed to delete location: {ex.Message}");
            }
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
    }
}
