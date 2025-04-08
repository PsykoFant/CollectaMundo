using CollectaMundo.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo.ViewModels
{
    public class CardViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // Cards lists for AllCardsDataGrid
        public List<CardSet> Cards { get; set; } = [];

        private List<CardSet> _filteredCards = [];
        public List<CardSet> FilteredCards
        {
            get => _filteredCards;
            set
            {
                if (_filteredCards != value)
                {
                    _filteredCards = value;
                    OnPropertyChanged(nameof(FilteredCards));
                }
            }
        }
        public ObservableCollection<CardSet> ColorIcons { get; } = [];
        public async Task LoadColorIconsAsync()
        {
            string query = "SELECT * FROM uniqueManaSymbols WHERE uniqueManaSymbol IN ('W', 'U', 'B', 'R', 'G', 'C', 'X') " +
                           "ORDER BY CASE uniqueManaSymbol WHEN 'W' THEN 1 WHEN 'U' THEN 2 WHEN 'B' THEN 3 WHEN 'R' THEN 4 " +
                           "WHEN 'G' THEN 5 WHEN 'C' THEN 6 WHEN 'X' THEN 7 END;";

            try
            {
                List<CardSet> tempCardList = [];
                using SQLiteCommand command = new(query, DBAccess.connection);
                using DbDataReader reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    tempCardList.Add(CreateColorIcon(reader));
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ColorIcons.Clear();
                    foreach (var item in tempCardList)
                    {
                        ColorIcons.Add(item);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while loading color icons: {ex.Message}");
            }
        }
        private static CardSet CreateColorIcon(DbDataReader reader)
        {
            return new CardSet
            {
                ManaCostImageBytes = reader["ManaSymbolImage"] as byte[],
                ManaCostRaw = reader["uniqueManaSymbol"]?.ToString() ?? string.Empty
            };
        }
    }
}
