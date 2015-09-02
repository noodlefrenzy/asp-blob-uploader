using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlobUploader.Models
{
    public class UploadedImage : TableEntity
    {
        public string Description { get; set; }

        public string BlobPath { get; set; }

        public string FileName { get; set; }

        public string Extension { get; set; }

        public DateTime UploadedOn { get; set; }
    }
}
