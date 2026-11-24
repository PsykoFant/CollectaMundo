namespace CollectaMundo.ApplicationServices.Startup
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
        string TradeListExportCurrency { get; }
        void PersistPriceInfo(string? updatedDate, string? retailer);
        void PersistExchangeRate(decimal usdToEurRate, DateTimeOffset retrievedAtUtc);
        void PersistBackupFolderPath(string newBackupFolderPath);
        void PersistTradeListExportCurrency(string currency);
    }

}
