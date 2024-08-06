using System.ComponentModel;

namespace CollectaMundo
{
    public class ColumnMapping : INotifyPropertyChanged
    {
        public string? CardSetField { get; set; }

        private string? csvHeader;
        public string? CsvHeader
        {
            get => csvHeader;
            set
            {
                if (csvHeader != value)
                {
                    csvHeader = value;
                    OnPropertyChanged(nameof(CsvHeader));
                }
            }
        }
        public List<string>? CsvHeaders { get; set; } = new List<string>();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string? SelectedNameHeader { get; set; }
        public string? SelectedSetHeader { get; set; }
        public string? SelectedSetCodeHeader { get; set; }
        public string? SelectedCountHeader { get; set; }
        public string? SelectedConditionHeader { get; set; }
        public string? SelectedLanguageHeader { get; set; }
        public string? SelectedFinishHeader { get; set; }
    }
}
