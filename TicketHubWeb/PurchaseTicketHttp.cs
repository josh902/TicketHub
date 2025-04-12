using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace TicketHubFunction
{
    public class PurchaseTicketHttp
    {
        private readonly ILogger<PurchaseTicketHttp> _logger;

        public PurchaseTicketHttp(ILogger<PurchaseTicketHttp> logger)
        {
            _logger = logger;
        }

        [Function("PurchaseTicket")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "purchase")] HttpRequestData req,
            FunctionContext executionContext)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation($"Received HTTP request: {requestBody}");

            try
            {
                // Optional: validate the payload by deserializing
                var purchase = JsonSerializer.Deserialize<TicketPurchase>(requestBody);

                // Get the connection string from settings
                string? storageConnection = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

                // Create queue client
                var queueClient = new QueueClient(storageConnection, "tickethub");
                await queueClient.CreateIfNotExistsAsync();

                // Send message to the queue (Base64 is auto-handled)
                await queueClient.SendMessageAsync(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(requestBody)));

                var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await response.WriteStringAsync("Ticket purchase queued successfully.");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Failed to process HTTP request: {ex.Message}");
                var response = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await response.WriteStringAsync($"Error: {ex.Message}");
                return response;
            }
        }
    }
}
