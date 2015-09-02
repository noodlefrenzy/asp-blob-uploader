using BlobUploader.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace BlobUploader.Controllers
{
    public class UploadController : ApiController
    {
        [Route("api/upload")]
        public async Task<IEnumerable<UploadedImage>> Post()
        {
            if (!Request.Content.IsMimeMultipartContent("form-data"))
            {
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
            }

            var multipartStreamProvider = new AzureBlobMultipartProvider(
                await AzureUtilities.GetImageBlobContainerAsync("ImageDataConnectionString"),
                await AzureUtilities.GetImageDataTableAsync("ImageDataConnectionString"));
            var results = await Request.Content.ReadAsMultipartAsync<AzureBlobMultipartProvider>(multipartStreamProvider);
            var imageDescription = results.FormFields["ImageDescription"];

            return await results.SaveAllAsync(imageDescription);
        }
    }
}
