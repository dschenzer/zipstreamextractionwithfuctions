using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace schenzer.zipstream
{
    public static class zipstream_fromblob
    {
        [FunctionName("zipstream_fromblob")]
        public static async Task Run([BlobTrigger("zipcontainer/{name}", Connection = "schenzercode_STORAGE")]Stream myBlob, string name, [OrchestrationClient] DurableOrchestrationClient orchestrationClient, ILogger log)
        {
            log.LogInformation($"Start Processing: C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");

            string destinationStorageAccount = Environment.GetEnvironmentVariable("destinationStorageAccount");
            string destinationStorageAccountContainer = Environment.GetEnvironmentVariable("destinationStorageAccountContainer");

            try
            {
                // Test for Zip File.
                if (name.Split(".").Last().ToLower() == "zip")
                {
                    CloudStorageAccount destinationCloudStorageAccount = CloudStorageAccount.Parse(destinationStorageAccount);
                    CloudBlobClient blobClient = destinationCloudStorageAccount.CreateCloudBlobClient();

                    CloudBlobContainer destinationCloudContainer = blobClient.GetContainerReference(destinationStorageAccountContainer);

                    ZipStreamExtractionMetadata requestMetadata;
                    List<ZipStreamArchiveEntry> archiveEntries;

                    using (ZipArchive blobZipStream = new ZipArchive(myBlob))
                    {
                        archiveEntries = new List<ZipStreamArchiveEntry>();

                        requestMetadata = new ZipStreamExtractionMetadata()
                        {
                            ArchiveFileName = name,
                            ArchiveFileLength = myBlob.Length
                        };

                        // Get entries of ZIP Container file to iterate on
                        foreach (var fileEntry in blobZipStream.Entries)
                        {
                            log.LogInformation($"Currently processing ZIP entry: {fileEntry.Name} \n");
                            string valideExtractName = Regex.Replace(fileEntry.Name, @"[^a-zA-Z0-9\-]","-").ToLower();

                            if (string.IsNullOrEmpty(valideExtractName) || !fileEntry.Name.Contains("."))
                            {
                                log.LogInformation($"ZIP Entry is empty or not a falid file: '{fileEntry.Name}' \n");
                                continue;
                            }

                            ZipStreamArchiveEntry zipEntry = new ZipStreamArchiveEntry()
                            {
                                DestinationBlobName = valideExtractName,
                                OriginalFileName = fileEntry.FullName,
                                ShortFileName = fileEntry.Name
                            };

                            archiveEntries.Add(zipEntry);

                            CloudBlockBlob destinationBlob = destinationCloudContainer.GetBlockBlobReference(valideExtractName);
                            destinationBlob.Properties.ContentType = "text/plain";
                            log.LogInformation($"Start uploaded extracted ZIP entry stream: {fileEntry.Name} to destination container {destinationBlob.Name} \n");

                            using (Stream destinationMemoryStream = fileEntry.Open())
                            {
                                await destinationBlob.UploadFromStreamAsync(destinationMemoryStream);
                            }

                            log.LogInformation($"Uploaded extracted ZIP entry: {fileEntry.Name} to destination container {destinationBlob.Name} \n");
                        }                       

                    }

                    requestMetadata.FileToBeExtracted = archiveEntries;

                    string instanceId = await orchestrationClient.StartNewAsync("OrchestrateZipStreamExtract", requestMetadata);
                    log.LogInformation($"ZipStreamOrchastrator has been started with {requestMetadata.ArchiveFileName} and request id '{instanceId}' \n");
                }
                else
                {
                    log.LogWarning($"The blob file is not a ZIP file to be extracted \n Name:{name} \n");
                }
            }
            catch (Exception ex)
            {                
                log.LogError($"Exception in processing files: Extracted file \n Name:{name} \n Message: {ex.Message} \n");
            }

            log.LogInformation($"End: C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");
        }
    }
}
