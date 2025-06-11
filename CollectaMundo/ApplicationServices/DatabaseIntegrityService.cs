using CollectaMundo.Data;
using System.Diagnostics;
using System.IO;

namespace CollectaMundo.ApplicationServices
{
    public class DatabaseIntegrityService : IDatabaseIntegrityService
    {
        private readonly IAppSettings _settings;
        private readonly IDbConnectionFactory _dbFactory;
        private readonly IDatabaseHealthRepository _healthRepo;

        public DatabaseIntegrityService()
        {
            _settings = new JsonAppSettings();
            _dbFactory = new DbConnectionFactory(_settings);
            _healthRepo = new DatabaseHealthRepository();
        }

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
                await uow.BeginAsync();

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
