namespace CollectaMundo.DomainLogic.Filtering
{
    public enum OperatorType
    {
        // Basic logical operators
        OR = 0,
        AND = 1,
        NOT = 2,

        // Comparison operators
        EQUALS = 3,
        NOT_EQUALS = 4,
        GREATER_THAN = 5,
        LESS_THAN = 6,
        GREATER_THAN_OR_EQUALS = 7,
        LESS_THAN_OR_EQUALS = 8,

        // Range operators
        IN_RANGE = 9,
        NOT_IN_RANGE = 10,

        // String-specific operators
        CONTAINS = 11,
        DOES_NOT_CONTAIN = 12,
        STARTS_WITH = 13,
        ENDS_WITH = 14,

        // Special operators
        IS_NULL = 15,
        IS_NOT_NULL = 16,

        // Unknown or default
        Unknown = -1
    }
}
