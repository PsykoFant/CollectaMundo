using System.ComponentModel;

namespace CollectaMundo.DomainLogic.Filtering.Models
{
    public class FilterOption(string optionName, bool isSelected = false) : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public string OptionName { get; } = optionName;

        private bool _isSelected = isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }
    }
}
