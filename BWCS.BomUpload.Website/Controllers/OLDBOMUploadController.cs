using BWCS.BomUpload.Website.Models;
using BWCS.BOMUpload.BLL;
using Newtonsoft.Json;
using NLog;
using NLog.Fluent;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Lifetime; 
using System.Security.Claims;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Helpers;
using System.Web.Management;
using System.Web.Mvc;
using System.Web.SessionState;
using System.Web.UI.WebControls.Expressions;
using System.Web.UI.WebControls.WebParts;
using static System.Net.WebRequestMethods;

namespace BWCS.BomUpload.Website.Controllers
{
    [SessionState(SessionStateBehavior.Required)]
    public class OLDBOMUploadController : Controller
    {
        Logger logger = LogManager.GetCurrentClassLogger();
        readonly string emailSMTPHostName = System.Configuration.ConfigurationManager.AppSettings["SMTPHostName"].ToString().Trim();
        readonly string TOEmail = System.Configuration.ConfigurationManager.AppSettings["TOEmail"].ToString().Trim();
        readonly string TOEmailITservice = System.Configuration.ConfigurationManager.AppSettings["TOEmailITservice"].ToString().Trim();
        readonly string TOEmail_Exception = System.Configuration.ConfigurationManager.AppSettings["TOEmail_Exception"].ToString().Trim();

        readonly string FROMEmail = System.Configuration.ConfigurationManager.AppSettings["FROMEmail"].ToString().Trim();

        string CCEmail = System.Configuration.ConfigurationManager.AppSettings["CCEmail"].ToString().Trim();
        readonly string CCEmailITservice = System.Configuration.ConfigurationManager.AppSettings["CCEmailITservice"].ToString().Trim();

        string EmailSubject = System.Configuration.ConfigurationManager.AppSettings["EmailSubject"].ToString().Trim();

        readonly string MakeDelayInProcessMilliseconds = System.Configuration.ConfigurationManager.AppSettings["MakeDelayInProcessMilliseconds"].ToString().Trim();
        readonly string SELECT_ITMRVA = System.Configuration.ConfigurationManager.AppSettings["SELECT_ITMRVA"].ToString().Trim();
        readonly string SELECT_PSTHDR = System.Configuration.ConfigurationManager.AppSettings["SELECT_PSTHDR"].ToString().Trim();
        readonly string SELECT_PSTDTL = System.Configuration.ConfigurationManager.AppSettings["SELECT_PSTDTL"].ToString().Trim();

        string loggedinUser = string.Empty;
        string loggedinUserEmail = string.Empty;  
        string strReadCookieDataForListing = System.Configuration.ConfigurationManager.AppSettings["ReadCookieDataForListing"].ToString();
        XASystemLink xaSyslk = new XASystemLink();
        BOMUploadMasterModel objModel = new BOMUploadMasterModel();

        public bool HasParentItemRevision = false;
        public string ParentItemRevisionVal = string.Empty;
        // public string isPDMData = string.Empty;
        // // if (Request.QueryString.AllKeys.Contains("ParentItemRevision"))

        /// <summary>
        /// Clear model and return index view.
        /// </summary>
        /// <returns></returns>
        public ActionResult Index(string ReadFrom = "", string ParentItem = "")
        {
            try
            {
                ModelState.Clear();
                HasParentItemRevision = Request.QueryString["ParentItemRevision"] != null;
                ParentItemRevisionVal = string.Empty; 
                if (HasParentItemRevision) 
                    { ParentItemRevisionVal = Request.QueryString["ParentItemRevision"].ToString().Trim(); } 

                if (!string.IsNullOrEmpty(ReadFrom))
                {
                    //isPDMData = Request.QueryString["ReadFrom"];
                    return View("PDMIndex");
                }
                else
                {
                    return View();
                }  
            }  
            catch (Exception ex) // || InvalidOperationException ex)
            {
                // Log the error
                System.Diagnostics.Debug.WriteLine("Controller error: " + ex.Message);
                TempData["ErrorMessage"] = "An unexpected error occurred.";
                return RedirectToAction("Index"); // Redirect to avoid breaking the view
            }
        }

        [HttpPost]
        public ActionResult SaveUserEnvironment(string environment, string fromQueryString)
        {
            // fromQueryString = "N" == BASE URL
            // fromQueryString = "Y" ==  PDM URL

            HelperGeneral.SaveUserEnvInCookie(Response, fromQueryString, environment); 
                //if (isPDMData != null)
                //{
                //    if (isPDMData == "SQLDB")
                //        HelperGeneral.SaveUserEnvInCookie(Response, "Y", environment); // PDM URL
                //}
                //else
                //    HelperGeneral.SaveUserEnvInCookie(Response, "N", environment);  //BASE URL

            return RedirectToAction("Index");
        }


