namespace CollectaMundo.ApplicationServices.CardLists
{
    [Flags]
    public enum CardLookupsOptions
    {
        None = 0,
        Icons = 1,
        Sets = 2,    // reserved for later
        Prices = 4,    // reserved for later
        All = Icons   // extend later as you add sets/prices
    }
}
