using CollectaMundo.Data.ImportExport;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo.ApplicationServices.ImportExport
{
    public class ImportExportService(IImportExportRepo importExportRepo) : IImportExportService
    {
        private readonly IImportExportRepo _importExportRepo = importExportRepo;
        public async Task ExportCollectionAsync()
        {
            try
            {
                var uow = new UnitOfWork();
                await uow.BeginAsync();

                var filePath = await _importExportRepo.ExportCollectionAsync(uow.CurrentConnection);

                if (filePath == null)
                {
                    MessageBox.Show("Your collection is empty - nothing to back up", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Backup created successfully at {filePath}", "A backup of your collection has been created!", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                await uow.CommitAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating CSV backup: {ex.Message}");
                MessageBox.Show($"Error creating CSV backup: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
