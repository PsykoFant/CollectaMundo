namespace CollectaMundo.ApplicationServices.Shared
{
    public interface IAppSettings
    {
        DatabaseSettings DatabaseSettings { get; }
        ConnectionStrings ConnectionStrings { get; }
        string CardDatabaseUrl { get; }
        string CardPricesUrl { get; }
        string UserDownloadsPath { get; }
        string BackupFolderPath { get; }
        string CardImageCachePath { get; }
        PriceInfo PriceInfo { get; }
        void PersistPriceInfo(string? updatedDate, string? retailer);
        void PersistBackupFolderPath(string newBackupFolderPath);
    }

}
