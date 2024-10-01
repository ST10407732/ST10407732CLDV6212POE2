using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System;

namespace ABCRetailFunctions
{
    public class AddCustomerProfileFunction
    {
        private readonly ILogger<AddCustomerProfileFunction> _logger;

        public AddCustomerProfileFunction(ILogger<AddCustomerProfileFunction> logger)
        {
            _logger = logger;
        }

        [Function("AddCustomerProfileFunction")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("Processing AddCustomerProfileFunction request...");

            // Read and deserialize the request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var customerProfile = JsonConvert.DeserializeObject<CustomerProfile>(requestBody);

            // Validate customer profile data
            if (customerProfile == null || string.IsNullOrEmpty(customerProfile.CustomerId.ToString()) || string.IsNullOrEmpty(customerProfile.Name))
            {
                _logger.LogError("Invalid customer profile data: {RequestBody}", requestBody);
                return new BadRequestObjectResult("Invalid customer profile data.");
            }

            // Get the Azure Table Storage connection string
            string storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

            // Initialize TableClient for "CustomerProfiles" table
            var tableClient = new TableClient(storageConnectionString, "CustomerProfiles");

            // Ensure the table exists
            await tableClient.CreateIfNotExistsAsync();

            // Create a new TableEntity to store the customer profile
            var tableEntity = new TableEntity("CustomerProfilePartition", customerProfile.CustomerId.ToString())
            {
                { "CustomerId", customerProfile.CustomerId },
                { "Name", customerProfile.Name },
                { "Email", customerProfile.Email },
                { "PhoneNumber", customerProfile.PhoneNumber },
                { "Address", customerProfile.Address },
                { "DateOfBirth", customerProfile.DateOfBirth },
                { "LoyaltyPoints", customerProfile.LoyaltyPoints }
            };

            // Add the entity to the Azure Table
            await tableClient.AddEntityAsync(tableEntity);

            _logger.LogInformation($"Customer profile for {customerProfile.Name} added successfully.");
            return new OkObjectResult("Customer profile added successfully.");
        }
    }

    // Define the CustomerProfile model to match your data structure
    public class CustomerProfile
    {
        public string CustomerId { get; set; }  // Unique identifier for the customer
        public string Name { get; set; } // Name of the customer
        public string Email { get; set; } // Email of the customer
        public string PhoneNumber { get; set; } // Phone number of the customer
        public string Address { get; set; } // Address of the customer
        public DateTimeOffset? DateOfBirth { get; set; } // Date of birth of the customer
        public int LoyaltyPoints { get; set; } // Loyalty points of the customer
    }
}
