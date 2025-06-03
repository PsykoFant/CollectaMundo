using CollectaMundo.Data;
using System.Diagnostics;
using System.IO;

namespace CollectaMundo.ApplicationServices
{
    public class StartupService(IAppSettings settings, IUnitOfWork uow, IDatabaseHealthRepository healthRepo) : IStartupService
    {
        private readonly IAppSettings _settings = settings;
        private readonly IUnitOfWork _uow = uow;
        private readonly IDatabaseHealthRepository _healthRepo = healthRepo;

        public async Task EnsureDatabaseIntegrityAsync()
        {
            bool redownloadDb = false;

            string dbPath = Path.Combine(_settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");

            if (!File.Exists(dbPath))
            {
                redownloadDb = true;
                Debug.WriteLine("Card DB does not exist. Will trigger download.");
            }
            else
            {
                await _uow.BeginAsync();

                try
                {
                    var conn = _uow.CurrentConnection;

                    bool hasAllTables = await _healthRepo.HasExpectedTablesAndViewsAsync(conn);
                    bool isOk = await _healthRepo.QuickCheckAsync(conn);

                    if (!hasAllTables || !isOk)
                    {
                        Debug.WriteLine("DB corrupt or incomplete. Will delete.");
                        File.Delete(dbPath);
                        redownloadDb = true;
                    }

                    await _uow.CommitAsync();
                }
                catch
                {
                    await _uow.RollbackAsync();
                    throw;
                }
                finally
                {
                    await _uow.DisposeAsync();
                }
            }

            if (redownloadDb)
            {
                // Placeholders for future injected services
                Debug.WriteLine("Downloading new card DB...");
                await Task.Delay(500); // Simulate download
                Debug.WriteLine("Download complete.");
            }
        }

        private Task<bool> CheckDatabaseIntegrityAsync(string dbPath)
        {
            // Simulate async DB check
            return Task.FromResult(true); // Always valid for now
        }
    }

}
