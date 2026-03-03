namespace CollectaMundo.Views.Pages.SharedElements
{
    public sealed record ColumnResizeSpec(double[] HeaderPaddings, double[]? InitialComboWidths = null)
    {
        public static ColumnResizeSpec ForSearchAndFilter() =>
            new(HeaderPaddings: [65d, 40d], InitialComboWidths: [25d, 25d]);

        public static ColumnResizeSpec ForMyCollection() =>
            new(HeaderPaddings: [65d, 45d], InitialComboWidths: [50d, 50d]);

        public static ColumnResizeSpec ForDecks() =>
            new(HeaderPaddings: [65d], InitialComboWidths: [50d]);
    }
}
