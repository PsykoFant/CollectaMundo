using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.Infrastructure.CardDatabaseManagement;
using CollectaMundo.Infrastructure.Common;
using System.Diagnostics;
using System.IO;

namespace CollectaMundo.ApplicationServices.CardDatabaseManagement
{
    public class DatabaseIntegrityService(IDbConnectionFactory dbFactory, IAppSettings settings) : IDatabaseIntegrityService
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly IAppSettings _settings = settings;
        private readonly IDatabaseIntegrityRepo _healthRepo = new DatabaseIntegrityRepo();

        public async Task<DatabaseStatus> GetDatabaseStatusAsync()
        {
            string dbPath = Path.Combine(_settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");

            if (!File.Exists(dbPath))
            {
                Debug.WriteLine("DB doesn't exist");
                return DatabaseStatus.Missing;
            }

            try
            {
                await using var uow = new UnitOfWork(_dbFactory);
                await uow.BeginReadOnlyAsync();

                bool isValid = await _healthRepo.HasExpectedTablesAndViewsAsync(uow.CurrentConnection) && await _healthRepo.QuickCheckAsync(uow.CurrentConnection);

                await uow.CommitAsync();

                Debug.WriteLine($"Is the database ok: {isValid}");

                return isValid ? DatabaseStatus.Healthy : DatabaseStatus.Corrupt;
            }
            catch
            {
                Debug.WriteLine("DB is corrupted (this is from inside catch)");
                return DatabaseStatus.Corrupt;
            }
        }
    }

}
