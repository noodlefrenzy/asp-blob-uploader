# asp-blob-uploader

Example of how to upload form file data to blob storage without using local file storage.

Uses a custom `MultipartStreamProvider` to process incoming MIME forms with image data and upload directly to blob storage (passing through a `MemoryStream` on the way, so not the scalability miracle it might at first appear, but avoids having to cope with a lack of local storage).
