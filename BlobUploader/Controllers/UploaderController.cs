using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace BlobUploader.Controllers
{
    public class UploaderController : Controller
    {
        // GET: Uploader
        public ActionResult Index()
        {
            return View();
        }
    }
}