using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Files.Shares;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Http;

namespace ABCRetailFunctions
{
    public static class UploadFileFunction
    {
        [Function("UploadFileFunction")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequestData req,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("UploadFileFunction");
            logger.LogInformation("Processing request to upload file to Azure File Share...");

            // Read the file name from the headers
            if (!req.Headers.TryGetValues("file-name", out var fileNameValues))
            {
                logger.LogError("File name is missing from headers.");
                var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("File name is required.");
                return badResponse;
            }

            string fileName = fileNameValues.FirstOrDefault();

            if (string.IsNullOrEmpty(fileName))
            {
                logger.LogError("File name is empty.");
                var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("File name is required.");
                return badResponse;
            }

            // Read the stream into a memory stream
            using var memoryStream = new MemoryStream();
            await req.Body.CopyToAsync(memoryStream);
            memoryStream.Position = 0; // Reset stream position

            if (memoryStream.Length == 0)
            {
                logger.LogError("File stream is empty.");
                var emptyStreamResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await emptyStreamResponse.WriteStringAsync("File stream is empty.");
                return emptyStreamResponse;
            }

            try
            {
                // Azure File Share logic with both file share and subdirectory
                var fileShareClient = new ShareClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"), "documents");

                // Access the "documents-directory" within the file share
                var directoryClient = fileShareClient.GetDirectoryClient("documents-directory");

                // Ensure that the directory exists, if not, you can optionally create it
                await directoryClient.CreateIfNotExistsAsync();

                var fileClient = directoryClient.GetFileClient(fileName);

                // Create the file and upload the content
                await fileClient.CreateAsync(memoryStream.Length);
                await fileClient.UploadAsync(memoryStream);

                logger.LogInformation($"File {fileName} uploaded successfully to 'documents/documents-directory' in Azure File Share.");

                var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await response.WriteStringAsync($"File {fileName} uploaded successfully to Azure File Share.");
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError($"File upload failed: {ex.Message} - StackTrace: {ex.StackTrace}");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"File upload failed: {ex.Message}");
                return errorResponse;
            }

        }
    }
}
