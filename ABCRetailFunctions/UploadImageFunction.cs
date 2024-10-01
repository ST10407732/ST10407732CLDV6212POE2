using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System;
using Microsoft.AspNetCore.Http;

namespace ABCRetailFunctions
{
    public class UploadImageFunction
    {
        private readonly ILogger<UploadImageFunction> _logger;

        public UploadImageFunction(ILogger<UploadImageFunction> logger)
        {
            _logger = logger;
        }

        [Function("UploadImageFunction")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("Processing UploadImageFunction request...");

            // Check if the request contains a file
            if (!req.Form.Files.Any())
            {
                _logger.LogError("No file found in the request.");
                return new BadRequestObjectResult("No file uploaded.");
            }

            // Get the first file from the form data
            IFormFile file = req.Form.Files[0];

            // Validate file type (optional)
            if (file == null || file.Length == 0)
            {
                _logger.LogError("Invalid file.");
                return new BadRequestObjectResult("Invalid file.");
            }

            // Generate a unique file name using the original file name and a timestamp
            var originalFileName = Path.GetFileNameWithoutExtension(file.FileName);
            var fileExtension = Path.GetExtension(file.FileName);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss"); // Format: YYYYMMDDHHMMSS
            var fileName = $"{originalFileName}_{timestamp}{fileExtension}";

            // Get the Azure Blob Storage connection string from environment variables
            string blobStorageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

            // Initialize the BlobServiceClient and BlobContainerClient
            BlobServiceClient blobServiceClient = new BlobServiceClient(blobStorageConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient("product-images");

            // Ensure the container exists
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            // Get a reference to a blob and upload the image
            BlobClient blobClient = containerClient.GetBlobClient(fileName);

            // Upload the file to Blob Storage
            using (var stream = file.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });
            }

            // Get the blob's URI (the image URL)
            var imageUrl = blobClient.Uri.ToString();

            _logger.LogInformation($"Image uploaded successfully: {imageUrl}");

            // Return the image URL
            return new OkObjectResult(new { ImageUrl = imageUrl });
        }
    }
}