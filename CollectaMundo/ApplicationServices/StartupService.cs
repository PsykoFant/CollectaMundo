using CollectaMundo.Data;
using CollectaMundo.ViewModels;
using System.Diagnostics;
using System.IO;

namespace CollectaMundo.ApplicationServices
{
    public class StartupService(IAppSettings settings, IDbConnectionFactory dbFactory, IDatabaseHealthRepository healthRepo, ICardDatabasePreparationService downloader) : IStartupService
    {
        private readonly IAppSettings _settings = settings;
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly IDatabaseHealthRepository _healthRepo = healthRepo;
        private readonly ICardDatabasePreparationService _resourceDownloader = downloader;

        public async Task EnsureDatabaseIntegrityAsync(StatusViewModel statusVm)
        {
            string dbPath = Path.Combine(_settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");
            bool redownloadDb = false;

            if (!File.Exists(dbPath))
            {
                redownloadDb = true;
                Debug.WriteLine("Card DB does not exist. Will trigger download.");
            }
            else
            {
                Debug.WriteLine("Card DB exists!");

                bool dbIsCorrupt = false;
                {


                    try
                    {
                        await using var uow = new UnitOfWork(_dbFactory);
                        await uow.BeginAsync();

                        bool isValid = await _healthRepo.HasExpectedTablesAndViewsAsync(uow.CurrentConnection) && await _healthRepo.QuickCheckAsync(uow.CurrentConnection);

                        if (!isValid)
                        {
                            dbIsCorrupt = true;
                            Debug.WriteLine("DB health check failed. Marking for deletion.");
                        }
                        else
                        {
                            Debug.WriteLine("DB health check passed.");
                        }

                        await uow.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"DB integrity exception — assuming corruption: {ex.Message}");
                        dbIsCorrupt = true; // force fallback
                    }

                }

                if (dbIsCorrupt)
                {
                    try
                    {
                        File.Delete(dbPath); // Now safe to delete                        
                        Debug.WriteLine("Deleted corrupted DB file.");
                    }
                    catch (Exception fileEx)
                    {
                        Debug.WriteLine("Failed to delete corrupted DB: " + fileEx.Message);
                    }
                    redownloadDb = true;
                }
            }

            if (redownloadDb)
            {
                var url = "https://mtgjson.com/api/v5/AllPrintings.sqlite";
                var path = Path.Combine(_settings.DatabaseSettings.SQLitePath, "AllPrintings.sqlite");
                bool result = await _resourceDownloader.DownloadResourceAsync(url, path, "Card Database", true, statusVm);

                if (result)
                {
                    await ProcessCardDatabaseAsync();
                }
            }
        }
        private Task ProcessCardDatabaseAsync()
        {
            Debug.WriteLine("Mock processing downloaded database...");
            return Task.CompletedTask;
        }

    }

}
