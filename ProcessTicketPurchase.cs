using System;
using System.Text.Json;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient; // ✅ Only the modern package

namespace TicketHubFunction
{
    public class ProcessTicketPurchase
    {
        private readonly ILogger<ProcessTicketPurchase> _logger;

        public ProcessTicketPurchase(ILogger<ProcessTicketPurchase> logger)
        {
            _logger = logger;
        }

        [Function(nameof(ProcessTicketPurchase))]
        public void Run(
            [QueueTrigger("tickethub", Connection = "AzureStorageConnectionString")] QueueMessage message)
        {
            _logger.LogInformation("➡️ Queue trigger received a message.");

            try
            {
                // Deserialize the JSON message safely
                TicketPurchase? purchase = JsonSerializer.Deserialize<TicketPurchase>(message.MessageText);

                if (purchase == null)
                {
                    _logger.LogWarning("⚠️ Failed to deserialize TicketPurchase.");
                    return;
                }

                // Get SQL connection string from environment
                string? connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                {
                    _logger.LogError("❌ SQL connection string is missing.");
                    return;
                }

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        INSERT INTO TicketPurchases 
                        (ConcertId, Name, Email, Phone, Quantity, CreditCard, Expiration, SecurityCode, Address, City, Province, PostalCode, Country, PurchaseDate)
                        VALUES 
                        (@ConcertId, @Name, @Email, @Phone, @Quantity, @CreditCard, @Expiration, @SecurityCode, @Address, @City, @Province, @PostalCode, @Country, @PurchaseDate)";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@ConcertId", purchase.ConcertId);
                        cmd.Parameters.AddWithValue("@Name", purchase.Name ?? "");
                        cmd.Parameters.AddWithValue("@Email", purchase.Email ?? "");
                        cmd.Parameters.AddWithValue("@Phone", purchase.Phone ?? "");
                        cmd.Parameters.AddWithValue("@Quantity", purchase.Quantity);
                        cmd.Parameters.AddWithValue("@CreditCard", purchase.CreditCard ?? "");
                        cmd.Parameters.AddWithValue("@Expiration", purchase.Expiration ?? "");
                        cmd.Parameters.AddWithValue("@SecurityCode", purchase.SecurityCode ?? "");
                        cmd.Parameters.AddWithValue("@Address", purchase.Address ?? "");
                        cmd.Parameters.AddWithValue("@City", purchase.City ?? "");
                        cmd.Parameters.AddWithValue("@Province", purchase.Province ?? "");
                        cmd.Parameters.AddWithValue("@PostalCode", purchase.PostalCode ?? "");
                        cmd.Parameters.AddWithValue("@Country", purchase.Country ?? "");
                        cmd.Parameters.AddWithValue("@PurchaseDate", purchase.PurchaseDate);

                        cmd.ExecuteNonQuery();
                    }
                }

                _logger.LogInformation("✅ Ticket purchase saved to Azure SQL Database.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Exception occurred: {ex.Message}");
                if (ex.InnerException != null)
                    _logger.LogError($"🔎 Inner exception: {ex.InnerException.Message}");
                _logger.LogError($"📄 Stack trace: {ex.StackTrace}");
            }
        }
    }
}