        /// <summary>
        /// Get all environments.
        /// </summary>
        /// <returns>Sort the environments based on user selection. If new user, then set default environment.</returns>
        public JsonResult GetAllEnvironments(string fromQueryString, string urlParentItemRevision)
        {
            loggedinUser = string.Empty;
            logger.Info("GetAllEnvironments Started - fromQueryString: " + fromQueryString);
            HasParentItemRevision = !string.IsNullOrEmpty(urlParentItemRevision);
            ParentItemRevisionVal = string.Empty;
            if (HasParentItemRevision)
            { ParentItemRevisionVal = urlParentItemRevision.ToString().Trim(); } 

            try
            {
                loggedinUser = HttpContext.User.Identity.Name.ToString();
                var userParts = loggedinUser.Split('\\');

                if (userParts.Length > 1)
                { loggedinUser = userParts[1].ToUpper(); }
                else
                { loggedinUser = loggedinUser.ToUpper(); }

                if (!string.IsNullOrEmpty(loggedinUser)) { loggedinUserEmail = GetEmailForLoggedInADUser(loggedinUser); }

                logger.Info("GetAllEnvironments by user: " + loggedinUser + Environment.NewLine);

                BOMUploadMasterModel objEnvironment = new BOMUploadMasterModel();
                
                return Json(objEnvironment.GetAllEnvironments(fromQueryString).Select(x => new
                {
                    x.EnvironmentId,
                    x.EnvironmentCode
                }).ToList(), JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                logger.Error("JsonResult GetAllEnvironments Exception Error: " + DateTime.Now.ToString() + ex.Message + System.Environment.NewLine);
                return Json(new { success = false, message = "Failed to retrieve environment list !" }, JsonRequestBehavior.AllowGet);
            }

        }

        /// <summary>
        /// Check for the existence of item number in the chosen environment.
        /// </summary>
        /// <param name="itemNumber">Item number.</param>
        /// <param name="environment">Environment in which the item exists.</param>
        /// <returns>Item information if item exists in the selected environment.</returns>
        [HttpPost]
        public JsonResult CheckItemExistence(string itemNumber, string environment, string site, string qsStrParentItemRevision, string fromQueryString = "N")
        {
            string result = string.Empty;
            string loggedinUser = string.Empty;
            BOMUploadMasterModel clsXA = new BOMUploadMasterModel();
            IList<Item> ItemList = new List<Item>();
            string strUoM; string strITTYP; string strItmDes;

            HasParentItemRevision = !string.IsNullOrEmpty(qsStrParentItemRevision);
            ParentItemRevisionVal = string.Empty;
            if (HasParentItemRevision)
            { ParentItemRevisionVal = qsStrParentItemRevision.ToString().Trim(); }     

            loggedinUser = HttpContext.User.Identity.Name.ToString().Split('\\')[1].ToUpper();
            if (loggedinUserEmail.Trim() == "") loggedinUserEmail = GetEmailForLoggedInADUser(loggedinUser);
            logger.Info("CheckItemExistence | Logged in user : " + loggedinUser + " | Logged in user Email: " + loggedinUserEmail + Environment.NewLine);

            if (string.IsNullOrEmpty(site) || site == "null")
            {
                BOMUploadMasterModel objSite = new BOMUploadMasterModel();
                site = objSite.GetSiteByEnvironment(environment);
                logger.Info("Site pulled from SQL table : " + site + Environment.NewLine);
            }

            string xaSystemName = System.Configuration.ConfigurationManager.AppSettings["XASystemName"];
            string maxIdleTime = System.Configuration.ConfigurationManager.AppSettings["MAXIDLE"];
            string xaUserName = System.Configuration.ConfigurationManager.AppSettings["XAUser"];
            string xaPassword = System.Configuration.ConfigurationManager.AppSettings["XAPwd"];

            if (environment.Length >=2 )
                environment = environment.Substring(0, 2); // Read first two letters for ENV 
 
            logger.Info("Check for Item Existence begins... for itemnumber  " + itemNumber + " in " + environment + " environment, Site: " + site);
            try
            {
                ItemList = clsXA.GetItemDetailsByRevision(itemNumber, environment, site, loggedinUser, ParentItemRevisionVal, out strUoM, out strITTYP, out strItmDes, fromQueryString);
                if (ItemList.Count > 0 && !string.IsNullOrEmpty(ItemList[0].ItemNumber) && !string.IsNullOrEmpty(ItemList[0].ItemDescription))
                {
                    logger.Info("Item " + itemNumber + " found in XA!");
                    //Session["ItemDescription"] = ItemList[0].ItemDescription;
                    System.Web.HttpContext.Current.Application["ItemDescription"] = ItemList[0].ItemDescription;
                }
                else
                {
                    logger.Info("Item " + itemNumber + " does not exist in XA!");
                    Item objItem = new Item();
                    objItem.ItemNumber = string.Empty;
                    objItem.ItemDescription = string.Empty;
                    objItem.CompleteDescription = string.Empty;
                    objItem.DrawingNumber = string.Empty;
                    ItemList.Add(objItem);

                }
            }
            catch (Exception sysex)
            {
                Item objItem = new Item();
                objItem.ItemNumber = itemNumber;
                objItem.ItemDescription = "CheckItemExistence - Systemlink service call error at " + DateTime.Now.ToString() + " | " + sysex.Message;
                objItem.CompleteDescription = "CheckItemExistence - Systemlink service call error at " + DateTime.Now.ToString() + " | " + sysex.Message;
                objItem.DrawingNumber = string.Empty;
                ItemList.Add(objItem);

                logger.Error("CheckItemExistence - Systemlink service call error at " + DateTime.Now.ToString() + sysex.Message + System.Environment.NewLine);
                logger.Info("CheckItemExistence| loggedinUser: " + loggedinUser + " | LoggedinUsereMail: " + loggedinUserEmail); 
                SendEmailAlertNotificationBOMUploadtoXASubject("Upload Bom To XA | CheckItemExistence - Systemlink service call Error: " + sysex.Message, loggedinUserEmail, itemNumber, environment);
            }
            finally
            { }
            var Results = ItemList.Select(
                 Item => new
                 {
                     Item.ItemNumber,
                     Item.ItemDescription,
                     Item.CompleteDescription,
                     Item.DrawingNumber, 
                     Item.ItemType
                 });

            return Json(Results, JsonRequestBehavior.AllowGet);

        }

        /// <summary>
        /// Check for the Single Level BOM header for item number in the chosen environment, Item Type: 1, 2, 9
        /// </summary>
        /// <param name="itemNumber">Item number.</param>
        /// <param name="environment">Environment in which the item exists.</param>
        /// <returns>Item information if item exists in the selected environment.</returns>
        [HttpPost]
        public JsonResult IsVaildHeaderParentItem(string itemNumber, string environment, string site, string fromQueryString = "N")
        {
            string result = string.Empty;
            string loggedinUser = string.Empty;
            BOMUploadMasterModel clsXA = new BOMUploadMasterModel();
            IList<Item> ItemList = new List<Item>();
            string strUoM; string strITTYP; string strParentItemRevision; string strItmDes;
            loggedinUser = HttpContext.User.Identity.Name.ToString().Split('\\')[1].ToUpper();
            logger.Info("IsVaildHeaderParentItem | Logged in user : " + loggedinUser + Environment.NewLine);

            if (string.IsNullOrEmpty(site) || site == "null")
            {
                BOMUploadMasterModel objSite = new BOMUploadMasterModel();
                site = objSite.GetSiteByEnvironment(environment);
                logger.Info("Site pulled from SQL table : " + site + Environment.NewLine);
            }

            logger.Info("Check for IsVaildHeaderParentItem begins... for itemnumber  " + itemNumber + " in " + environment + " environment, Site: " + site);

            ItemList = clsXA.GetItemDetails(itemNumber, environment, site, loggedinUser, out strParentItemRevision, out strUoM, out strITTYP, out strItmDes, fromQueryString);

            var validHeaderType = new List<string> { "0", "1", "2", "9" };
            bool IsValidParentItemHeader = validHeaderType.Contains(strITTYP);

            if (ItemList.Count > 0 && !string.IsNullOrEmpty(ItemList[0].ItemNumber) && IsValidParentItemHeader)
            {
                logger.Info("Parent Item " + itemNumber + " found in XA have valid Item type: " + strITTYP + " to create header in PSTHDR!");
                //Session["ItemDescription"] = ItemList[0].ItemDescription;
                System.Web.HttpContext.Current.Application["ItemDescription"] = ItemList[0].ItemDescription;
            }
            else
            {
                logger.Info("XA Error: Item " + itemNumber + " ( Item Type " + strITTYP + " ) cannot have a BOM. Only Types 0, 1, 2, and 9 can have BOMs. Please change the item type using the NICe tool.");
                Item objItem = new Item();
                objItem.ItemNumber = string.Empty;
                objItem.ItemDescription = string.Empty;
                objItem.CompleteDescription = string.Empty;
                objItem.DrawingNumber = string.Empty;
                ItemList.Add(objItem);

            }

            // Call Logout XA IsVaildHeaderParentItem 
            if (Session["XASlrSession"] != null)
                objModel.LogoutXASyslinkSessionHandle(Session["XASlrSession"].ToString());

            var Results = ItemList.Select(
                 Item => new
                 {
                     Item.ItemNumber,
                     Item.ItemDescription,
                     Item.CompleteDescription,
                     Item.DrawingNumber,
                     Item.ItemType
                 });

            return Json(Results, JsonRequestBehavior.AllowGet);

        }

        /// <summary>
        /// Get Single Level BOM Components.
        /// </summary>
        /// <param name="sord"></param>
        /// <param name="page"></param>
        /// <param name="rows"></param>
        /// <returns>Single level BOM components data.</returns>
        public JsonResult GetSingleLevelBOMComponents(string itemNumber, string environment, string site, string sord, int page, int rows)
        {
            try
            {
                logger.Info("Get Single Level BOM Components begins ");
                string loggedinUser = string.Empty;
                BOMUploadMasterModel clsXA = new BOMUploadMasterModel();
                List<Item> bomComponentList = new List<Item>();
                loggedinUser = HttpContext.User.Identity.Name.ToString().Split('\\')[1].ToUpper();
                logger.Info("GetSingleLevelBOMComponents | Logged in user : " + loggedinUser + Environment.NewLine);
                logger.Info("GetSingleLevelBOMComponents begins... for itemnumber  " + itemNumber + " in " + environment + " environment, Site: " + site);

                if ((string.IsNullOrEmpty(site) || site == "null") && (!string.IsNullOrEmpty(environment)))
                {
                    BOMUploadMasterModel objSite = new BOMUploadMasterModel();
                    site = objSite.GetSiteByEnvironment(environment);
                    logger.Info("Site pulled from SQL table : " + site + Environment.NewLine);
                }

                bomComponentList = clsXA.GetAllSingleLevelBOM(itemNumber, environment, site, loggedinUser);

                var Results = bomComponentList.Select(
                    itemData => new
                    {
                        itemData.MasterCode,
                        itemData.ItemNumber,
                        itemData.UserSequence,
                        itemData.ComponentItemNumber,
                        itemData.ComponentDescription,
                        itemData.ExtendedDescrption1,
                        itemData.ExtendedDescrption2,
                        itemData.CompleteComponentDescription,
                        itemData.Qty,
                        itemData.Revision,
                        itemData.UnitOfMeasure,
                        itemData.ItemType,
                        itemData.Site,
                        itemData.ParentItemNumber,
                        itemData.ParentItemRevision,
                        itemData.AlternateBOMId,
                        itemData.ComponentItem,
                        itemData.ComponentItemRevision,
                        itemData.Environment,
                        itemData.Flag,
                        itemData.Action,
                        itemData.Order
                    });


                Session["BOMData"] = bomComponentList;
                if (bomComponentList.Count > 0)
                {
                    Session["Site"] = (from item in bomComponentList select item.Site).FirstOrDefault();
                    Session["ParentItemNumber"] = (from item in bomComponentList select item.ParentItemNumber).FirstOrDefault();
                    Session["ParentItemRevision"] = (from item in bomComponentList select item.ParentItemRevision).FirstOrDefault();
                }
                else
                {
                    Session["Site"] = site;
                    Session["ParentItemNumber"] = itemNumber;
                    Session["ParentItemRevision"] = "";
                    // TO DO implement Create Header process and Email notification in case any error 
                }
                Session["Environment"] = environment;

                logger.Info("Retrieved single level BOM from XA");

                // Call Logout XA session Retrieved single level BOM from XA 
                if (Session["XASlrSession"] != null)
                    objModel.LogoutXASyslinkSessionHandle(Session["XASlrSession"].ToString());

                int pageIndex = Convert.ToInt32(page) - 1;
                int pageSize = rows;
                int totalRecords = Results.Count();
                var totalPages = (int)Math.Ceiling((float)totalRecords / (float)rows);

                var jsonData = new
                {
                    total = totalPages,
                    page,
                    records = totalRecords,
                    rows = Results
                };
                //HelperGeneral.SaveUserEnvInCookie(Response, "N", environment);  //BASE URL 
                return Json(jsonData, JsonRequestBehavior.AllowGet);

            }
            catch (Exception ex)
            {
                logger.Error(DateTime.Now.ToString() + ex.Message + System.Environment.NewLine);
                return Json(new { success = false, message = "Failed to retrieve single level BOM components from XA !" }, JsonRequestBehavior.AllowGet);
            }

        }


        /// <summary>
        /// Compare PDM and XA BOM data for the parent item and prepare final resultset.
        /// </summary>
        /// <param name="sord"></param>
        /// <param name="page"></param>
        /// <param name="rows"></param>
        /// <returns>.</returns>
        public JsonResult ListComparedComponentsResultByPDMandXAData(string itemNumber, string environment, string site, string urlSQLParentItemRevision, string sord, int page, int rows)
        {
            try
            {
                string loggedinUser = string.Empty;
                BOMUploadMasterModel clsXA = new BOMUploadMasterModel();

                loggedinUser = HttpContext.User.Identity.Name.ToString().Split('\\')[1].ToUpper();
                if (loggedinUserEmail.Trim() == "") loggedinUserEmail = GetEmailForLoggedInADUser(loggedinUser);
                logger.Info("ListComparedComponentsResultByPDMandXAData for ParentItem: " + itemNumber + " | Env.: " + environment + " | Site: " + site + " | Logged in user: " + loggedinUser + " | Logged in user Email: " + loggedinUserEmail + Environment.NewLine);
                bool isValidItem = false;
                bool isValidXAParentItem = false;
                List<Item> NewBOMList = new List<Item>();
                List<Item> ChangeBOMList = new List<Item>();
                List<Item> DeleteBOMList = new List<Item>();
                List<Item> NoChangeBOMList = new List<Item>();
                List<Item> SkippedBOMList = new List<Item>();
                List<Item> BallonChangeBOMList = new List<Item>();
                string strUoM = "";
                string strITTYP = "", strItmDes = "";
                string strParentItemRevision = "";
                string strCompParentItemRevision = "";
                TempData["BOMUploadSuccessMsg"] = "";

                // Get Site from SQL master table if site is blank and environment is not blank.
                if ((string.IsNullOrEmpty(site) || site == "null") && (!string.IsNullOrEmpty(environment)))
                {
                    BOMUploadMasterModel objSite = new BOMUploadMasterModel();
                    site = objSite.GetSiteByEnvironment(environment);
                    logger.Info("Site pulled from SQL table : " + site + " for Env.: " + environment + Environment.NewLine);
                }

                if (strReadCookieDataForListing == "Y")
                {
                    var ENVCookiePDM = Request.Cookies["EnvironmentCookiePDM"];
                    if (!string.IsNullOrEmpty(ENVCookiePDM["EnvironmentPDM"]))
                    {
                        environment = ENVCookiePDM.Value.Replace("EnvironmentPDM=", string.Empty);  
                        BOMUploadMasterModel objSite = new BOMUploadMasterModel();
                        site = objSite.GetSiteByEnvironment(environment);
                        logger.Info("Read Cookie Site pulled from SQL table : " + site + " for Env.: " + environment + Environment.NewLine);
                    }
                }
 
                logger.Info("Get PDM components from SQL temporary table begins - itemNumber | env | site: " + itemNumber + " | " + environment + " | " + site);
                List<Item> PDMbomComponentList = new List<Item>();

                string strItem2DEnv = environment.Substring(0, 2); // Read first two letters for ENV 
                // Get all BOM components from SQL PDMData table for the parent item. //SQL Revision 
                PDMbomComponentList = clsXA.GetPDMComponentsRevisionFromSQL(itemNumber, strItem2DEnv, site, urlSQLParentItemRevision, loggedinUser);
 
                // Check whether the Parent item exists in Item revision file in XA. Only valid items could be processed. strParentItemRevision collected 
                isValidXAParentItem = ValidateItemInXARevision(itemNumber, environment, site, urlSQLParentItemRevision, out strUoM, out strITTYP, out strItmDes);

                // Call Logout XA session 1
                if (Session["XASlrSession"] != null)
                    objModel.LogoutXASyslinkSessionHandle(Session["XASlrSession"].ToString());

                // Process only if records are retrieved from SQL table PDMData.
                if (PDMbomComponentList.Count > 0 && isValidXAParentItem)
                {
                    // Loop through each record and set the flag as 1 if exists and 0 if does not exist.
                    for (int i = 0; i < PDMbomComponentList.Count; i++)
                    {
                        ////// Check whether the item exists in Item revision file in XA. Only valid items could be processed. strCompParentItemRevision collected  
                        ////if (HasUrlParentItemRevision && urlSQLParentItemRevision == "GetLatestRevision")
                        ////    isValidItem = ValidateItemInXA(PDMbomComponentList[i].ComponentItemNumber, environment, site, out strCompParentItemRevision, out strUoM, out strITTYP, out strItmDes);
                        ////else
                        ////    isValidItem = ValidateItemInXARevision(PDMbomComponentList[i].ComponentItemNumber, environment, site, ApplicationLatestParentItemRevision, out strUoM, out strITTYP, out strItmDes);

                        // Check whether the item exists in Item revision file in XA. Only valid items could be processed.strCompParentItemRevision collected  
                        isValidItem = ValidateItemInXARevision(PDMbomComponentList[i].ComponentItemNumber, environment, site, urlSQLParentItemRevision, out strUoM, out strITTYP, out strItmDes);
                        if (isValidItem)
                        {
                            PDMbomComponentList[i].Flag = "1";
                            PDMbomComponentList[i].UnitOfMeasure = strUoM;
                            PDMbomComponentList[i].ItemType = strITTYP;
                            PDMbomComponentList[i].CompleteComponentDescription = strItmDes;
 
                        } // Flag = 1 if item exists in item revision file.
                        else { PDMbomComponentList[i].Flag = "0"; } // Flag = 0 if item does not exist in item revision file.

                        isValidItem = false;
                    }
                    // } -- Compare SQL DB data with XA data if there is data in SQL DB for ParentItem.
                    logger.Info("Get PDM components from SQL temporary table ends.");

                    logger.Info("GetAllSingleLevelBOM XA BOM components begins | ItemNumber | env | site: " + itemNumber + " | " + environment + " | " + site);
                    List<Item> XAbomComponentList = new List<Item>();

                    // Get all BOM components from XA for the parent item.
                    XAbomComponentList = clsXA.GetAllSingleLevelBOMRevision(itemNumber, environment, site, urlSQLParentItemRevision, loggedinUser, "Y");
                    logger.Info("GetAllSingleLevelBOM XA BOM components ends | XAbomComponentList.Count: " + XAbomComponentList.Count.ToString());

                    // If there is no record in XA BOM component list, then all records in SQL PDMData will be added to XA BOM. Note: only valid items.
                    if (XAbomComponentList.Count == 0)
                    {
                        // Create BOM Header if there is no recorded in XA BOM Component list.
                        Session["CreateBOMHeader"] = "Yes";
                        PDMbomComponentList.Where(item => item.Flag == "1").ToList().ForEach(item => { item.Action = "Add"; item.Order = 1; item.Note = "Is not in XA BOM"; });
                        PDMbomComponentList.Where(item => item.Flag == "0").ToList().ForEach(item => { item.Action = "Skipped"; item.Order = 4; item.Note = "Item doesn't exist in Item Rev."; });
                        logger.Info("CreateBOMHeader = Yes | ItemNumber | env | site: " + itemNumber + " | " + environment + " | " + site);
                    }
                    else
                    {
                        Session["CreateBOMHeader"] = "No"; // No need to call BOM header creation. 

                        logger.Info("CreateBOMHeader = No | ItemNumber | env | site: " + itemNumber + " | " + environment + " | " + site);
                        //If all components are available, the WebApp will check to see if the BOM exists in XA for the given parent.
                        //If not, it will create a new BOM. If yes, it will modify existing BOM to match what is in the form. - Shaji 

                        // Child Item Revision check required in comparision Due to revision data coming from PDM data.  
                        // New BOM list - Component Item number exist in PDM but not in XA BOM component. (Note: Item exists in ITMRVA). So add record in XA BOM. 
                        var NewBOM = PDMbomComponentList.Except((from pdmbom in PDMbomComponentList
                                                                 join xabom in XAbomComponentList
                                                                 on new { a = pdmbom.UserSequence, c = pdmbom.ComponentItemNumber.ToUpper() } 
                                                                 equals new { a = xabom.UserSequence, c = xabom.ComponentItemNumber.ToUpper() } 
                                                                 // on pdmbom.ComponentItemNumber equals xabom.ComponentItemNumber // PREV
                                                                 select pdmbom).ToList());
                        NewBOMList = NewBOM.ToList();

                        var BalloonChangeBOM = from xabom in XAbomComponentList
                                               join pdmbom in PDMbomComponentList
                                               // on new { c = xabom.ComponentItemNumber } equals new { c = pdmbom.ComponentItemNumber  } // PREV
                                               on new { c = xabom.ComponentItemNumber.ToUpper() } 
                                               equals new { c = pdmbom.ComponentItemNumber.ToUpper() }
                                               where pdmbom.UserSequence != xabom.UserSequence
                                               select new Item
                                               {
                                                   MasterCode = xabom.MasterCode,
                                                   ItemNumber = xabom.ItemNumber,
                                                   UserSequence = xabom.UserSequence, //pdmbom.UserSequence,
                                                   ComponentItemNumber = xabom.ComponentItemNumber.ToUpper(),
                                                   ComponentDescription = xabom.ComponentDescription,
                                                   ExtendedDescrption1 = xabom.ExtendedDescrption1,
                                                   ExtendedDescrption2 = xabom.ExtendedDescrption2,
                                                   CompleteComponentDescription = xabom.CompleteComponentDescription,
                                                   Qty = pdmbom.Qty,
                                                   Revision = xabom.Revision,
                                                   UnitOfMeasure = xabom.UnitOfMeasure,
                                                   ItemType = xabom.ItemType,
                                                   Site = xabom.Site,
                                                   ParentItemNumber = xabom.ParentItemNumber,
                                                   ParentItemRevision = xabom.ParentItemRevision,
                                                   AlternateBOMId = xabom.AlternateBOMId,
                                                   ComponentItem = xabom.ComponentItem,
                                                   ComponentItemRevision = xabom.ComponentItemRevision,
                                                   Environment = xabom.Environment,
                                                   Flag = xabom.Flag,
                                                   Action = xabom.Action,
                                                   Note = xabom.Note,
                                                   Order = xabom.Order,
                                                   // Set IsBalloonChanged based on the compare condition
                                                   IsBalloonChanged = pdmbom.UserSequence != xabom.UserSequence ? 1 : 0,
                                                   IsQuantityChanged = pdmbom.Qty != xabom.Qty ? 1 : 0
                                               };

                        BallonChangeBOMList = BalloonChangeBOM.ToList();

                        // Delete BOM list - Component exists in XA BOM but not in PDM. (Note: PDM is the source of truth). So delete record from XA BOM.
                        var DeleteBOM = XAbomComponentList.Except((from xabom in XAbomComponentList
                                                                   join pdmbom in PDMbomComponentList
                                                                   // on xabom.ComponentItemNumber equals pdmbom.ComponentItemNumber // PREV
                                                                   on new { a = xabom.UserSequence, c = xabom.ComponentItemNumber.ToUpper() } 
                                                                   equals new { a = pdmbom.UserSequence, c = pdmbom.ComponentItemNumber.ToUpper() }
                                                                   select xabom).ToList());
                        //on xabom.ComponentItemNumber equals pdmbom.ComponentItemNumber
                        DeleteBOMList = DeleteBOM.ToList();

                        // No Change BOM List - There is no change in data between PDM and XA BOM. (No action is required)
                        var NoChangeBOM = from xabom in XAbomComponentList
                                          join pdmbom in PDMbomComponentList
                                          on new { a = xabom.UserSequence, b = xabom.Qty, c = xabom.ComponentItemNumber.ToUpper() } 
                                          equals new { a = pdmbom.UserSequence, b = pdmbom.Qty, c = pdmbom.ComponentItemNumber.ToUpper() }
                                          //on new { a = pdmbom.UserSequence, b = pdmbom.Qty, c = pdmbom.ComponentItemNumber } equals new { a = xabom.UserSequence, b = xabom.Qty, c = xabom.ComponentItemNumber }
                                          select xabom; //pdmbom
                        NoChangeBOMList = NoChangeBOM.ToList();

                        //var ChangeBOM = from pdmbom in PDMbomComponentList
                        //                join xabom in XAbomComponentList

                        var ChangeBOM = from xabom in XAbomComponentList
                                        join pdmbom in PDMbomComponentList
                                        // on new { c = xabom.ComponentItemNumber } equals new { c = pdmbom.ComponentItemNumber  } // PREV
                                        on new { c = xabom.ComponentItemNumber.ToUpper(), s = xabom.UserSequence } 
                                        equals new { c = pdmbom.ComponentItemNumber.ToUpper(), s = pdmbom.UserSequence }
                                        where pdmbom.UserSequence != xabom.UserSequence || pdmbom.Qty != xabom.Qty
                                        select new Item
                                        {
                                            MasterCode = xabom.MasterCode,
                                            ItemNumber = xabom.ItemNumber,
                                            UserSequence = xabom.UserSequence, //pdmbom.UserSequence,
                                            ComponentItemNumber = xabom.ComponentItemNumber.ToUpper(),
                                            ComponentDescription = xabom.ComponentDescription,
                                            ExtendedDescrption1 = xabom.ExtendedDescrption1,
                                            ExtendedDescrption2 = xabom.ExtendedDescrption2,
                                            CompleteComponentDescription = xabom.CompleteComponentDescription,
                                            Qty = pdmbom.Qty,
                                            Revision = xabom.Revision,
                                            UnitOfMeasure = xabom.UnitOfMeasure,
                                            ItemType = xabom.ItemType,
                                            Site = xabom.Site,
                                            ParentItemNumber = xabom.ParentItemNumber,
                                            ParentItemRevision = xabom.ParentItemRevision,
                                            AlternateBOMId = xabom.AlternateBOMId,
                                            ComponentItem = xabom.ComponentItem,
                                            ComponentItemRevision = xabom.ComponentItemRevision,
                                            Environment = xabom.Environment,
                                            Flag = xabom.Flag,
                                            Action = xabom.Action,
                                            Note = xabom.Note,
                                            Order = xabom.Order,
                                            // Set IsBalloonChanged based on the compare condition
                                            IsBalloonChanged = pdmbom.UserSequence != xabom.UserSequence ? 1 : 0,
                                            IsQuantityChanged = pdmbom.Qty != xabom.Qty ? 1 : 0
                                        };

                        //select xabom ;  select pdmbom;
                        // { a = pdmbom.UserSequence, b = pdmbom.Qty } equals new { a = xabom.UserSequence, b = xabom.Qty }
                        ChangeBOMList = ChangeBOM.ToList();
                        SkippedBOMList = NewBOMList.ToList();

                        ChangeBOMList.ForEach(item => { item.Action = "Change"; item.Order = 2; item.Note = "Qty was different"; }); // Set Action to Change if item is same but qty or user sequence is different in PDM or XA BOM.

                        // SET item.ParentItemRevision = strParentItemRevision for NEW item  
                        NewBOMList.Where(item => item.Flag == "1").ToList().ForEach(item => 
                            { item.Action = "Add"; item.Order = 1; item.Note = "Is not in XA BOM"; item.ParentItemRevision = strParentItemRevision; });  // Set Action to Add if new item and the item exists in item revision file.
                        SkippedBOMList.Where(item => item.Flag == "0").ToList().ForEach(item => { item.Action = "Skipped"; item.Order = 4; item.Note = "Item doesn't exist in XA"; }); // Set Action to Skipped if it is a new item but does not exist in item revision file.
                        DeleteBOMList.ForEach(item => { item.Action = "Delete"; item.Order = 3; item.Note = "Item is not in SolidWorks BOM, but it is in XA BOM."; });// Set Action to Delete if item exists in XA BOM but not in PDM data.
                        NoChangeBOMList.ForEach(item => { item.Action = "No Change"; item.Order = 5; item.Note = "Same in both SolidWorks (SW) and XA"; }); // Set Action to No Change if there is no change to item usersequence or qty in PDM and XA BOM.

                        BallonChangeBOMList.ForEach(item => { item.Action = "BallonChange"; item.Order = 6; item.Note = "Balloon # differs, but ITNBR and QTY match"; }); // OLD Note: Balloon does not exist in XA

                        foreach (Item balloonBOMitem in BallonChangeBOMList)
                        {
                            foreach (Item newBOMitem in NewBOMList)
                            {
                                // balloon change only 
                                if (newBOMitem.ComponentItemNumber.ToUpper() == balloonBOMitem.ComponentItemNumber.ToUpper())
                                    newBOMitem.Note = "Balloon # differs, but ITNBR and QTY match";

                                foreach (Item tmpDELitem in DeleteBOMList)
                                {
                                    // balloon change and the quantity change // Highlight both balloon and Qty changes 
                                    if (tmpDELitem.ComponentItemNumber.ToUpper() == newBOMitem.ComponentItemNumber.ToUpper()
                                       && tmpDELitem.Qty != newBOMitem.Qty)
                                        newBOMitem.Note = "Balloon # change and the Quantity change";
                                }
                            }
                        }             

                        PDMbomComponentList.Clear(); // Clear data in PDM NOM List.
                        // Concat all list items and take distinct and set sort by Order.
                        PDMbomComponentList = NewBOMList.Concat(ChangeBOMList).Concat(DeleteBOMList).Concat(SkippedBOMList).Concat(NoChangeBOMList).Distinct().OrderBy(item => item.Order).ToList();
                    }
                }

                //else { TempData["nodatamessage"] = "ParentItem not in SQLDB."; } 
                if (!isValidXAParentItem)
                { PDMbomComponentList.Clear(); }

                // Show the final list in jqgrid.

                var Results = PDMbomComponentList.Select(
                    itemData => new
                    {
                        itemData.MasterCode,
                        itemData.ItemNumber,
                        itemData.UserSequence,
                        itemData.ComponentItemNumber,
                        itemData.ComponentDescription,
                        itemData.ExtendedDescrption1,
                        itemData.ExtendedDescrption2,
                        itemData.CompleteComponentDescription,
                        itemData.Qty,
                        itemData.Revision,
                        itemData.UnitOfMeasure,
                        itemData.ItemType,
                        itemData.Site,
                        itemData.ParentItemNumber,
                        itemData.ParentItemRevision,
                        itemData.AlternateBOMId,
                        itemData.ComponentItem,
                        itemData.ComponentItemRevision,
                        itemData.Environment,
                        itemData.Flag,
                        itemData.Action,
                        itemData.Note,
                        itemData.Order,
                        itemData.IsBalloonChanged,
                        itemData.IsQuantityChanged
                    });

                // Set session variables.
                Session["PDMBOMData"] = PDMbomComponentList;
                Session["PDMSite"] = (from item in PDMbomComponentList select item.Site).FirstOrDefault();
                Session["PDMParentItemNumber"] = (from item in PDMbomComponentList select item.ParentItemNumber).FirstOrDefault();
                // //Session["PDMParentItemRevision"] = (from item in PDMbomComponentList select item.ParentItemRevision).FirstOrDefault();
                Session["PDMEnvironment"] = environment;

                Session["Environment"] = environment;
                Session["Site"] = (from item in PDMbomComponentList select item.Site).FirstOrDefault();
                Session["ParentItemNumber"] = (from item in PDMbomComponentList select item.ParentItemNumber).FirstOrDefault();
                Session["ParentItemRevision"] = strParentItemRevision; // Collected from XA  
                 
                logger.Info("ListComparedComponentsResultByPDMandXAData|Retrieved PDM data from SQL End.");

                int pageIndex = Convert.ToInt32(page) - 1;
                int pageSize = rows;
                int totalRecords = Results.Count();
                var totalPages = (int)Math.Ceiling((float)totalRecords / (float)rows);

                var jsonData = new
                {
                    total = totalPages,
                    page,
                    records = totalRecords,
                    rows = Results
                };

                // Call Logout XA session 2
                if (Session["XASlrSession"] != null)
                    objModel.LogoutXASyslinkSessionHandle(Session["XASlrSession"].ToString());

                return Json(jsonData, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                logger.Error(DateTime.Now.ToString() + ex.Message + System.Environment.NewLine);
                logger.Info("ListComparedComponentsResultByPDMandXAData|loggedinUser: " + loggedinUser + " |LoggedinUsereMail: " + loggedinUserEmail); 
                SendEmailAlertNotificationBOMUploadtoXASubject("Upload BOM To XA | ListComparedComponentsResultByPDMandXAData - Systemlink service call Error: " + ex.Message, loggedinUserEmail, itemNumber, environment);
                return Json(new { success = false, message = "Failed to retrieve BOM components from SQL temporary table !" }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Update the BOM component.
        /// </summary>
        /// <param name="componentDetail">The row that is being modified.</param>
        /// <returns>Json result of the update component.</returns>
        [HttpPost]
        public JsonResult UpdateBOMComponent(Item componentDetail)
        {
            string errorMessage = string.Empty;
            string site, parentItem, parentItemRevision, alternateBOMId, componentItem, componentItemRevision, token, usersequence, environment, quantityPerUnit;
            site = parentItem = parentItemRevision = alternateBOMId = componentItem = componentItemRevision = token = usersequence = environment = quantityPerUnit = string.Empty;
            string updateResult = string.Empty;
            string loggedinUser = string.Empty;

            try
            {
                logger.Info("Updating a BOM component begins... ");

                site = componentDetail.Site;
                parentItem = componentDetail.ParentItemNumber;
                parentItemRevision = componentDetail.ParentItemRevision;
                alternateBOMId = componentDetail.AlternateBOMId;
                componentItem = componentDetail.ComponentItem;
                componentItemRevision = componentDetail.ComponentItemRevision;
                token = componentDetail.MasterCode;
                usersequence = componentDetail.UserSequence;
                quantityPerUnit = componentDetail.Qty;
                environment = componentDetail.Environment;

                loggedinUser = HttpContext.User.Identity.Name.ToString().Split('\\')[1].ToUpper();
                logger.Info("The user initiated the Modify BOM component : " + loggedinUser + Environment.NewLine);

                BOMUploadMasterModel clsXA = new BOMUploadMasterModel();
                updateResult = clsXA.UpdateBOMComponent(token, site, parentItem, parentItemRevision, alternateBOMId, componentItem, componentItemRevision, usersequence, quantityPerUnit, environment, loggedinUser);
                //HelperGeneral.SaveUserEnvInCookie(Response, "N", environment);  //BASE URL 
            }
            catch (Exception ex)
            {
                logger.Error(DateTime.Now.ToString() + ex.Message + System.Environment.NewLine);
                errorMessage = "Error :" + ex.Message;
                return Json(errorMessage, JsonRequestBehavior.AllowGet);
            }
            return Json(errorMessage, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// Delete the BOM component.
        /// </summary>
        /// <param name="id">Unique identifier of the component being deleted.</param>
        /// <returns>Json result of the delete component.</returns>
        [HttpPost]
        public JsonResult DeleteBOMComponent(string id)
        {
            string errorMessage = string.Empty;
            string site, parentItem, parentItemRevision, alternateBOMId, componentItem, componentItemRevision, token, environment, userSequence, qty;
            site = parentItem = parentItemRevision = alternateBOMId = componentItem = componentItemRevision = token = environment = userSequence = qty = string.Empty;
            string deleteResult = string.Empty;
            string loggedinUser = string.Empty;
            IList<Item> ItemList = new List<Item>();
            IList<Item> componentDetail = new List<Item>();

            try
            {
                logger.Info("Deleteing a BOM component " + id + " begins... ");

                if (Session["BOMData"] != null)
                {
                    ItemList = (List<Item>)Session["BOMData"];
                    componentDetail = ItemList.Where(u => u.MasterCode == id).ToList();
                    foreach (var item in componentDetail)
                    {
                        site = item.Site;
                        parentItem = item.ParentItemNumber;
                        parentItemRevision = item.ParentItemRevision;
                        alternateBOMId = item.AlternateBOMId;
                        componentItem = item.ComponentItem;
                        componentItemRevision = item.ComponentItemRevision;
                        userSequence = item.UserSequence;
                        qty = item.Qty;
                        token = item.MasterCode;
                        environment = item.Environment;
                    }
                }
                loggedinUser = HttpContext.User.Identity.Name.ToString().Split('\\')[1].ToUpper();
                logger.Info("The user initiated the Delete BOM component : " + loggedinUser + Environment.NewLine);
                BOMUploadMasterModel clsXA = new BOMUploadMasterModel();
                deleteResult = clsXA.DeleteBOMComponent(token, site, parentItem, parentItemRevision, alternateBOMId, componentItem, componentItemRevision, userSequence, qty, environment, loggedinUser);
                //HelperGeneral.SaveUserEnvInCookie(Response, "N", environment);  //BASE URL 
                logger.Info("Deleted the BOM component " + id);
            }
            catch (Exception ex)
            {
                logger.Error(DateTime.Now.ToString() + ex.Message + System.Environment.NewLine);
                errorMessage = "Failed to delete the BOM component : " + id + Environment.NewLine + ex.Message;
                return Json(errorMessage, JsonRequestBehavior.AllowGet);
            }
            return Json(errorMessage, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// Create a new BOM component.
        /// </summary>
        /// <param name="componentDetail">The new row that is being aded.</param>
        /// <returns>Json result of the add new component.</returns>
        public JsonResult CreateBOMComponent(Item componentDetail)
        {
            string errorMessage = string.Empty;
            string site, parentItem, parentItemRevision, alternateBOMId, componentItem, componentItemRevision, token, usersequence, environment, quantityPerUnit;
            site = parentItem = parentItemRevision = alternateBOMId = componentItem = componentItemRevision = token = usersequence = environment = quantityPerUnit = string.Empty;
            string createResult = string.Empty;
            bool isValidItem = false;
            string loggedinUser = string.Empty;
            string strUoM1 = "";
            string strITTYP1 = "";
            string strItmDes1 = "";
            string strCompParentItemRevision = "";
            try
            {
                logger.Info("Creating a BOM component begins... ");

                if (Session["Environment"] != null) environment = Convert.ToString(Session["Environment"]);
                componentItem = componentDetail.ComponentItemNumber;

                if (Session["Site"] != null) site = Convert.ToString(Session["Site"]);
                if (Session["ParentItemNumber"] != null) parentItem = Convert.ToString(Session["ParentItemNumber"]);
                if (Session["ParentItemRevision"] != null) parentItemRevision = Convert.ToString(Session["ParentItemRevision"]);

                // Check if the new item being added exists in XA.
                isValidItem = ValidateItemInXA(componentItem, environment, site, out strCompParentItemRevision, out strUoM1, out strITTYP1, out strItmDes1);

                // Proceed with creating the BOM component only when the item exists in XA.
                if (isValidItem)
                {
                    logger.Info("Component being added is a valid item : " + componentItem + Environment.NewLine);

                    alternateBOMId = componentDetail.AlternateBOMId;
                    componentItemRevision = componentDetail.Revision;

                    usersequence = componentDetail.UserSequence;
                    quantityPerUnit = componentDetail.Qty;

                    loggedinUser = HttpContext.User.Identity.Name.ToString().Split('\\')[1].ToUpper();
                    logger.Info("The user initiating Create BOM component : " + loggedinUser + Environment.NewLine);

                    BOMUploadMasterModel clsXA = new BOMUploadMasterModel();
                    createResult = clsXA.CreateBOMComponent(site, parentItem, parentItemRevision, alternateBOMId, componentItem, strCompParentItemRevision, usersequence, quantityPerUnit, environment, loggedinUser);
                    //HelperGeneral.SaveUserEnvInCookie(Response, "N", environment);  //BASE URL 
                }
                else
                {
                    logger.Info("Component " + componentItem + " does not exist in XA " + Environment.NewLine);
                    errorMessage = "Item " + componentItem + " does not exist in XA. Create the item using New Item Creation (NIC) tool and upload the BOM!";
                    throw new HttpException(404, errorMessage);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Create BOM component failed : " + ex.Message + System.Environment.NewLine);
                errorMessage = "Error :" + ex.Message;
                return Json(errorMessage, JsonRequestBehavior.AllowGet);
            }
            return Json(errorMessage, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// Create a BOM Header while create new BOM component from BASE URL to load BOM from XA.
        /// </summary>
        /// <param name="componentDetail">The new row that is being aded.</param>
        /// <returns>Json result of the add new component.</returns>
        public JsonResult CreateBOMHeaderBOMComponent(Item componentDetail)
        {
            string errorMessage = string.Empty;
            string site, parentItem, parentItemRevision, alternateBOMId, componentItem, componentItemDesc, componentItemRevision, token, usersequence, environment, quantityPerUnit;
            site = parentItem = parentItemRevision = alternateBOMId = componentItem = componentItemRevision = token = usersequence = environment = quantityPerUnit = string.Empty;
            string createResult = string.Empty;
            bool isValidItem = false;
            string loggedinUser = string.Empty;
            string strUoM1 = "";
            string strITTYP1 = "";
            string strItmDes1 = "";
            string strCompParentItemRevision = "", strItemDescription = "";
            List<Item> XAbomComponentList = new List<Item>();
            BOMUploadMasterModel clsXA = new BOMUploadMasterModel();
            System.Text.StringBuilder logEmailBody = new StringBuilder();
            bool boolSendEmailNotification = false;
            string strheaderCreationResult = string.Empty;
            string successMessage = string.Empty;

            try
            {
                logger.Info("Creating a CreateBOMHeaderBOMComponent begins... ");
                loggedinUser = HttpContext.User.Identity.Name.ToString().Split('\\')[1].ToUpper();
                if (Session["Environment"] != null) environment = Convert.ToString(Session["Environment"]);
                componentItem = componentDetail.ComponentItemNumber;
                componentItemDesc = componentDetail.ComponentDescription;

                if (Session["Site"] != null) site = Convert.ToString(Session["Site"]);
                if (Session["ParentItemNumber"] != null) parentItem = Convert.ToString(Session["ParentItemNumber"]);
                if (Session["ParentItemRevision"] != null) parentItemRevision = Convert.ToString(Session["ParentItemRevision"]);
                if (System.Web.HttpContext.Current.Application["ItemDescription"] != null)
                { strItemDescription = System.Web.HttpContext.Current.Application["ItemDescription"].ToString(); }
                if (Session["BOMData"] != null)
                { XAbomComponentList = (List<Item>)Session["BOMData"]; }

                logEmailBody.Append("This to notify that below issues are captured while creating BOM for Parent Item number: " + parentItem + " on " + DateTime.Now + ". <br/><br/>");
                logEmailBody.Append("<table border=1>");
                logEmailBody.Append("<tr bgcolor='yellow'><th>ParentItemNo</th><th>Com.ItemNumber</th><th>Action</th><th>Env. </th><th>Site</th><th>End User</th><th>Error log</th></tr>");
                logEmailBody.Append("");

                //if no BOM exist for parent
                if (XAbomComponentList.Count == 0)
                {
                    logger.Info("GetAllSingleLevelBOM XA BOM components begins ");
                    // Get all BOM components from XA for the parent item.
                    XAbomComponentList = clsXA.GetAllSingleLevelBOM(parentItem, environment, site, loggedinUser, "N"); //from BASE URL 
                    logger.Info("GetAllSingleLevelBOM XA BOM components ends | XAbomComponentList.Count: " + XAbomComponentList.Count.ToString());

                    // If there is no record in XA BOM component list, then all records in SQL PDMData will be added to XA BOM. Note: only valid items.
                    if (XAbomComponentList.Count == 0)
                    {
                        logger.Info("The user initiating Create BOM Header: " + loggedinUser + " | Env: " + environment + " | Site: " + site + Environment.NewLine);

                        strheaderCreationResult = clsXA.CreateBOMHeader(site, parentItem, strItemDescription, parentItemRevision, environment, loggedinUser);
                        //Capture Error while creating BOMHeader 
                        if (strheaderCreationResult.Contains("Error"))
                        {
                            boolSendEmailNotification = true;
                            errorMessage = "CreateError messages were generated during Create BOM Header - Item: " + parentItem;
                            logger.Info(errorMessage + "| Error message: " + strheaderCreationResult + Environment.NewLine);
                            logEmailBody.Append("<tr><td>" + parentItem + "</td>");
                            logEmailBody.Append("<td></td>");
                            logEmailBody.Append("<td>Create BOM Header</td>");
                            logEmailBody.Append("<td>" + environment.Substring(0, 2) + " (" + environment + ") </td>");
                            logEmailBody.Append("<td>" + site + "</td>");
                            logEmailBody.Append("<td>" + loggedinUser + "</td>");
                            logEmailBody.Append("<td>" + strheaderCreationResult + "</td></tr>");
                        }
                        else
                        {
                            logger.Info("CreateBOMHeader: " + parentItem + " created in XA | Env: " + environment + " | Site: " + site);
                        }
                        logger.Info("CreateBOMHeaderBOMComponent | CreateBOMHeader response | " + strheaderCreationResult);
                    }
                }

                // Check if the new item being added exists in XA.
                isValidItem = ValidateItemInXA(componentItem, environment, site, out strCompParentItemRevision, out strUoM1, out strITTYP1, out strItmDes1);

                // Proceed with creating the BOM component only when the item exists in XA.
                if (isValidItem)
                {
                    logger.Info("Component being added is a valid item : " + componentItem + Environment.NewLine);

                    alternateBOMId = componentDetail.AlternateBOMId;
                    componentItemRevision = componentDetail.Revision;

                    usersequence = componentDetail.UserSequence;
                    quantityPerUnit = componentDetail.Qty;

                    logger.Info("The user initiating Create BOM component : " + loggedinUser + Environment.NewLine);

                    createResult = clsXA.CreateBOMComponent(site, parentItem, parentItemRevision, alternateBOMId, componentItem, strCompParentItemRevision, usersequence, quantityPerUnit, environment, loggedinUser);
                    if (createResult.Contains("Error") || createResult.Contains("CreateError"))
                    {
                        if (createResult.Contains("no product structure"))
                            errorMessage = "CreateError messages were generated during Create BOM component: " + componentItem + " | You are attempting to add a product structure detail (PSTDTL) record, but there is no product structure header (PSTHDR) record for the parent structure ID..";
                        else
                            errorMessage = "CreateError messages were generated during Create BOM component: " + componentItem;

                        logger.Info(errorMessage + Environment.NewLine);
                        boolSendEmailNotification = true;
                        logEmailBody.Append("<tr><td>" + parentItem + "</td>");
                        logEmailBody.Append("<td>" + componentItem + "</td>");
                        logEmailBody.Append("<td>Create BOM Component</td>");
                        logEmailBody.Append("<td>" + environment.Substring(0, 2) + " (" + environment + ") </td>");
                        logEmailBody.Append("<td>" + site + "</td>");
                        logEmailBody.Append("<td>" + loggedinUser + "</td>");
                        logEmailBody.Append("<td>" + createResult + "</td></tr>");
                    }
                }
                else
                {
                    boolSendEmailNotification = true;
                    logger.Info("Component Item" + componentItem + " does not exist in XA " + Environment.NewLine);
                    if (strheaderCreationResult.Contains("Error"))
                    {
                        errorMessage = "Error messages were generated during Create BOM Header - Item: " + parentItem + " | ";
                        errorMessage = errorMessage + " Item " + componentItem + " does not exist in XA. Create the item using New Item Creation (NIC) tool and upload the BOM!";
                    }
                    else
                        errorMessage = "Item " + componentItem + " does not exist in XA. Create the item using New Item Creation (NIC) tool and upload the BOM!";

                    throw new HttpException(404, errorMessage);
                }

                if (boolSendEmailNotification)
                {
                    logEmailBody.Append("</table>"); // <br/><br/> Regards, <br/>XA BOM Synchronization Master.
                    // // SendEmailAlertNotificationBOMUploadtoXA(logEmailBody.ToString()); //EmailSubject  
                    if (loggedinUserEmail.Trim() == "") loggedinUserEmail = GetEmailForLoggedInADUser(loggedinUser); 
                    logger.Info("CreateBOMHeaderBOMComponent|loggedinUser: " + loggedinUser + " |LoggedinUsereMail: " + loggedinUserEmail);
                    SendEmailAlertNotificationBOMUploadtoXASubject(logEmailBody.ToString(), loggedinUserEmail, parentItem, environment);
                    errorMessage = strheaderCreationResult + Environment.NewLine + errorMessage + Environment.NewLine;
                    throw new HttpException(404, errorMessage);
                }
                else
                {
                    logEmailBody.Clear(); 
                }

                XAbomComponentList = null;
                logger.Info("Creating a CreateBOMHeaderBOMComponent End. ");

            }
            catch (Exception ex)
            {
                logger.Error("CreateBOMHeaderBOMComponent failed : " + ex.Message + System.Environment.NewLine);
                errorMessage = "Error :" + ex.Message;
                return Json(errorMessage, JsonRequestBehavior.AllowGet);
            }
            return Json(errorMessage, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// Validate whether item exists in XA in the chosen environment.
        /// </summary>          
        /// <param name="itemNumber">Item being checked for existence in XA.</param>
        /// <param name="environment">Environment in which item is being validated.</param
        /// <param name="site">Site in which item is being validated.</param>
        /// <returns>true if item exists else false.</returns>
        public bool ValidateItemInXA(string itemNumber, string environment, string site, out string strParentItemRevision, out string strUoM, out string strITTYP, out string strItmDes)
        {
            bool validItem = false;
            string errorMessage = string.Empty;
            BOMUploadMasterModel clsXA = new BOMUploadMasterModel();
            IList<Item> ItemList = new List<Item>();
            //string strUoM1 = "";
            //string strITTYP1 = "";

            logger.Info("Validate Item Existence while adding a new item  " + itemNumber + " in " + environment + " environment, Site: " + site);

            try
            {
                ItemList = clsXA.GetItemDetails(itemNumber, environment, site, loggedinUser, out strParentItemRevision, out strUoM, out strITTYP, out strItmDes);

                if (ItemList != null)
                {
                    if (ItemList.Count > 0 && !string.IsNullOrEmpty(ItemList[0].ItemNumber))
                    {
                        validItem = true;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("Check for Item Existence in XA failed : " + ex.Message + System.Environment.NewLine);
                errorMessage = "Error :" + ex.Message;
                throw;
            }

            logger.Info("Check for Item Existence ends...");

            return validItem;
        }

        public bool ValidateItemInXARevision(string itemNumber, string environment, string site, string strParentItemRevision, out string strUoM, out string strITTYP, out string strItmDes)
        {
            bool validItem = false;
            string errorMessage = string.Empty;
            BOMUploadMasterModel clsXA = new BOMUploadMasterModel();
            IList<Item> ItemList = new List<Item>();

            logger.Info("Validate Item Existence while adding a new item  " + itemNumber + " in " + environment + " environment, Site: " + site);

            try
            {
                ItemList = clsXA.GetItemDetailsByRevision(itemNumber, environment, site, loggedinUser, strParentItemRevision, out strUoM, out strITTYP, out strItmDes);

                if (ItemList != null)
                {
                    if (ItemList.Count > 0 && !string.IsNullOrEmpty(ItemList[0].ItemNumber))
                    {
                        validItem = true;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("Check for Item Existence in XA failed : " + ex.Message + System.Environment.NewLine);
                errorMessage = "Error :" + ex.Message;
                throw;
            }

            logger.Info("Check for Item Existence ends...");

            return validItem;
        }

        public async Task MakeDelayInProcessAsync(string seconds)
        {
            logger.Info("MakeDelayInProcessAsync | Making delay in process for " + seconds + " milliseconds to avoid hitting XA performance issue. ");
            await Task.Delay(Convert.ToInt32(seconds)); 
            logger.Info("MakeDelayInProcessAsync | Delay in process completed. ");
        }

        /// <summary>
        /// Initiate the call to upload BOM to XA based on some actions.
        /// </summary>
        /// <returns>Success or failure result.</returns>
        [HttpPost]
        [ActionName("UploadBomToXA")]
        public ActionResult UploadBomToXA()
        {
            string successMessage = "Successfully Uploaded all BOM to XA.";
            string createResult = string.Empty;
            string updateResult = string.Empty;
            string deleteResult = string.Empty;
            string itemDescription = string.Empty;
            string headerCreationResult = string.Empty;
            string site = string.Empty;
            string parentItemNumber = string.Empty;
            string environment = string.Empty;
            string createHeader = string.Empty;
            TempData["BOMUploadSuccessMsg"] = "";
            TempData["BOMUploadtoXAFailureMsg"] = "";
            System.Text.StringBuilder logEmailBody = new StringBuilder();
            bool boolSendEmailNotification = false;
            bool IsDontDelete = false;
            string parentItemRevisionNum = string.Empty;
            int intXABOMHeaderCnt = 0; 

            loggedinUser = HttpContext.User.Identity.Name.ToString().Split('\\')[1].ToUpper();
            if (loggedinUserEmail.Trim() == "") loggedinUserEmail = GetEmailForLoggedInADUser(loggedinUser);
            logger.Info("The user initiating Upload bulk BOM to XA : " + loggedinUser + Environment.NewLine);
            logger.Info("UploadBomToXA|loggedinUser: " + loggedinUser + " |LoggedinUsereMail: " + loggedinUserEmail); 

            List<Item> componentDetailList = new List<Item>();
            BOMUploadMasterModel clsXA = new BOMUploadMasterModel();

            try
            {
                logger.Info("UploadBomToXA|xaSystemName: " + clsXA.GetxaSystemName);
                //if (Session["ItemDescription"] != null) { itemDescription = Session["ItemDescription"].ToString(); }
                if (System.Web.HttpContext.Current.Application["ItemDescription"] != null)
                { itemDescription = System.Web.HttpContext.Current.Application["ItemDescription"].ToString(); }
                if (Session["PDMBOMData"] != null) { componentDetailList = (List<Item>)Session["PDMBOMData"]; }
                if (Session["PDMSite"] != null) { site = Session["PDMSite"].ToString(); }
                if (Session["PDMParentItemNumber"] != null) { parentItemNumber = Session["PDMParentItemNumber"].ToString().Trim(); }
                if (Session["PDMEnvironment"] != null) { environment = Session["PDMEnvironment"].ToString(); }  // MUST NEET IT 
                if (Session["CreateBOMHeader"] != null) { createHeader = Session["CreateBOMHeader"].ToString(); }

                // Collected from XA using Get Single Level BOM  
                if (Session["ParentItemRevision"] != null) { parentItemRevisionNum = Session["ParentItemRevision"].ToString(); } 

                var ENVCookiePDM = Request.Cookies["EnvironmentCookiePDM"];
                if (!string.IsNullOrEmpty(ENVCookiePDM["EnvironmentPDM"]))
                {
                    environment = ENVCookiePDM.Value.Replace("EnvironmentPDM=", string.Empty);
                    BOMUploadMasterModel objSite = new BOMUploadMasterModel();
                    site = objSite.GetSiteByEnvironment(environment);
                }
                else
                {
                    logger.Info(">> UploadBomToXA >> Environment (not from cookie): " + environment + Environment.NewLine); 
                    BOMUploadMasterModel objSite = new BOMUploadMasterModel();
                    site = objSite.GetSiteByEnvironment(environment);   
                }

                logEmailBody.Append("This to notify that below issues are captured while BOM Upload to XA for Parent Item number: " + parentItemNumber + " on " + DateTime.Now + " from XA-System: " + clsXA.GetxaSystemName + ". <br/><br/>");
                logEmailBody.Append("<table border=1>");
                logEmailBody.Append("<tr bgcolor='yellow'><th>ParentItemNo</th><th>Com.ItemNumber</th><th>Action</th><th>Env. </th><th>Site</th><th>End User</th><th>Error log</th></tr>");
                logEmailBody.Append("");

                // Initiate BOM Header creation if the flag on createHeader is set to Yes.
                if (createHeader == "Yes")
                {
                    intXABOMHeaderCnt = clsXA.XAHDRCountExecuteScalar(SELECT_PSTHDR, site, environment, parentItemNumber, parentItemRevisionNum);

                    logger.Info(">> UploadBomToXA >> XAHDRCountExecuteScalar from XA to ensure BOM HEADER exists or not | ParentItemNumber | env | site: " + parentItemNumber + " | " + environment + " | " + site + " | BOM Header Count: " + intXABOMHeaderCnt.ToString() + Environment.NewLine);

                    // If there is no HEADER record in XA BOM component list, ADD NEW BOM Header.
                    if (intXABOMHeaderCnt == 0)
                    {
                        headerCreationResult = clsXA.CreateBOMHeader(site, parentItemNumber, itemDescription, parentItemRevisionNum, environment, loggedinUser);    // + parentItemRevisionNum

                        // SKIP this error[Shaji]: A product structure header (PSTHDR) record already exists with the same number as the one you entered
                        //Capture Error while creating BOMHeader 
                        if (headerCreationResult.Contains("Error") && headerCreationResult.Contains("header (PSTHDR) record already exists"))
                        {
                            boolSendEmailNotification = true;

                            successMessage = "Error in creating new BOM header | BOM upload to XA failed. Please verify the details in the email.";

                            logEmailBody.Append("<tr><td>" + parentItemNumber + "</td>");
                            logEmailBody.Append("<td></td>");
                            logEmailBody.Append("<td>Create BOM Header</td>");
                            logEmailBody.Append("<td>" + environment.Substring(0, 2) + " (" + environment + ") </td>");
                            logEmailBody.Append("<td>" + site + "</td>");
                            logEmailBody.Append("<td>" + loggedinUser + "</td>");
                            logEmailBody.Append("<td>" + headerCreationResult + "</td></tr>");
                            logger.Error("UpLoadBomToXA() | CreateBOMHeader response | " + headerCreationResult);
                        }
                        else
                        {
                            intXABOMHeaderCnt = 1; 
                            logger.Info("CreateBOMHeader: " + parentItemNumber + " created in XA | Env: " + environment + " | Site: " + site + " | parentItemRevisionNum: " + parentItemRevisionNum);
                            logger.Info("UpdloadBomToXA() | CreateBOMHeader response | " + headerCreationResult); 
                        }

                    }
                    else
                    {
                        logger.Info("CreateBOMHeader: " + parentItemNumber + " BOM Header already exists in XA | Env: " + environment + " | Site: " + site + " | parentItemRevisionNum: " + parentItemRevisionNum);
                    }
                }
                else
                    intXABOMHeaderCnt = 1; // BOM Header is already exists in XA. 

                int intReturn = 0; 
                // // // Process Component if BOM header exists in XA // // // 
                logger.Info(">> UploadBomToXA >> componentDetailList from ListComparedComponentsResultByPDMandXAData - COUNT: " + componentDetailList.Count.ToString() + Environment.NewLine);  
                if (componentDetailList.Count > 0 && intXABOMHeaderCnt > 0)
                {
                    // Make delay between Systenlink call and XA Header Count check 
                    _ = MakeDelayInProcessAsync(MakeDelayInProcessMilliseconds);
                    
                    // IT NOT NEEDED 
                    // // intXABOMHeaderCnt = clsXA.XAHDRCountExecuteScalar(SELECT_PSTHDR, site, environment, parentItemNumber, parentItemRevisionNum);
                    ////logger.Info("BOM Header " + (intXABOMHeaderCnt > 0 ? " EXISTS for : " : " NOT EXISTS for : ") + parentItemNumber + " in XA | intXABOMHeaderCnt: " + intXABOMHeaderCnt.ToString() + " | Env: " + environment + " | Site: " + site + " | parentItemRevisionNum: " + parentItemRevisionNum);
                    ////if (intXABOMHeaderCnt > 0)
                    ////{
                    
                        Request.InputStream.Position = 0;
                        using (var reader = new StreamReader(Request.InputStream))
                        {
                            var bodyBOMXAU = reader.ReadToEnd();
                            // it has DONT DELETE item / component information 
                            var itemsBXAU = JsonConvert.DeserializeObject<List<Item>>(bodyBOMXAU);
                            int intXABOMComponentCnt = 0;
                            // Loop through each item in jqgrid and process BOM component.
                            foreach (Item componentItem in componentDetailList)
                            {
                                IsDontDelete = false;
                                
                                if (componentItem.Action == "Add")
                                {
                                    intXABOMComponentCnt = clsXA.XADTLCountExecuteScalar(SELECT_PSTDTL, site, environment, componentItem.ParentItemNumber, componentItem.ComponentItemNumber, componentItem.UserSequence, parentItemRevisionNum); 
                                    logger.Info(">> UploadBomToXA >> " + componentItem.Action + " << BOM component to XA | hasBOMCompExist: " + intXABOMComponentCnt.ToString() + " | BOM Component: " + componentItem.ComponentItemNumber + " | UserSequence: " + componentItem.UserSequence + Environment.NewLine);  
                                    if (intXABOMComponentCnt == 0)
                                    {
                                        logger.Info("Add component to XA : " + componentItem.ComponentItemNumber + " for Parent Item: " + parentItemNumber + " | Env: " + environment + " | Site: " + site + " | ComponentItemRevision: " + componentItem.ComponentItemRevision);
                                        createResult = clsXA.CreateBOMComponent(componentItem.Site, componentItem.ParentItemNumber, parentItemRevisionNum, componentItem.AlternateBOMId, componentItem.ComponentItemNumber.ToUpper(), componentItem.ComponentItemRevision, componentItem.UserSequence, componentItem.Qty, componentItem.Environment, loggedinUser);
                                        componentItem.Status = createResult;
                                    }
                                }
                                else if (componentItem.Action == "Change")
                                {
                                    intXABOMComponentCnt = clsXA.XADTLCountExecuteScalar(SELECT_PSTDTL, site, environment, componentItem.ParentItemNumber, componentItem.ComponentItemNumber, componentItem.UserSequence, parentItemRevisionNum); 
                                    logger.Info(">> UploadBomToXA >> " + componentItem.Action + " << BOM component to XA | hasBOMCompExist: " + intXABOMComponentCnt.ToString() + " | BOM Component: " + componentItem.ComponentItemNumber + " | UserSequence: " + componentItem.UserSequence + Environment.NewLine); 
                                    if (intXABOMComponentCnt > 0)
                                    {
                                        // componentItem.ParentItemRevision 
                                        logger.Info("Change component to XA : " + componentItem.ComponentItemNumber + " for Parent Item: " + parentItemNumber + " | Env: " + environment + " | Site: " + site + " | ComponentItemRevision: " + componentItem.ComponentItemRevision);
                                        updateResult = clsXA.UpdateBOMComponent(componentItem.MasterCode, componentItem.Site, componentItem.ParentItemNumber, parentItemRevisionNum, componentItem.AlternateBOMId, componentItem.ComponentItemNumber.ToUpper(), componentItem.ComponentItemRevision, componentItem.UserSequence, componentItem.Qty, componentItem.Environment, loggedinUser);
                                        componentItem.Status = updateResult;
                                    }
                                }
                                else if (componentItem.Action == "Delete")
                                {
                                    foreach (var itemX in itemsBXAU)
                                    {
                                        if (itemX.DontDelete && itemX.Action == componentItem.Action && itemX.ComponentItemNumber.ToUpper() == componentItem.ComponentItemNumber.ToUpper() && itemX.UserSequence == componentItem.UserSequence)
                                        {
                                            IsDontDelete = true;
                                            break;
                                        }
                                    }

                                    intXABOMComponentCnt = clsXA.XADTLCountExecuteScalar(SELECT_PSTDTL, site, environment, componentItem.ParentItemNumber, componentItem.ComponentItemNumber, componentItem.UserSequence, parentItemRevisionNum); 
                                    logger.Info(">> UploadBomToXA >> " + componentItem.Action + " << BOM component to XA | hasBOMCompExist: " + intXABOMComponentCnt.ToString() + " | BOM Component: " + componentItem.ComponentItemNumber + " | UserSequence: " + componentItem.UserSequence + Environment.NewLine); 
                                    if (intXABOMComponentCnt > 0 && !IsDontDelete)
                                    {
                                        logger.Info("Delete component from XA : " + componentItem.ComponentItemNumber + " for Parent Item: " + parentItemNumber + " | Env: " + environment + " | Site: " + site + " | ComponentItemRevision: " + componentItem.ComponentItemRevision);
                                        deleteResult = clsXA.DeleteBOMComponent(componentItem.MasterCode, componentItem.Site, componentItem.ParentItemNumber, parentItemRevisionNum, componentItem.AlternateBOMId, componentItem.ComponentItemNumber.ToUpper(), componentItem.ComponentItemRevision, componentItem.UserSequence, componentItem.Qty, componentItem.Environment, loggedinUser);
                                        componentItem.Status = deleteResult;
                                    }
                                }
                                else
                                {
                                    componentItem.Status = "Successfully Skipped NoChange";
                                }

                            if (componentItem.Status != null)
                                if (!componentItem.Status.Contains("Successfully"))
                                {
                                    boolSendEmailNotification = true;
                                    logEmailBody.Append("<tr><td>" + componentItem.ParentItemNumber + "</td>");
                                    logEmailBody.Append("<td>" + componentItem.ComponentItemNumber + "</td>");
                                    logEmailBody.Append("<td>" + componentItem.Action + "</td>");
                                    logEmailBody.Append("<td>" + componentItem.Environment + "</td>");
                                    logEmailBody.Append("<td>" + componentItem.Site + "</td>");
                                    logEmailBody.Append("<td>" + loggedinUser + "</td>");
                                    logEmailBody.Append("<td>" + componentItem.Status + "</td></tr>");
                                }

                                logger.Info("Component Item Systemlink response from XA : " + componentItem.ComponentItemNumber + " for Parent Item: " + parentItemNumber 
                                            + " | Env: " + environment + " | Site: " + site + " | ComponentItemRevision: " + componentItem.ComponentItemRevision
                                            + " | Component Systemlink response: " + componentItem.Status + Environment.NewLine); 
                            }
                        }

                        logEmailBody.Append("</table><br/><br/> Regards, <br/>XA BOM Synchronization Master.");

                        // Call Logout XA session 3 
                        if (Session["XASlrSession"] != null)
                            objModel.LogoutXASyslinkSessionHandle(Session["XASlrSession"].ToString());

                        if (boolSendEmailNotification)
                        {
                            // //SendEmailAlertNotificationBOMUploadtoXA(logEmailBody.ToString());
                            SendEmailAlertNotificationBOMUploadtoXASubject(logEmailBody.ToString(), loggedinUserEmail, parentItemNumber, environment);
                        }
                        else
                        {
                            logEmailBody.Clear();
                        }
                    ////}
                    ////else 
                    ////{ 
                    ////    successMessage = "Components exists | BOM Header["+ intXABOMHeaderCnt.ToString() + "]" + (intXABOMHeaderCnt > 0 ? " EXISTS for : " : " NOT EXISTS for : " + " parentItemNumber: " + parentItemNumber + " in XA.");
                    ////}
                }

                // Delete SQL PDM data after retrieval 
                // The SQL table is just a temporary table. So if it gets cleared after every attempt to push to Xa, either success or fail clearing should be OK,
                // because then and then what we're doing is we're reporting back from XA what we've pushed or not pushed.
                // I think it's OK to clear the SQL table after every push - [ Shaji ].   

                clsXA.DELETE_PDMComponentsFromSQLByParentNumber(parentItemNumber);  
                // XABomComponentList = null; 
            }
            catch (Exception ex)
            {
                logger.Error("Check for Item Existence in XA failed : " + ex.Message + System.Environment.NewLine);
                    boolSendEmailNotification = true;
                // Redirect the page to list PDM SQL and XA comparision
                // 403 error for - it is unauthorized, so the server is refusing to give the requested resource
                if (ex.Message.Contains("[DB2 for i5/OS]SQL0551 - Not authorized to object"))
                {
                    SendEmailAlertNotificationBOMUploadtoXASubject("Upload Bom To XA Error: " + ex.Message, loggedinUserEmail, parentItemNumber, environment);
                    successMessage = ex.Message;
                    // TempData["BOMUploadtoXAFailureMsg"] = ex.Message;
                    // ViewBag.ErrorMessage = TempData["BOMUploadtoXAFailureMsg"];
                    // wrong place //return RedirectToAction("Index", "BOMUpload", new { ReadFrom = "SQLDB", ParentItem = parentItemNumber });
                }
                else
                {
                    //TempData["BOMUploadtoXAFailureMsg"] = ex.Message;
                    SendEmailAlertNotificationBOMUploadtoXASubject("Upload Bom To XA Error: " + ex.Message, loggedinUserEmail, parentItemNumber, environment);
                    // wrong place //return Json(new { success = false, successMessage = successMessage, message = "Failed to process!" }, JsonRequestBehavior.AllowGet);
                }
                
                // clear the SQL table data after every push if comparision viewed and UploadBomToXA Click  
                clsXA.DELETE_PDMComponentsFromSQLByParentNumber(parentItemNumber); 
            }

            TempData["BOMUploadSuccessMsg"] = successMessage + " | ParentItem: " + parentItemNumber + " | Env: " + environment + " | Site: " + site;
            logger.Info(successMessage + " | ParentItem: " + parentItemNumber + " | Env: " + environment + " | Site: " + site + Environment.NewLine);

            // the BOM page redirected to load BOMs from XA for ParentItem and Env.
            string redirectUrl = string.Empty;
            var queryStr = System.Web.HttpUtility.ParseQueryString(string.Empty);
           
            queryStr["Env"] = environment;
            queryStr["ParentItem"] = parentItemNumber;

            queryStr["Upl"] = boolSendEmailNotification ? "0" : "1";

            redirectUrl = $"/BOMUpload/Index?" + queryStr.ToString();
            ViewBag.SelectedEnv = environment; 
            return Json(new { redirectUrl }, JsonRequestBehavior.AllowGet);   
            // // return Json(new { redirectUrl });
        }

        [Obsolete] // NO USE REMOVE LATER 
        [HttpPost]
        public ActionResult SaveUserSelectedENVPDM(string environment)
        {
            try
            {
                logger.Info("ActionResult: Saving of user selection of PDM environment in cookie has begun" + Environment.NewLine);
                //Create a Cookie with a suitable Key.
                HttpCookie EnvironmentCookiePDM = Request.Cookies["EnvironmentCookiePDM"];

                if (EnvironmentCookiePDM == null)
                     EnvironmentCookiePDM = new HttpCookie("EnvironmentCookiePDM");

                //Set the Cookie value.
                EnvironmentCookiePDM.Values["EnvironmentPDM"] = environment;

                //Set the Expiry date.
                EnvironmentCookiePDM.Expires = DateTime.Now.AddDays(60);

                //Add the cookie to the Response so it's sent to the browser
                Response.Cookies.Add(EnvironmentCookiePDM);

                logger.Info("ActionResult: Saving of user selection of PDM environment in cookie has been completed." + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Log the exception details
                // Example: Use a logging framework or log to a file
                System.IO.File.WriteAllText(@"C:\500path\500log.txt", ex.ToString());

                // Return a custom error response
                return new HttpStatusCodeResult(500, "SaveUserSelectedENVPDM : An unexpected error occurred: " + ex.Message);
            }

            // Return a JSON response indicating success
            return Json(new { success = true });
        }

        /// <summary>
        /// SendEmailAlertNotificationBOMUploadtoXA
        /// </summary>      
        public void SendEmailAlertNotificationBOMUploadtoXA(string strEmailBody)
        {
            string toEmail = TOEmail;
            string fromEmail = FROMEmail;
            MailMessage message = new MailMessage(fromEmail, toEmail);

            if (CCEmail.Length > 0)
                message.CC.Add(CCEmail);

            logger.Info("SMTP settings FROM: {0} |TO: {1} |CC: {2} |HOST: {3}", FROMEmail, TOEmail, CCEmail, emailSMTPHostName);
          
            try
            {
                message.Subject = EmailSubject;

                message.Body = strEmailBody;
                message.BodyEncoding = System.Text.Encoding.UTF8;
                message.IsBodyHtml = true;

                // Credentials are necessary if the server requires the client
                // to authenticate before it will send email on the client's behalf.

                SmtpClient client = new SmtpClient(emailSMTPHostName);
                client.UseDefaultCredentials = true;
                //// client.EnableSsl = true; Not required
                client.Send(message);
            }
            catch (Exception ex)
            {
                logger.Info("Exception caught in SendEmailAlertNotificationBOMUploadtoXA while sending Email Alert: {0}",
                    ex.ToString());
                
                throw ex;
            }
        }

        /// <summary>
        /// SendEmailAlertNotificationBOMUploadtoXASubject
        /// </summary>      
        public void SendEmailAlertNotificationBOMUploadtoXASubject(string strEmailBody, string strToLoggedInUserEmail, string strParentItem, string strENV)
        {
            System.Text.StringBuilder logEmailBody = new System.Text.StringBuilder();
            string toEmail = strToLoggedInUserEmail.Trim().Contains(".com") ? strToLoggedInUserEmail : TOEmail;
            string fromEmail = FROMEmail;
            string strEmailSub = string.Empty;
            strEmailSub = "Parent Item #: " + strParentItem + " | ENV: " + strENV;

            // send email to itservice@barry-wehmiller.co if Systemlink service call error 
            if (strEmailBody.Contains("Systemlink service") || strEmailBody.Contains("DB2")) 
            {
                toEmail = TOEmailITservice;
                CCEmail = CCEmailITservice; 
                strEmailSub = "Systemlink service call error Parent Item #: " + strParentItem + " | ENV: " + strENV; 
            }

            MailMessage message = new MailMessage(fromEmail, toEmail);

            if (CCEmail.Length > 0)
                message.CC.Add(CCEmail);

            logger.Info("SMTP settings FROM: {0} TO: {1} HOST: {2}", FROMEmail, TOEmail, emailSMTPHostName);

            try
            {
                logEmailBody.Append("This to notify that below issues are captured while BOM Upload to XA for Parent Item number: " + strParentItem + " | ENV: " + strENV + " on " + DateTime.Now + ".");
                logEmailBody.Append("<br/><br/>");
                logEmailBody.Append("<table border=0 width=\"50%\" align=\"Left\">");
                logEmailBody.Append("<tr bgcolor='yellow'><th>BOM Upload to XA notification – Error in XA BOM: </th></tr>");
                logEmailBody.Append("<tr bgcolor=HoneyDew><td></td></tr>");
                logEmailBody.Append("<tr bgcolor=HoneyDew><td>" + strEmailBody + "</td></tr>");
                logEmailBody.Append("<tr bgcolor=HoneyDew><td></td></tr>");
                logEmailBody.Append("<tr bgcolor=HoneyDew><td></td></tr>");
                logEmailBody.Append("<tr bgcolor=HoneyDew><td><br/>Please respond to this email if you were unable to correct the issue. Our team will reach out to you to help resolve your issue.</td></tr>");
                logEmailBody.Append("<tr bgcolor=HoneyDew><td><br/>Regards, </td></tr>");
                logEmailBody.Append("<tr bgcolor=HoneyDew><td>XA BOM Synchronization Master.</td></tr>");
                logEmailBody.Append("</table>");
                logEmailBody.Append(""); 

                if (strEmailSub.Trim().Length > 3)
                {
                    EmailSubject = EmailSubject + " | " + strEmailSub;
                }
                message.Subject = EmailSubject;
                message.Body = logEmailBody.ToString();
                message.BodyEncoding = System.Text.Encoding.UTF8;
                message.IsBodyHtml = true;

                // Credentials are necessary if the server requires the client
                // to authenticate before it will send email on the client's behalf.

                SmtpClient client = new SmtpClient(emailSMTPHostName);
                client.UseDefaultCredentials = true;
                //// client.EnableSsl = true; Not required
                client.Send(message);
            }
            catch (Exception ex)
            {
                logger.Info("Exception caught in SendEmailAlertNotificationBOMUploadtoXA while sending Email Alert: {0}",
                    ex.ToString());

                throw ex;
            }
        }

        [HttpPost]
        public ActionResult LogGridLoadData(int count, string env, string item) 
        {
            logger.Info($"BOMUploadGrid loaded with {count} records for ENV [{env}], Item [{item}] at {DateTime.Now}");
            return Json(new { success = true });
        }

        public string GetEmailForLoggedInADUser(string strLoginUser)
        {
            string emailId = string.Empty;
            string email = string.Empty;

            try
            {
 
                string eValue = strLoginUser.Trim();
                if (eValue != "")
                {
                    System.DirectoryServices.DirectorySearcher searcher1 = new System.DirectoryServices.DirectorySearcher();
                    searcher1.Filter = "(&(sAMAccountName=" + eValue + ")(objectCategory=person)(objectClass=user))";
                    System.DirectoryServices.SearchResultCollection results1;
                    results1 = searcher1.FindAll();
                    foreach (System.DirectoryServices.SearchResult item in results1)
                    {
                        emailId = item.GetDirectoryEntry().Properties["Mail"].Value.ToString();
                        if (emailId.Trim().ToLower().Contains(".com"))
                            break; 
                    }  
                    //if (email.Contains(emailId) == false)
                    //{
                    //    if (Count == 0)
                    //    {
                    //        email = emailId;
                    //    }
                    //    else
                    //    {
                    //        email = emailId + "," + email;
                    //    }
                    //}
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return emailId;

        }

    }
}