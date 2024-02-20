﻿using System.Data;
using System.Data.SQLite;
using System.Windows;
using System.Diagnostics;
using System.IO;


namespace CardboardHoarder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            DatabaseHelper.CheckDatabaseExistence();
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
                try
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
                catch (Exception ex)
                {
                    // Handle exceptions (e.g., log, show error message, etc.)
                    Console.WriteLine($"Error while loading data: {ex.Message}");
                }
                finally
                {
                    // Ensure the connection is closed in the finally block
                    if (connection.State == ConnectionState.Open)
                    {
                        connection.Close();
                    }
                }
            }
        }        
    }
}