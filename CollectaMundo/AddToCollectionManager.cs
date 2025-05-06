using CollectaMundo.DomainLogic.Models;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace CollectaMundo
{
    public class AddToCollectionManager
    {
        private static AddToCollectionManager? _instance;
        public static AddToCollectionManager Instance => _instance ??= new AddToCollectionManager();
        public ObservableCollection<CardSet> CardItemsToAdd { get; private set; }
        public ObservableCollection<CardSet> CardItemsToEdit { get; private set; }

        // Timer for delayed processing
        private readonly System.Timers.Timer _typingTimer;
        private const int TypingDelay = 500; // 500 milliseconds delay
        private TextBox? _lastTextBox;
        private ObservableCollection<CardSet>? _lastTargetCollection;

        public AddToCollectionManager()
        {
            CardItemsToAdd = [];
            CardItemsToEdit = [];
        }

        private void TypingTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs? e)
        {
            if (sender == null || e == null)
            {
                return; // Safeguard against potential nulls, though they shouldn't be null
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                CardsOwnedTextChangedLogic(_lastTextBox, _lastTargetCollection);
            });
        }
        private static void CardsOwnedTextChangedLogic(TextBox? textBox, ObservableCollection<CardSet>? targetCollection)
        {
            if (textBox?.DataContext is CardSet cardItem)
            {
                // Try parsing the new value
                if (int.TryParse(textBox.Text, out int newCount) && newCount >= 0)
                {
                    // Update CardsOwned with the parsed value
                    cardItem.CardsOwned = newCount;

                    // Adjust CardsForTrade if necessary
                    if (cardItem.CardsOwned < cardItem.CardsForTrade)
                    {
                        cardItem.CardsForTrade = cardItem.CardsOwned;
                    }

                    // If CardsOwned drops to zero or below, remove the item
                    if (cardItem.CardsOwned <= 0 && targetCollection != null)
                    {
                        targetCollection.Remove(cardItem);
                    }
                }
                else
                {
                    // If not valid, reset to the previous valid value
                    textBox.Text = cardItem.CardsOwned.ToString();
                }
            }
        }
        public static void HideCardsToEditListView(bool showLogo)
        {
            //MainWindow.CurrentInstance.LogoSmall.Visibility = showLogo ? Visibility.Visible : Visibility.Collapsed;
            //MainWindow.CurrentInstance.CardsToEditListView.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.ButtonSubmitCardEditsInMyCollection.Visibility = Visibility.Collapsed;
            //MainWindow.CurrentInstance.ButtonClearCardsToEdit.Visibility = Visibility.Collapsed;
        }

        // Right-click specific operations
        public async void DeleteCardsFromCollection(List<CardSet> selectedCards)
        {
            if (DBAccess.connection == null)
            {
                MessageBox.Show("Database connection is not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await DBAccess.connection.OpenAsync();
            try
            {
                foreach (CardSet card in selectedCards)
                {
                    // Delete card from database (myCollection)
                    string deleteSql = "DELETE FROM myCollection WHERE uuid = @uuid;";
                    using var deleteCommand = new SQLiteCommand(deleteSql, DBAccess.connection);
                    deleteCommand.Parameters.AddWithValue("@uuid", card.Uuid);
                    await deleteCommand.ExecuteNonQueryAsync();

                    // Check if CardItemsToEdit contains the card and remove it if found
                    var cardToEdit = CardItemsToEdit.FirstOrDefault(editCard => editCard.Uuid == card.Uuid);
                    if (cardToEdit != null)
                    {
                        CardItemsToEdit.Remove(cardToEdit);

                        if (CardItemsToEdit.Count == 0)
                        {
                            HideCardsToEditListView(true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete cards: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Failed to delete cards: {ex.Message}");
            }
            finally
            {
                // Provide update of the operation
                var cardDetails = selectedCards.Select(card =>
                    $"- {card.Name}").Aggregate((current, next) => current + "\n" + next);

                MainWindow.CurrentInstance.EditStatusScrollViewer.Visibility = Visibility.Visible;
                MainWindow.CurrentInstance.EditStatusTextBlock.Text = "Deleted the following cards from your collection:\n\n" + cardDetails;

                // Reload the collection
                MainWindow.CurrentInstance.MyCollectionDataGrid.ItemsSource = null;
                //await CardViewModel.CreateCardListObjectAsync(MainWindow.CurrentInstance.AllCardsVM.MyCollection, MainWindow.CurrentInstance.AllCardsVM.MyCollectionView, MainWindow.CurrentInstance.myCollectionQuery, CardListObject.MyCollection);
                //await MainWindow.CurrentInstance.PopulateFilterUiElements();

                DBAccess.connection.Close();
            }
        }
        public async void SetCardsForTrade(List<CardSet> selectedCards, bool setForTrade)
        {
            if (DBAccess.connection == null)
            {
                MessageBox.Show("Database connection is not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await DBAccess.connection.OpenAsync();
            try
            {
                string sqlString = string.Empty;

                if (setForTrade)
                {
                    sqlString = "UPDATE myCollection SET trade = count WHERE uuid = @uuid;";
                }
                else
                {
                    sqlString = "UPDATE myCollection SET trade = 0 WHERE uuid = @uuid;";
                }


                foreach (CardSet card in selectedCards)
                {

                    using var setForTradeCommand = new SQLiteCommand(sqlString, DBAccess.connection);
                    setForTradeCommand.Parameters.AddWithValue("@uuid", card.Uuid);
                    await setForTradeCommand.ExecuteNonQueryAsync();

                    // Check if CardItemsToEdit contains the card and remove it if found
                    var cardToEdit = CardItemsToEdit.FirstOrDefault(editCard => editCard.Uuid == card.Uuid);
                    if (cardToEdit != null)
                    {
                        CardItemsToEdit.Remove(cardToEdit);

                        if (CardItemsToEdit.Count == 0)
                        {
                            HideCardsToEditListView(true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update trade count: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Failed to update trade count: {ex.Message}");
            }
            finally
            {
                // Provide update of the operation
                var cardDetails = selectedCards.Select(card =>
                    $"- {card.Name}").Aggregate((current, next) => current + "\n" + next);

                MainWindow.CurrentInstance.EditStatusScrollViewer.Visibility = Visibility.Visible;
                MainWindow.CurrentInstance.EditStatusTextBlock.Text = "Updated trade status for the collowing cards:\n\n" + cardDetails;

                // Reload the collection
                MainWindow.CurrentInstance.MyCollectionDataGrid.ItemsSource = null;
                //await CardViewModel.CreateCardListObjectAsync(MainWindow.CurrentInstance.AllCardsVM.MyCollection, MainWindow.CurrentInstance.AllCardsVM.MyCollectionView, MainWindow.CurrentInstance.myCollectionQuery, CardListObject.MyCollection);

                DBAccess.connection.Close();

                //MainWindow.CurrentInstance.ApplyFiltersToAllLists();
            }
        }


        // Adjust listviews column widths so text is not clipped
        public static void AdjustColumnWidths()
        {
            //AdjustListViewColumnWidths(MainWindow.CurrentInstance.CardsToAddListView);
            //AdjustListViewColumnWidths(MainWindow.CurrentInstance.CardsToEditListView);
        }


    }
}
