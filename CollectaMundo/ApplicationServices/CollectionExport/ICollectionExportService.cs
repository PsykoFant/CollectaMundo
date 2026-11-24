using CollectaMundo.ApplicationServices.Shared.Operation;

namespace CollectaMundo.ApplicationServices.CollectionExport
{
    public interface ICollectionExportService
    {
        string TradeListExportCurrency { get; }
        void PersistTradeListExportCurrency(string currency);
        Task<OperationResult> ExportFullCollectionBackupAsync(CancellationToken cancellationToken = default);
        Task<OperationResult> ExportTradeListAsync(string targetCurrency, CancellationToken cancellationToken = default);
        string BackupFolderPath { get; }
        OperationResult ChangeBackupFolderPath();
        void OpenBackupFolder();
    }
}
