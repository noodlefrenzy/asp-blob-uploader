using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlobUploader
{
    public static class AzureUtilities
    {
        public const string DefaultImageDataTable = "imagedata";
        public const string ImageDataTableKey = "ImageDataTable";

        public const string ImageBlobContainerName = "images";

        // Cache the configuration data.
        private static ConcurrentDictionary<string, string> _ConfigurationEntries = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// Pulls configuration entries from either the CloudConfigurationManager (app/web.config or the cscfg if deployed) or the environment variable of the same name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>The found value, or null.</returns>
        /// <remarks>Side-effect: Stores the result in the dictionary cache.</remarks>
        public static string FromConfiguration(string name)
        {
            return _ConfigurationEntries.GetOrAdd(name, x => CloudConfigurationManager.GetSetting(x) ?? Environment.GetEnvironmentVariable(name));
        }

        /// <summary>
        /// Get/create the Azure Table Storage table for image data.
        /// </summary>
        /// <param name="connectionStringOrKey"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public static async Task<CloudTable> GetImageDataTableAsync(string connectionStringOrKey = null, string tableName = null)
        {
            tableName = tableName ?? FromConfiguration(ImageDataTableKey) ?? DefaultImageDataTable;
            Trace.TraceInformation("Image data table: {0}", tableName);
            var table = GetTableClient(connectionStringOrKey)
                .GetTableReference(tableName);
            await table.CreateIfNotExistsAsync();

            return table;
        }

        public static CloudTableClient GetTableClient(string connectionStringOrKey = null)
        {
            return GetStorageAccount(connectionStringOrKey).CreateCloudTableClient();
        }

        public static CloudBlobClient GetBlobClient(string connectionStringOrKey = null)
        {
            return GetStorageAccount(connectionStringOrKey).CreateCloudBlobClient();
        }

        public static CloudStorageAccount GetStorageAccount(string connectionStringOrKey = null)
        {
            var key = FromConfiguration(connectionStringOrKey ?? "StorageConnectionString");
            if (key == null)
            {
                // NOTE: In the real world, you'd want to remove this, so you didn't log keys.
                Trace.TraceInformation("Couldn't find '{0}' as setting, assuming it's the actual key.", connectionStringOrKey);
                key = connectionStringOrKey;
            }

            return CloudStorageAccount.Parse(key);
        }

        public static async Task<CloudBlobContainer> GetImageBlobContainerAsync(string connectionStringOrKey = null)
        {
            var blobClient = GetBlobClient(connectionStringOrKey);

            var container = blobClient.GetContainerReference(ImageBlobContainerName);

            // Create the container if it doesn't already exist
            await container.CreateIfNotExistsAsync();

            // Enable public access to blobs but not the full container
            var permissions = await container.GetPermissionsAsync();
            if (permissions.PublicAccess == BlobContainerPublicAccessType.Off)
            {
                permissions.PublicAccess = BlobContainerPublicAccessType.Blob;
                await container.SetPermissionsAsync(permissions);
            }

            return container;
        }
    }
}
