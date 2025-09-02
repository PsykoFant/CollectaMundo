namespace CollectaMundo.ApplicationServices.CardLists.Lookups
{
    [Flags]
    public enum CardLookupsOptions
    {
        None = 0,
        Icons = 1,
        Sets = 2,
        Prices = 4,

        All = Icons | Sets | Prices
    }
}
