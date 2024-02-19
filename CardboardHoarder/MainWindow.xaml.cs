using System.Data;
using System.Data.SQLite;
using System.Windows;

namespace CardboardHoarder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            GridSearchAndFilter.Visibility = Visibility.Visible;
            GridMyCollection.Visibility = Visibility.Hidden;
            LoadData();
        }
        private void reset_grids()
        {
            GridSearchAndFilter.Visibility = Visibility.Hidden;
            GridMyCollection.Visibility = Visibility.Hidden;
        }
        private void MenuSearchAndFilter_Click(object sender, RoutedEventArgs e)
        {
            reset_grids();
            GridSearchAndFilter.Visibility = Visibility.Visible;
        }

        private void MenuMyCollection_Click(object sender, RoutedEventArgs e)
        {
            reset_grids();
            GridMyCollection.Visibility = Visibility.Visible;
        }
        private void LoadData()
        {
            using (SQLiteConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();
                string query = "SELECT name, SetCode FROM cards"; // Adjust the query accordingly
                SQLiteCommand command = new SQLiteCommand(query, connection);


                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    mainCardWindowDatagrid.ItemsSource = reader.Cast<IDataRecord>()
                                                              .Select(r => new CardSet
                                                              {
                                                                  Name = r["Name"]?.ToString() ?? string.Empty,
                                                                  SetCode = r["SetCode"]?.ToString() ?? string.Empty
                                                              })
                                                              .ToList();
                }
            }
        }
    }
}