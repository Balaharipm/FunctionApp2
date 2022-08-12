using System;
using System.IO;
using System.IO.Compression;
using Azure.Storage.Blobs.Specialized;
using System.Text.RegularExpressions;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;


namespace FunctionApp_Unzip
{
    public class unzip
    {
        [FunctionName("Unzip")]
        public void Run([BlobTrigger("incoming/{name}", Connection = "conn_str2")]Stream myBlob, string name, ILogger log)
        {
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");
            //using (MemoryStream zipBlobFileStream = new MemoryStream())
            if (name.Substring(name.Length - 4).ToLower() == ".zip")
            {
                using (ZipArchive archive = new ZipArchive(myBlob))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        log.LogInformation($"Now processing {entry.FullName}");

                        //Replace all NO digits, letters, or "-" by a "-" Azure storage is specific on valid characters
                        string valideName = Regex.Replace(entry.Name, @"[^a-zA-Z0-9\-]", "-").ToLower();

                        string connStr = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
                        string toContainer = Environment.GetEnvironmentVariable("ToContainer");
                        BlockBlobClient blobClient = new BlockBlobClient(connStr, toContainer, valideName);

                        using (var fileStream = entry.Open())
                        {
                            if (entry.Length > 0)
                                blobClient.UploadAsync(fileStream);
                        }
                    }
                }
            }
        }
    }
 }

