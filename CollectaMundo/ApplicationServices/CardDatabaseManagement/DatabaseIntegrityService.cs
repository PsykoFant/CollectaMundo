using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.Infrastructure.CardDatabaseManagement;
using System.Diagnostics;
using System.IO;

namespace CollectaMundo.ApplicationServices.CardDatabaseManagement
{
    public class DatabaseIntegrityService(IUnitOfWorkRunner uowRunner, IAppSettings settings) : IDatabaseIntegrityService
    {
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly IAppSettings _settings = settings;
        private readonly DatabaseIntegrityRepo _healthRepo = new();

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
                bool isValid = await _uowRunner.ExecuteReadOnlyAsync(async conn => await _healthRepo.HasExpectedTablesAndViewsAsync(conn) && await _healthRepo.QuickCheckAsync(conn));

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
