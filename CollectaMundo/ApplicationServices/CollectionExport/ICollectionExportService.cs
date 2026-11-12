using CollectaMundo.ApplicationServices.Shared.Operation;

namespace CollectaMundo.ApplicationServices.CollectionExport
{
    public interface ICollectionExportService
    {
        Task<OperationResult> ExportFullCollectionBackupAsync(CancellationToken cancellationToken = default);
        Task<OperationResult> ExportTradeListAsync(string targetCurrency, CancellationToken cancellationToken = default);
        string BackupFolderPath { get; }
        OperationResult ChangeBackupFolderPath(string newBackupFolderPath);
    }
}
