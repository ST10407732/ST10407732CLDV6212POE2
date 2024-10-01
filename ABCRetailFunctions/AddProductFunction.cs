using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using Newtonsoft.Json;
using System;
using Microsoft.AspNetCore.Http;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;

namespace ABCRetailFunctions
{
    public class AddProductFunction
    {
        private readonly ILogger<AddProductFunction> _logger;

        public AddProductFunction(ILogger<AddProductFunction> logger)
        {
            _logger = logger;
        }

        [Function("AddProductFunction")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("Processing AddProductFunction request...");

            // Check if the request contains a file
            if (!req.Form.Files.Any())
            {
                _logger.LogError("No file found in the request.");
                return new BadRequestObjectResult("No file uploaded.");
            }

            IFormFile file = req.Form.Files[0];

            // Validate file type (optional)
            if (file == null || file.Length == 0)
            {
                _logger.LogError("Invalid file.");
                return new BadRequestObjectResult("Invalid file.");
            }

            // Read the product JSON part from the request
            var productJson = req.Form["product"];
            var product = JsonConvert.DeserializeObject<Product>(productJson);

            if (product == null || string.IsNullOrEmpty(product.ProductId) || string.IsNullOrEmpty(product.ProductName))
            {
                _logger.LogError("Invalid product data.");
                return new BadRequestObjectResult("Invalid product data.");
            }

            // Upload image to Blob Storage
            var imageUrl = await UploadImageAsync(file);
            if (string.IsNullOrEmpty(imageUrl))
            {
                _logger.LogError("Image upload failed.");
                return new BadRequestObjectResult("Image upload failed.");
            }

            product.ImageUrl = imageUrl;

            // Get the Azure Table Storage connection string
            string storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

            var tableClient = new TableClient(storageConnectionString, "Products");

            await tableClient.CreateIfNotExistsAsync();

            var tableEntity = new TableEntity("ProductPartition", product.ProductId)
            {
                { "ProductId", product.ProductId },
                { "ProductName", product.ProductName },
                { "Description", product.Description },
                { "Price", product.Price },
                { "Category", product.Category },
                { "StockQuantity", product.StockQuantity },
                { "ImageUrl", product.ImageUrl }
            };

            await tableClient.AddEntityAsync(tableEntity);

            _logger.LogInformation($"Product {product.ProductName} added successfully.");
            return new OkObjectResult(new { Message = "Product added successfully.", ImageUrl = product.ImageUrl });
        }

        private async Task<string> UploadImageAsync(IFormFile file)
        {
            string blobStorageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

            BlobServiceClient blobServiceClient = new BlobServiceClient(blobStorageConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient("product-images");

            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var fileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(file.FileName)}";

            BlobClient blobClient = containerClient.GetBlobClient(fileName);

            using (var stream = file.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });
            }

            return blobClient.Uri.ToString();
        }
    }
    // Define the Product model to match your data structure
    public class Product
    {
        public string ProductId { get; set; } // Unique identifier for the product
        public string ProductName { get; set; } // Name of the product
        public string Description { get; set; } // Description of the product
        public decimal Price { get; set; } // Price of the product
        public string Category { get; set; } // Category of the product
        public int StockQuantity { get; set; } // Stock quantity of the product
        public string ImageUrl { get; set; } // URL of the product image
    }
}
