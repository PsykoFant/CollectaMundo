using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;

namespace CardboardHoarder
{
   public class DatabaseHelper
    {
        private static string connectionString = "Data Source=c:/code/Cardboardhoarder/AllPrintings/AllPrintings.sqlite;Version=3;";

        public static SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(connectionString);
        }
    }

}
