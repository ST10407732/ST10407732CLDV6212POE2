using Azure;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace ABCRetailFunctions
{
    public class AddOrderFunction
    {
        private readonly ILogger<AddOrderFunction> _logger;

        public AddOrderFunction(ILogger<AddOrderFunction> logger)
        {
            _logger = logger;
        }

        [Function("AddOrderFunction")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("Processing AddOrderFunction request...");

            // Read and deserialize the request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var order = JsonConvert.DeserializeObject<Order>(requestBody);

            // Validate order data
            if (order == null || string.IsNullOrEmpty(order.OrderId) || string.IsNullOrEmpty(order.CustomerId))
            {
                _logger.LogError("Invalid order data: {RequestBody}", requestBody);
                return new BadRequestObjectResult("Invalid order data.");
            }

            // Get the Azure Table Storage connection string
            string storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

            // Initialize TableClient for "Orders" table
            var tableClient = new TableClient(storageConnectionString, "Orders");

            // Ensure the table exists
            await tableClient.CreateIfNotExistsAsync();

            var tableEntity = new TableEntity(order.PartitionKey ?? "OrderPartition", order.RowKey ?? order.OrderId)
{
    { "OrderId", order.OrderId },
    { "CustomerId", order.CustomerId ?? string.Empty },
    { "ProductId", order.ProductId ?? string.Empty },
    { "OrderDate", order.OrderDate },
    { "TotalAmount", order.TotalAmount }
};


            // Add the entity to the Azure Table
            await tableClient.AddEntityAsync(tableEntity);

            // Initialize the queue client for the outgoing queue
            QueueClient outgoingQueue = new QueueClient(storageConnectionString, "outgoing-order-queue");

            // Ensure the queue exists
            await outgoingQueue.CreateIfNotExistsAsync();
            if (outgoingQueue.Exists())
            {
                // Serialize and add the order message to the outgoing queue
                var queueMessage = JsonConvert.SerializeObject(order);
                await outgoingQueue.SendMessageAsync(queueMessage);
                _logger.LogInformation($"New Order {order.OrderId} added to the outgoing queue.");
            }

            _logger.LogInformation($"Order {order.OrderId} added successfully.");
            return new OkObjectResult("Order added successfully.");
        }
    }

    // Order model definition
    public class Order : ITableEntity
    {
        [Key]
        public string OrderId { get; set; }  // Changed to string
        public string? CustomerId { get; set; }  // FK to CustomerProfile
        public string? ProductId { get; set; }
        public DateTimeOffset OrderDate { get; set; }
        public double TotalAmount { get; set; }

        // ITableEntity implementation
        public string? PartitionKey { get; set; }
        public string? RowKey { get; set; }
        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }
}
