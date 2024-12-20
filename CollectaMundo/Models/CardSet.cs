﻿using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CollectaMundo.Models
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
        public DateTime? ReleaseDate { get; set; }
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
        public decimal? NormalPrice { get; set; }
        public decimal? FoilPrice { get; set; }
        public decimal? EtchedPrice { get; set; }

        private ImageSource? _setIcon;
        public ImageSource? SetIcon
        {
            get
            {
                if (_setIcon == null && SetIconBytes != null)
                {
                    _setIcon = ConvertImage(SetIconBytes);
                }
                return _setIcon;
            }
            set => _setIcon = value;
        }
        public byte[]? SetIconBytes { get; set; }
        public string? ManaCostRaw { get; set; }

        private ImageSource? _manaCostImage;
        public ImageSource? ManaCostImage
        {
            get
            {
                if (_manaCostImage == null && ManaCostImageBytes != null)
                {
                    _manaCostImage = ConvertImage(ManaCostImageBytes);
                }
                return _manaCostImage;
            }
            set => _manaCostImage = value;
        }
        public byte[]? ManaCostImageBytes { get; set; }
        private static BitmapImage? ConvertImage(byte[] imageData)
        {
            try
            {
                using (MemoryStream ms = new(imageData))
                {
                    BitmapImage image = new();
                    ms.Position = 0;
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to convert image: {ex.Message}");
                return null;
            }
        }
        public class CardItem : CardSet, INotifyPropertyChanged
        {
            public int? CardId { get; set; }
            public event PropertyChangedEventHandler? PropertyChanged;
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            private int _cardsOwned;
            public int CardsOwned
            {
                get => _cardsOwned;
                set
                {
                    if (_cardsOwned != value)
                    {
                        _cardsOwned = value;
                        OnPropertyChanged(nameof(CardsOwned));
                    }
                }
            }

            private int _cardsForTrade;
            public int CardsForTrade
            {
                get => _cardsForTrade;
                set
                {
                    if (_cardsForTrade != value)
                    {
                        _cardsForTrade = value;
                        OnPropertyChanged(nameof(CardsForTrade));
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
            public decimal? CardItemPrice { get; set; }
        }
    }
}
