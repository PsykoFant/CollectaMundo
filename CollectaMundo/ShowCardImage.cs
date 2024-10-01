using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo
{
    class ShowCardImage
    {
        // Method to show the card image and handle promo types
        public static async Task ShowImage(string uuid, string? types = null)
        {
            try
            {
                await DBAccess.OpenConnectionAsync();

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
        private static async Task<string?> GetScryfallIdByUuidAsync(string uuid)
        {
            string query = "SELECT scryfallId FROM cardIdentifiers WHERE uuid = @uuid UNION ALL SELECT scryfallId FROM tokenIdentifiers WHERE uuid = @uuid";

            using (var command = new SQLiteCommand(query, DBAccess.connection))
            {
                command.Parameters.AddWithValue("@uuid", uuid);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return reader["scryfallId"].ToString();
                    }
                }
            }
            return null;
        }
        private static async Task<string?> GetImagePromoTypesByUuidAsync(string uuid)
        {
            string query = "SELECT promoTypes FROM cards WHERE uuid = @uuid UNION ALL SELECT promoTypes FROM tokens WHERE uuid = @uuid";

            using (var command = new SQLiteCommand(query, DBAccess.connection))
            {
                command.Parameters.AddWithValue("@uuid", uuid);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        var promoTypes = reader["promoTypes"]?.ToString();
                        if (!string.IsNullOrEmpty(promoTypes))
                        {
                            return "Promo type: " + promoTypes;
                        }
                    }
                }
            }
            return null;
        }
        private static async Task<string?> GetImageSetByUuidAsync(string uuid)
        {
            string query = "SELECT s.name FROM sets s JOIN cards c ON s.code = c.setCode WHERE c.uuid = @uuid " +
               "UNION ALL " +
               "SELECT s.name FROM sets s JOIN tokens t ON s.tokenSetCode = t.setCode WHERE t.uuid = @uuid;";

            using (var command = new SQLiteCommand(query, DBAccess.connection))
            {
                command.Parameters.AddWithValue("@uuid", uuid);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        var imageSet = reader["name"]?.ToString();
                        if (!string.IsNullOrEmpty(imageSet))
                        {
                            return imageSet;
                        }
                    }
                }
            }
            return null;
        }
        private static async Task<bool> IsDoubleSidedCardAsync(string uuid)
        {
            string query = "SELECT side FROM cards WHERE uuid = @uuid UNION ALL SELECT side FROM tokens WHERE uuid = @uuid";
            using (var command = new SQLiteCommand(query, DBAccess.connection))
            {
                command.Parameters.AddWithValue("@uuid", uuid);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return reader["side"].ToString() == "a";
                    }
                }
            }
            return false;
        }
    }
}
