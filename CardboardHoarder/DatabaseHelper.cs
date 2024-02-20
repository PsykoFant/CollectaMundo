using System.Data.SQLite;
using Microsoft.Extensions.Configuration;

public class DatabaseHelper
{
    private static IConfiguration Configuration { get; set; }

    static DatabaseHelper()
    {
        // Set up configuration
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        Configuration = builder.Build();
    }

    public static SQLiteConnection GetConnection()
    {
        // Retrieve the connection string from appsettings.json
        string connectionString = Configuration.GetConnectionString("SQLiteConnection")!;

        // Create and return SQLiteConnection
        return new SQLiteConnection(connectionString);
    }
}
