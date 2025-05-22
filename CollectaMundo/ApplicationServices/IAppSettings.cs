namespace CollectaMundo.ApplicationServices
{
    public interface IAppSettings
    {
        DatabaseSettings DatabaseSettings { get; }
        ConnectionStrings ConnectionStrings { get; }
        PriceInfo PriceInfo { get; }
    }

}
