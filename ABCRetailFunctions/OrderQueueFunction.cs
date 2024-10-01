using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Storage.Queues;  // Required for using QueueClient

public static class OrderQueueFunction
{
    [Function("ProcessOrderQueue")]
    public static async Task Run(
        [QueueTrigger("orders", Connection = "AzureWebJobsStorage")] string queueMessage,
        FunctionContext context)
    {
        var log = context.GetLogger("ProcessOrderQueue");
        log.LogInformation($"Processing queue message: {queueMessage}");

        try
        {
            var order = JsonConvert.DeserializeObject<Order>(queueMessage);
            if (order == null)
            {
                log.LogError("Received invalid order message.");
                return;
            }

            // Add any necessary processing logic here (e.g., sending confirmation emails, updating inventory)
            log.LogInformation($"New Order {order.OrderId} processed successfully.");
        }
        catch (Exception ex)
        {
            log.LogError($"Error processing queue message: {ex.Message}");
        }
    }
}

public class Order
{
    public string OrderId { get; set; }
    public string CustomerId { get; set; }
    public string ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime OrderDate { get; set; }
}
