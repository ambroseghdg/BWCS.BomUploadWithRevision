using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace BWCS.BomUpload.Website.Controllers
{
    public class PDMBOMUploadController : Controller
    {
        // GET: PDMBOMUpload
        public ActionResult Index()
        {
            ModelState.Clear();
            return View();
        }
    }
}