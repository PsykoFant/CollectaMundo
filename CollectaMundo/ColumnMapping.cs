namespace CollectaMundo
{
    public class ColumnMapping
    {
        public string? CardSetField { get; set; }
        public string? CsvHeader { get; set; }
        public List<string> CsvHeaders { get; set; } = new List<string>();
        public string? SelectedNameHeader { get; set; }
        public string? SelectedSetHeader { get; set; }
        public string? SelectedSetCodeHeader { get; set; }
        public string? SelectedCountHeader { get; set; }
        public string? SelectedConditionHeader { get; set; }
        public string? SelectedLanguageHeader { get; set; }
        public string? SelectedFinishHeader { get; set; }
    }
}
