namespace CollectaMundo.ApplicationServices.KeyedDataProvider
{
    [Flags]
    public enum KeyedDataProviderOptions
    {
        None = 0,
        Icons = 1,
        Sets = 2,
        Prices = 4,
        Locations = 8,

        All = Icons | Sets | Prices | Locations
    }
}
