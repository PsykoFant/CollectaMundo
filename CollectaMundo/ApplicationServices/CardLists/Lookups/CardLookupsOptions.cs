namespace CollectaMundo.ApplicationServices.CardLists.Lookups
{
    [Flags]
    public enum CardLookupsOptions
    {
        None = 0,
        Icons = 1,
        Sets = 2,
        Prices = 4,    // reserved for later
        All = Icons   // extend later as you add sets/prices
    }
}
