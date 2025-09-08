namespace CollectaMundo.ApplicationServices
{
    public interface IAppSettings
    {
        DatabaseSettings DatabaseSettings { get; }
        ConnectionStrings ConnectionStrings { get; }
        string CardDatabaseUrl { get; }
        string CardPricesUrl { get; }
        string UserDownloadsPath { get; }
        string BackupFolderPath { get; }
        PriceInfo PriceInfo { get; }
        void UpdatePriceInfo(string? updatedDate, string? retailer);
    }

}
