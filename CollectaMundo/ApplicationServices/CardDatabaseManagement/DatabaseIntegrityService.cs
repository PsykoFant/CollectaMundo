using CollectaMundo.ApplicationServices.CardDatabaseManagement.Models;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.Infrastructure.CardDatabaseManagement;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;

namespace CollectaMundo.ApplicationServices.CardDatabaseManagement
{
    public class DatabaseIntegrityService(IUnitOfWorkRunner uowRunner, IAppSettings settings, IDatabaseIntegrityRepo integrityRepo) : IDatabaseIntegrityService
    {
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly IAppSettings _settings = settings;
        private readonly IDatabaseIntegrityRepo _integrityRepo = integrityRepo;
        public async Task<DatabaseStatus> GetDatabaseStatusAsync()
        {
            string dbPath = Path.Combine(_settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");

            if (!File.Exists(dbPath))
            {
                Debug.WriteLine("[DatabaseStatus] Database missing.");
                return DatabaseStatus.Missing;
            }

            try
            {
                bool isReady = await _uowRunner.ExecuteReadOnlyAsync(async conn =>
                    {
                        if (await _integrityRepo.HasIncompletePreparationAsync(conn))
                        {
                            throw new InvalidDataException("The card database exists, but its first-time preparation did not complete successfully.");
                        }

                        return await _integrityRepo.HasExpectedTablesAndViewsAsync(conn) && await _integrityRepo.HasOracleCardsAsync(conn);
                    });


                if (!isReady)
                {
                    throw new InvalidDataException("The existing database does not contain the required application schema or Oracle card data.");
                }

                Debug.WriteLine("[DatabaseStatus] Database ready.");

                return DatabaseStatus.Healthy;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseStatus] Readiness check failed: {ex}");

                throw;
            }
        }
        public Task VerifyMainDatabaseIntegrityAsync(SQLiteConnection conn, SQLiteTransaction? tx)
        {
            return _integrityRepo.VerifyMainDatabaseIntegrityAsync(conn, tx);
        }
        public async Task MarkFirstTimePreparationStartedAsync()
        {
            await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
            {
                await _integrityRepo.MarkPreparationStartedAsync(conn, tx);

                return (Result: true, Commit: true);
            });
        }
        public async Task MarkFirstTimePreparationCompletedAsync()
        {
            await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
            {
                await _integrityRepo.MarkPreparationCompletedAsync(conn, tx);

                return (Result: true, Commit: true);
            });
        }

    }

}
