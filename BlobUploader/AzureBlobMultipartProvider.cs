using BlobUploader.Models;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace BlobUploader
{
    public class MultipartBlobStream : Stream
    {
        public static bool ReplaceFilenamesWithGuids { get; set; }

        public MultipartBlobStream(CloudBlobContainer container, string filename, string extension)
        {
            extension = extension.ToLowerInvariant().TrimStart('.');
            if (filename.EndsWith(extension))
            {
                filename = filename.Substring(0, filename.Length - 1 - extension.Length);
            }
            this.blobContainer = container;
            if (ReplaceFilenamesWithGuids)
            {
                this.FileName = Guid.NewGuid().ToString("N");
            }
            else
            {
                this.FileName = filename;
            }
            this.Extension = extension;
        }

        private CloudBlobContainer blobContainer;
        private MemoryStream underlyingStream = new MemoryStream();

        public string FileName { get; set; }
        public string Extension { get; set; }

        public override bool CanRead { get { return this.underlyingStream.CanRead; } }

        public override bool CanSeek { get { return this.underlyingStream.CanSeek; } }

        public override bool CanWrite { get { return this.underlyingStream.CanWrite; } }

        public override long Length { get { return this.underlyingStream.Length; } }

        public override long Position
        {
            get { return this.underlyingStream.Position; }
            set { this.underlyingStream.Position = value; }
        }

        public override void Flush()
        {
            this.underlyingStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.underlyingStream.Read(buffer, offset, count);
        }

        public override int ReadByte()
        {
            return this.underlyingStream.ReadByte();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this.underlyingStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            this.underlyingStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.underlyingStream.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value)
        {
            this.underlyingStream.WriteByte(value);
        }

        public string BlobPath()
        {
            return Uri.EscapeUriString(string.Format("{0}.{1}", this.FileName, this.Extension));
        }

        public async Task<Uri> UploadStreamToBlobAsync()
        {
            var blobPath = this.BlobPath();
            Trace.TraceInformation("Stream closed, writing {0} bytes to {1} in {2}.", this.underlyingStream.Length, blobPath, this.blobContainer.Name);
            this.underlyingStream.Position = 0;
            var blob = this.blobContainer.GetBlockBlobReference(blobPath);
            await blob.UploadFromStreamAsync(this.underlyingStream);
            return blob.Uri;
        }
    }

    /// <summary>
    /// Processes uploaded images into Blob storage. Uses 
    /// </summary>
    public class AzureBlobMultipartProvider : MultipartStreamProvider
    {
        private CloudBlobContainer imageBlobContainer;
        private CloudTable imageDataTable;

        public AzureBlobMultipartProvider(CloudBlobContainer imageBlobContainer, CloudTable imageDataTable)
        {
            this.imageBlobContainer = imageBlobContainer;
            this.imageDataTable = imageDataTable;
            this.FormFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            this.FormFiles = new Dictionary<string, MultipartBlobStream>();
        }

        public IDictionary<string, string> FormFields { get; private set; }

        public IDictionary<string, MultipartBlobStream> FormFiles { get; private set; }

        public override Stream GetStream(HttpContent parent, HttpContentHeaders headers)
        {
            ContentDispositionHeaderValue contentDisposition = headers.ContentDisposition;
            if (contentDisposition != null)
            {
                // Found a file! Track it, and ultimately upload to blob store.
                if (!String.IsNullOrWhiteSpace(contentDisposition.FileName))
                {
                    var fileInfo = new FileInfo(contentDisposition.FileName.Trim('"'));
                    var blobStream = new MultipartBlobStream(this.imageBlobContainer, fileInfo.Name, fileInfo.Extension);
                    this.FormFiles[fileInfo.Name] = blobStream;
                    return blobStream;
                }
                else
                {
                    return new MemoryStream();
                }
            }
            else
            {
                throw new InvalidOperationException("No 'Content-Disposition' header");
            }
        }

        /// <summary>
        /// Read the non-file contents as form data.
        /// </summary>
        /// <returns></returns>
        public override async Task ExecutePostProcessingAsync()
        {
            foreach (var formContent in Contents)
            {
                ContentDispositionHeaderValue contentDisposition = formContent.Headers.ContentDisposition;
                // Not a file, treat as a form field.
                if (String.IsNullOrWhiteSpace(contentDisposition.FileName))
                {
                    var fieldName = (contentDisposition.Name ?? "").Trim('"');
                    var fieldValue = await formContent.ReadAsStringAsync();
                    this.FormFields[fieldName] = fieldValue;
                }
            }
        }

        public async Task<IEnumerable<UploadedImage>> SaveAllAsync(string imageDescription)
        {
            var tasks = new List<Task<UploadedImage>>();
            foreach (var blob in this.FormFiles.Values)
            {
                tasks.Add(Task.Run(async () =>
                {
                    UploadedImage upload = null;
                    try
                    {
                        var blobUri = await blob.UploadStreamToBlobAsync();

                        upload = new UploadedImage()
                        {
                            PartitionKey = blob.FileName,
                            RowKey = blob.Extension,
                            FileName = blob.FileName,
                            Extension = blob.Extension,
                            BlobPath = blobUri.ToString(),
                            UploadedOn = DateTime.UtcNow
                        };
                        var upsert = TableOperation.InsertOrReplace(upload);
                        await this.imageDataTable.ExecuteAsync(upsert);
                    }
                    catch (Exception e)
                    {
                        Trace.TraceError("Failed to store metadata for {0}: {1}", blob.FileName, e);
                    }

                    return upload;
                }));
            }

            return await Task.WhenAll(tasks);
        }
    }
}
