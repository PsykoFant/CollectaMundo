using System.Data.SQLite;

namespace CardboardHoarder
{
    public class DatabaseHelper
    {
        private static string connectionString = "Data Source=c:/code/AllPrintings/AllPrintings.sqlite;Version=3;";

        public static SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(connectionString);
        }
    }

}
