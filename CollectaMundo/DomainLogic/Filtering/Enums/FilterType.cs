namespace CollectaMundo.DomainLogic.Filtering.Enums
{
    // Defines the type of filtering applicable to a criteria.
    public enum FilterType
    {
        Single,  // A single-selection filter (e.g., Name, SetName)
        Multi,   // A multi-selection filter (e.g., Keywords, Colors, Types)
        Numeric  // A numeric-based filter (e.g., ManaValue, CardsForTrade)
    }


}
