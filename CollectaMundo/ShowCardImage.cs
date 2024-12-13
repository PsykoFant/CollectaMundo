using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo
{
    class ShowCardImage
    {
        // Method to show the card image and handle promo types
        public static async Task ShowImage(string? uuid, string cardName = "")
        {
            try
            {
                await DBAccess.OpenConnectionAsync();

                // If method is being called without a specific uuid, fetch uuid for the oldest version of the card
                uuid ??= GetOldestCardUuid(cardName);

                // Ensure UUID is not null before proceeding
                if (string.IsNullOrEmpty(uuid))
                {
                    throw new ArgumentNullException(nameof(uuid), "UUID cannot be null or empty.");
                }

                // Get and display the promo types
                string? promoTypes = await GetImagePromoTypesByUuidAsync(uuid);
                string? imageSet = await GetImageSetByUuidAsync(uuid);
                MainWindow.CurrentInstance.ImagePromoLabel.Content = promoTypes ?? string.Empty;
                MainWindow.CurrentInstance.ImageSetLabel.Content = imageSet ?? string.Empty;

                // Get the Scryfall ID
                string? scryfallId = await GetScryfallIdByUuidAsync(uuid);

                if (!string.IsNullOrEmpty(scryfallId) && scryfallId.Length >= 2)
                {
                    char dir1 = scryfallId[0];
                    char dir2 = scryfallId[1];

                    string cardImageUrl = $"https://cards.scryfall.io/normal/front/{dir1}/{dir2}/{scryfallId}.jpg";
                    string secondCardImageUrl = $"https://cards.scryfall.io/normal/back/{dir1}/{dir2}/{scryfallId}.jpg";

                    MainWindow.CurrentInstance.ImageSourceUrl = cardImageUrl;

                    if (await IsDoubleSidedCardAsync(uuid))
                    {
                        MainWindow.CurrentInstance.ImageSourceUrl2nd = secondCardImageUrl;
                    }
                    else
                    {
                        MainWindow.CurrentInstance.ImageSourceUrl2nd = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing card image: {ex.Message}");
                MessageBox.Show($"Error showing card image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DBAccess.CloseConnection();
            }
        }
        private static string? GetOldestCardUuid(string cardName)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(cardName))
                {
                    throw new ArgumentException("Card name cannot be null or empty.", nameof(cardName));
                }

                // Step 1: Fetch all setCodes for the given card name
                List<string> setCodes = [];
                string fetchSetCodesQuery = "SELECT setCode FROM cards WHERE name = @cardName;";

                using (SQLiteCommand fetchSetCodesCommand = new(fetchSetCodesQuery, DBAccess.connection))
                {
                    fetchSetCodesCommand.Parameters.AddWithValue("@cardName", cardName);

                    using SQLiteDataReader reader = fetchSetCodesCommand.ExecuteReader();
                    while (reader.Read())
                    {
                        string setCode = reader["setCode"].ToString() ?? string.Empty;
                        setCodes.Add(setCode);
                    }
                }

                // Remove "PRM" from the list if it exists. Otherwise Magic Online Promos will always be the oldest set. 
                setCodes.Remove("PRM");

                if (setCodes.Count == 0)
                {
                    Debug.WriteLine($"No sets found for the specified card name: {cardName}");
                    return null;
                }

                // Step 2: Find the oldest setCode using a single query
                string fetchOldestSetCodeQuery = @"
                    SELECT code 
                    FROM sets
                    WHERE code IN ({0})
                    ORDER BY releaseDate ASC
                    LIMIT 1;";

                // Dynamically construct the IN clause
                string inClause = string.Join(", ", setCodes.Select((_, i) => $"@setCode{i}"));
                fetchOldestSetCodeQuery = string.Format(fetchOldestSetCodeQuery, inClause);

                string? oldestSetCode = null;

                using (SQLiteCommand fetchOldestSetCodeCommand = new(fetchOldestSetCodeQuery, DBAccess.connection))
                {
                    // Add parameters for the IN clause
                    for (int i = 0; i < setCodes.Count; i++)
                    {
                        fetchOldestSetCodeCommand.Parameters.AddWithValue($"@setCode{i}", setCodes[i]);
                    }

                    using SQLiteDataReader reader = fetchOldestSetCodeCommand.ExecuteReader();
                    if (reader.Read())
                    {
                        oldestSetCode = reader["code"].ToString();
                    }
                }

                if (string.IsNullOrEmpty(oldestSetCode))
                {
                    Debug.WriteLine($"No valid oldest set code found for card: {cardName}");
                    return null;
                }

                // Step 3: Fetch the uuid for the card with the oldest setCode
                string fetchUuidQuery = "SELECT uuid FROM cards WHERE name = @cardName AND setCode = @setCode;";

                using (SQLiteCommand fetchUuidCommand = new(fetchUuidQuery, DBAccess.connection))
                {
                    fetchUuidCommand.Parameters.AddWithValue("@cardName", cardName);
                    fetchUuidCommand.Parameters.AddWithValue("@setCode", oldestSetCode);

                    using SQLiteDataReader reader = fetchUuidCommand.ExecuteReader();
                    if (reader.Read())
                    {
                        return reader["uuid"].ToString();
                    }
                }

                Debug.WriteLine($"No uuid found for card: {cardName} in the oldest set code: {oldestSetCode}.");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetOldestCardUuid: {ex.Message}");
                throw;
            }
        }
        private static async Task<string?> GetScryfallIdByUuidAsync(string uuid)
        {
            string query = "SELECT scryfallId FROM cardIdentifiers WHERE uuid = @uuid UNION ALL SELECT scryfallId FROM tokenIdentifiers WHERE uuid = @uuid";

            using var command = new SQLiteCommand(query, DBAccess.connection);
            command.Parameters.AddWithValue("@uuid", uuid);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return reader["scryfallId"].ToString();
            }
            return null;
        }
        private static async Task<string?> GetImagePromoTypesByUuidAsync(string uuid)
        {
            string query = "SELECT promoTypes FROM cards WHERE uuid = @uuid UNION ALL SELECT promoTypes FROM tokens WHERE uuid = @uuid";

            using var command = new SQLiteCommand(query, DBAccess.connection);
            command.Parameters.AddWithValue("@uuid", uuid);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var promoTypes = reader["promoTypes"]?.ToString();
                if (!string.IsNullOrEmpty(promoTypes))
                {
                    return "Promo type: " + promoTypes;
                }
            }
            return null;
        }
        private static async Task<string?> GetImageSetByUuidAsync(string uuid)
        {
            string query = "SELECT s.name FROM sets s JOIN cards c ON s.code = c.setCode WHERE c.uuid = @uuid " +
               "UNION ALL " +
               "SELECT s.name FROM sets s JOIN tokens t ON s.tokenSetCode = t.setCode WHERE t.uuid = @uuid;";

            using var command = new SQLiteCommand(query, DBAccess.connection);
            command.Parameters.AddWithValue("@uuid", uuid);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var imageSet = reader["name"]?.ToString();
                if (!string.IsNullOrEmpty(imageSet))
                {
                    return imageSet;
                }
            }
            return null;
        }
        private static async Task<bool> IsDoubleSidedCardAsync(string uuid)
        {
            string query = "SELECT side FROM cards WHERE uuid = @uuid UNION ALL SELECT side FROM tokens WHERE uuid = @uuid";
            using var command = new SQLiteCommand(query, DBAccess.connection);
            command.Parameters.AddWithValue("@uuid", uuid);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return reader["side"].ToString() == "a";
            }
            return false;
        }
    }
}
