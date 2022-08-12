using System;
using System.IO;
using System.IO.Compression;
using Azure.Storage.Blobs.Specialized;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using PgpCore;
using Org.BouncyCastle.Bcpg.OpenPgp;



namespace FunctionApp_unzipDecrypt
{
    public class unzipDecrypt
    {
        [FunctionName("unzipDecrypt")]
        public async Task Run([BlobTrigger("ingress/{name}", Connection = "con_str")] Stream myBlob, string name, ILogger log)
        {
            log.LogInformation($"C# Blob trigger function Processing blob\n Name:{name} \n Size: {myBlob.Length} Bytes");
            string privateKeyBase64 = Environment.GetEnvironmentVariable("pgp-private-key");
            string passPhrase = Environment.GetEnvironmentVariable("pgp-passphrase");

            if (string.IsNullOrEmpty(privateKeyBase64))
            {
                log.LogInformation($"Please add a base64 encoded private key to an environment variable called pgp-private-key");
            }

            byte[] privateKeyBytes = Convert.FromBase64String(privateKeyBase64);
            string privateKey = Encoding.UTF8.GetString(privateKeyBytes);
            
            Stream decryptedData = null;
                try
                {
                     decryptedData = await DecryptAsync(myBlob, privateKey, passPhrase);
                    //return new OkObjectResult(decryptedData);
                }
                catch (PgpException pgpException)
                {
                    //return new BadRequestObjectResult(pgpException.Message);
                    log.LogInformation(pgpException.Message);
                }


            try
            {
                if (name.Substring(name.Length - 4).ToLower() == ".pgp")
                {

                    // Save blob(zip file) contents to a Memory Stream.
                    using (MemoryStream zipBlobFileStream = new MemoryStream())
                    {
                        //await myBlob.CopyToAsync(zipBlobFileStream);
                        decryptedData.CopyTo(zipBlobFileStream);
                        await zipBlobFileStream.FlushAsync();
                        zipBlobFileStream.Position = 0;
                        
                        using (ZipArchive archive = new ZipArchive(decryptedData))
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
                                        await blobClient.UploadAsync(fileStream);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogInformation($"Exception while unzipping: {ex.Message}");

            }
        }


        private static async Task<Stream> DecryptAsync(Stream inputStream, string privateKey, string passPhrase)
        {
            using (PGP pgp = new PGP())
            {
                Stream outputStream = new MemoryStream();

                using (inputStream)
                using (Stream privateKeyStream = privateKey.ToStream())
                {
                    await pgp.DecryptStreamAsync(inputStream, outputStream, privateKeyStream, passPhrase);
                    outputStream.Seek(0, SeekOrigin.Begin);
                    return outputStream;
                }
            }
        }

    }
}
