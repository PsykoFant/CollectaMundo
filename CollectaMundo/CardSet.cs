using System.ComponentModel;
using System.Windows.Media;

namespace CollectaMundo
{
    public class CardSet
    {
        public string? Artist { get; set; }
        public List<string>? ArtistIds { get; set; }
        public string? BorderColor { get; set; }
        public List<string>? CardParts { get; set; }
        public List<string>? ColorIdentity { get; set; }
        public List<string>? ColorIndicator { get; set; }
        public string? Colors { get; set; }
        public double? ConvertedManaCost { get; set; }
        public string? Defense { get; set; }
        public double? FaceConvertedManaCost { get; set; }
        public double? FaceManaValue { get; set; }
        public string? FaceName { get; set; }
        public string? Finishes { get; set; }
        public string? FlavorName { get; set; }
        public string? FlavorText { get; set; }
        public bool? HasNonFoil { get; set; }
        public bool? IsAlternative { get; set; }
        public bool? IsFullArt { get; set; }
        public bool? IsFunny { get; set; }
        public bool? IsOnlineOnly { get; set; }
        public bool? IsOversized { get; set; }
        public bool? IsPromo { get; set; }
        public bool? IsRebalanced { get; set; }
        public bool? IsReprint { get; set; }
        public bool? IsReserved { get; set; }
        public bool? IsStarter { get; set; }
        public bool? IsStorySpotlight { get; set; }
        public bool? IsTextless { get; set; }
        public string? Keywords { get; set; }
        public string? Language { get; set; }
        public List<string>? OtherLanguages { get; set; }
        public string? Layout { get; set; }
        public string? Life { get; set; }
        public string? Loyalty { get; set; }
        public string? ManaCost { get; set; }
        public double? ManaValue { get; set; }
        public string? Name { get; set; }
        public string? Number { get; set; }
        public List<string>? OtherFaceIds { get; set; }
        public string? Power { get; set; }
        public List<string>? PromoTypes { get; set; }
        public string? Rarity { get; set; }
        public List<string>? RebalancedPrintings { get; set; }
        public string? SetCode { get; set; }
        public string? SetName { get; set; }
        public string? Side { get; set; }
        public List<string>? Subsets { get; set; }
        public string? SubTypes { get; set; }
        public string? SuperTypes { get; set; }
        public string? Text { get; set; }
        public string? Toughness { get; set; }
        public string? Type { get; set; }
        public string? Types { get; set; }
        public string? Uuid { get; set; }
        public ImageSource? SetIcon { get; set; }
        public ImageSource? ManaCostImage { get; set; }
        public class CardItem : CardSet, INotifyPropertyChanged
        {
            public int? CardId { get; set; }
            public event PropertyChangedEventHandler? PropertyChanged;
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            private int _count;
            public int Count
            {
                get => _count;
                set
                {
                    if (_count != value)
                    {
                        _count = value;
                        OnPropertyChanged(nameof(Count));
                    }
                }
            }

            private int _countTrade;
            public int CountTrade
            {
                get => _countTrade;
                set
                {
                    if (_countTrade != value)
                    {
                        _countTrade = value;
                        OnPropertyChanged(nameof(CountTrade));
                    }
                }
            }

            private string? _selectedCondition;
            public List<string> Conditions { get; } = new List<string>
                {
                    "Mint",
                    "Near Mint",
                    "Excellent",
                    "Good",
                    "Light Played",
                    "Played",
                    "Poor"
                };
            public string? SelectedCondition
            {
                get => _selectedCondition;
                set
                {
                    if (_selectedCondition != value)
                    {
                        _selectedCondition = value;
                        OnPropertyChanged(nameof(SelectedCondition));
                    }
                }
            }
            public List<string> AvailableFinishes { get; set; } = new List<string>();
            public string? SelectedFinish { get; set; }
        }
    }
}
