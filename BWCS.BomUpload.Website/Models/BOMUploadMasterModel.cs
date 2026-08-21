using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Security.Policy;
using System.Web;
using System.Web.DynamicData;
using System.Web.Mvc;
using System.Web.SessionState;
using BWCS.BOMUpload.BLL;
using BWCS.BOMUpload.DAL;
using NLog;

namespace BWCS.BomUpload.Website.Models
{
    public class BOMUploadMasterModel
    {
        #region Declarations

        string xaServer = System.Configuration.ConfigurationManager.AppSettings["XASERVER"];
        string xaSystemName = System.Configuration.ConfigurationManager.AppSettings["XASystemName"];
        string maxIdleTime = System.Configuration.ConfigurationManager.AppSettings["MAXIDLE"];
        string xaUserName = System.Configuration.ConfigurationManager.AppSettings["XAUser"];
        string xaPassword = System.Configuration.ConfigurationManager.AppSettings["XAPwd"];
        string errorMessage = string.Empty;
        string actionResult = string.Empty;
        HttpContext context = HttpContext.Current;
        string xaSession = string.Empty; 
        XASystemLink clsSLR = new XASystemLink();
        Logger logger = LogManager.GetCurrentClassLogger();

        DBManager dbManager = null;

        private static double intervalTime = Convert.ToDouble((ConfigurationManager.AppSettings["IntervalTime"]).ToString());

        #endregion

        public string GetxaSystemName 
        {
            get { return xaSystemName; } // set { xaSystemName = value; }
        }

        /// <summary>
        /// Reuse Systemlink session handle for successive sys. link request calls ... 
        /// </summary>
        /// <returns></returns>
        public void getXASLRSession(string strItemEnv) 
        {
            try
            {
                if (HttpContext.Current.Session["XASlrSession"] == null)
                {
                    xaSession = clsSLR.XASession(xaUserName, xaPassword, maxIdleTime, strItemEnv, xaSystemName);
                    HttpContext.Current.Session["XASlrSession"] = xaSession;
                }
                else
                    xaSession = HttpContext.Current.Session["XASlrSession"] as string;

                // xaSession = HttpContext.Current.Session["XASlrSession"] as string; 
            }
            catch (Exception)
            {
                throw;
            }
        }

        #region BOM Upload Functions

        #region Get Item Information

        /// <summary>
        /// Get item details from XA.
        /// </summary>
        /// <param name="itemNumber">Item to be retrieved from XA.</param>
        /// <param name="environment">Environment to which item belongs to.</param>
        /// <returns>Item information for the searched itemnumber.</returns>
        public IList<Item> GetItemDetails(string itemNumber, string environment, string site, string loggedinUser, out string strParentItemRevision, out string strUom, out string strITTYP, out string strItmDes, string fromQueryString = "N")
        {
            IList<Item> ItemData = new List<Item>();

            string xaAction, stritemStatus = string.Empty;
            bool blSuccess = false;
            string strItemEnv = string.Empty;
            try
            {
                //if (fromQueryString == "N")
                //    SaveUserSelection(loggedinUser, environment);
                //else if (fromQueryString == "Y")
                //    SaveUserSelectionPDM(loggedinUser, environment);

                itemNumber = itemNumber.Trim().ToUpper();//A78305
                strItemEnv = environment.Substring(0, 2); // Read first two letters for Environment 

                getXASLRSession(strItemEnv);  
                //xaSession = clsSLR.XASession(xaUserName, xaPassword, maxIdleTime, strItemEnv, xaSystemName);

                if (!string.IsNullOrEmpty(xaSession))
                {
                    xaAction = clsSLR.GetItemDetails(itemNumber);
                    if (!string.IsNullOrEmpty(xaAction))
                    {
                        xaAction = xaAction.Replace("^^item^^", itemNumber);
                        xaAction = xaAction.Replace("^^site^^", site);
                        logger.Info("Get Item Details Systemlink Request : " + xaAction + Environment.NewLine);
                        blSuccess = clsSLR.ExecuteSLRequest(xaSession, xaAction, strItemEnv, ref errorMessage, ItemData, ref actionResult);

                        if (errorMessage.Trim().Length > 0) { logger.Info("Error while GetItemDetails: " + errorMessage + Environment.NewLine); }
                    }
                }
            }
            catch (Exception)
            {
                throw ;
            }

            strUom = ""; strITTYP = ""; strParentItemRevision = ""; strItmDes = "";
            if (ItemData.Count > 0 && !string.IsNullOrEmpty(ItemData[0].ItemNumber.Trim()))
            {
                strUom = ItemData[0].UnitOfMeasure;
                strITTYP = ItemData[0].ItemType.Trim();
                strParentItemRevision = ItemData[0].ParentItemRevision;
                strItmDes = ItemData[0].CompleteDescription;    //ItemDescription;

                //if (!string.IsNullOrEmpty(ItemData[0].ExtendedDescrption1))
                //    strItmDes = strItmDes + (ItemData[0].ExtendedDescrption1.Trim().Length > 0 ? " | " + ItemData[0].ExtendedDescrption1.Trim() : "");

                //if (!string.IsNullOrEmpty(ItemData[0].ExtendedDescrption2))
                //    strItmDes = strItmDes + (ItemData[0].ExtendedDescrption2.Trim().Length > 0 ? " | " + ItemData[0].ExtendedDescrption2.Trim() : "");

            }

            return ItemData;
        }

        #endregion

        #region Get Item Information with Revision

        /// <summary>
        /// Get item details with Revision from XA. GetItemDetailsByRevision
        /// </summary>
        /// <param name="itemNumber">Item to be retrieved from XA.</param>
        /// <param name="environment">Environment to which item belongs to.</param>
        /// <returns>Item information for the searched itemnumber.</returns>
        public IList<Item> GetItemDetailsByRevision(string itemNumber, string environment, string site, string loggedinUser, string strParentItemRevision, out string strUom, out string strITTYP, out string strItmDes, string fromQueryString = "N")
        {
            IList<Item> ItemData = new List<Item>();

            string xaAction, stritemStatus = string.Empty;
            bool blSuccess = false;
            string strItemEnv = string.Empty;
            try
            {
                itemNumber = itemNumber.Trim().ToUpper(); //A78305
                strItemEnv = environment.Substring(0, 2); // Read first two letters for Environment 

                getXASLRSession(strItemEnv);  

                if (!string.IsNullOrEmpty(xaSession))
                {
                    xaAction = clsSLR.GetItemDetailsByRevision(itemNumber);
                    if (!string.IsNullOrEmpty(xaAction))
                    {
                        xaAction = xaAction.Replace("^^item^^", itemNumber);
                        xaAction = xaAction.Replace("^^site^^", site); 
                        xaAction = xaAction.Replace("^^revision^^", strParentItemRevision); 

                        logger.Info("Get Item Details Revision Systemlink Request : " + xaAction + Environment.NewLine);
                        blSuccess = clsSLR.ExecuteSLRequest(xaSession, xaAction, strItemEnv, ref errorMessage, ItemData, ref actionResult);

                        if (errorMessage.Trim().Length > 0) { logger.Info("Error while GetItemDetailsByRevision: " + errorMessage + Environment.NewLine); }
                    }
                }
            }
            catch (Exception)
            {
                throw ;
            }

            strUom = ""; strITTYP = ""; strItmDes = "";
            if (ItemData.Count > 0 && !string.IsNullOrEmpty(ItemData[0].ItemNumber.Trim()))
            {
                strUom = ItemData[0].UnitOfMeasure;
                strITTYP = ItemData[0].ItemType.Trim();
                strItmDes = ItemData[0].CompleteDescription;    //ItemDescription;

                //if (!string.IsNullOrEmpty(ItemData[0].ExtendedDescrption1))
                //    strItmDes = strItmDes + (ItemData[0].ExtendedDescrption1.Trim().Length > 0 ? " | " + ItemData[0].ExtendedDescrption1.Trim() : "");

                //if (!string.IsNullOrEmpty(ItemData[0].ExtendedDescrption2))
                //    strItmDes = strItmDes + (ItemData[0].ExtendedDescrption2.Trim().Length > 0 ? " | " + ItemData[0].ExtendedDescrption2.Trim() : "");

            }

            return ItemData;
        }

        #endregion

        /// <summary>
        /// Get GetParentItemRevisionByItemSite from XA.
        /// </summary>
        /// <param name="itemNumber">Item to be retrieved from XA.</param>
        /// <param name="environment">Environment to which item belongs to.</param>
        /// <returns>Item information for the searched itemnumber.</returns>
        public IList<Item> GetParentItemRevisionByItemSite(string itemNumber, string environment, string site, string loggedinUser, out string strParentItemRevision, out string strUom, out string strITTYP, out string strItmDes, string fromQueryString = "N")
        {
            IList<Item> ItemData = new List<Item>();

            string xaAction, stritemStatus = string.Empty;
            bool blSuccess = false;
            string strItemEnv = string.Empty;
            try
            {
                itemNumber = itemNumber.Trim().ToUpper(); //A78305
                strItemEnv = environment.Substring(0, 2); // Read first two letters for Environment 

                getXASLRSession(strItemEnv);

                if (!string.IsNullOrEmpty(xaSession))
                {
                    xaAction = clsSLR.GetParentItemRevisionByParentItemSite(itemNumber);
                    if (!string.IsNullOrEmpty(xaAction))
                    {
                        xaAction = xaAction.Replace("^^parentItem^^", itemNumber);
                        xaAction = xaAction.Replace("^^site^^", site);
                        logger.Info("GetParentItemRevisionByItemSite Systemlink Request : " + xaAction + Environment.NewLine);
                        blSuccess = clsSLR.ExecuteSLRequest(xaSession, xaAction, strItemEnv, ref errorMessage, ItemData, ref actionResult);

                        if (errorMessage.Trim().Length > 0) { logger.Info("Error while GetParentItemRevisionByItemSite: " + errorMessage + Environment.NewLine); }
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }

            strUom = ""; strITTYP = ""; strParentItemRevision = ""; strItmDes = "";
            if (ItemData.Count > 0 && !string.IsNullOrEmpty(ItemData[0].ItemNumber.Trim()))
            {
                strUom = ItemData[0].UnitOfMeasure;
                strITTYP = ItemData[0].ItemType.Trim();
                strParentItemRevision = ItemData[0].ParentItemRevision;
                strItmDes = ItemData[0].CompleteDescription;    //ItemDescription;

            }

            return ItemData;
        }

        #region Get Single Level BOM Data

        /// <summary>
        /// Get the list of BOM components attached to the parent item number. 
        /// </summary>
        /// <param name="itemNumber">Parent item number for which BOM components are retrieved from XA.</param>
        /// <param name="environment">Environment in which item exists.</param>
        /// <returns>List of BOM components.</returns>
        public List<Item> GetAllSingleLevelBOM(string itemNumber, string environment, string siteId, string loggedinUser, string fromQueryString = "N")
        {
            string xaAction = string.Empty;
            bool blSuccess = false;
            List<Item> BOMData = new List<Item>();

            //if (fromQueryString == "N")
            //    SaveUserSelection(loggedinUser, environment); // It may not required due to base url cookie 
            //else if (fromQueryString == "Y")
            //    SaveUserSelectionPDM(loggedinUser, environment);

            string strItemEnv = environment.Substring(0, 2); // Read first two letters for ENV 
            logger.Info("Get all Single level BOM has begun" + Environment.NewLine);
            try
            {
                itemNumber = itemNumber.Trim().ToUpper();//A78305
                getXASLRSession(strItemEnv);  
                //xaSession = clsSLR.XASession(xaUserName, xaPassword, maxIdleTime, strItemEnv, xaSystemName);
                if (!string.IsNullOrEmpty(xaSession))
                {
                    if (strItemEnv != "UU")
                    { xaAction = clsSLR.GetSingleLevelBOM(itemNumber); }
                    else if (strItemEnv == "UU")
                    { xaAction = clsSLR.UUGetSingleLevelBOM(itemNumber); }

                    if (!string.IsNullOrEmpty(xaAction))
                    {
                        xaAction = xaAction.Replace("^^parentItem^^", itemNumber);
                        xaAction = xaAction.Replace("^^site^^", siteId);    // siteid send in syteline request 
                        logger.Info("Get Single Level BOM Systemlink Request : " + xaAction + Environment.NewLine);
                        blSuccess = clsSLR.ExecuteSLRequest(xaSession, xaAction, strItemEnv, ref errorMessage, BOMData, ref actionResult);

                        if (errorMessage.Trim().Length > 0) { logger.Info("Error while GetAllSingleLevelBOM: " + errorMessage + Environment.NewLine); }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            logger.Info("Get all Single level BOM has completed" + Environment.NewLine);
            return BOMData;
        }

        #endregion

        #region Get Single Level BOM Data with Revision

        /// <summary>
        /// Get the list of BOM components attached to the parent item number for Parent item revision. GetAllSingleLevelBOMRevision
        /// </summary>
        /// <param name="itemNumber">Parent item number for which BOM components are retrieved from XA.</param>
        /// <param name="environment">Environment in which item exists.</param>
        /// <returns>List of BOM components.</returns>
        public List<Item> GetAllSingleLevelBOMRevision(string itemNumber, string environment, string siteId, string parentItemRevision, string loggedinUser, string fromQueryString = "N")
        {
            string xaAction = string.Empty;
            bool blSuccess = false;
            List<Item> BOMData = new List<Item>();

            string strItemEnv = environment.Substring(0, 2); // Read first two letters for ENV 
            logger.Info("Get all Single level BOM has begun" + Environment.NewLine);
            try
            {
                itemNumber = itemNumber.Trim().ToUpper();//A78305
                getXASLRSession(strItemEnv);  
 
                if (!string.IsNullOrEmpty(xaSession))
                {
                    if (strItemEnv != "UU")
                    { xaAction = clsSLR.GetSingleLevelBOMRevision(itemNumber); }
                    else if (strItemEnv == "UU")
                    { xaAction = clsSLR.UUGetSingleLevelBOM(itemNumber); }

                    if (!string.IsNullOrEmpty(xaAction))
                    {
                        xaAction = xaAction.Replace("^^parentItem^^", itemNumber);
                        xaAction = xaAction.Replace("^^site^^", siteId);    // siteid send in syteline request 
                        xaAction = xaAction.Replace("^^parentItemRevision^^", parentItemRevision);    // revision send in syteline request
                        logger.Info("Get Single Level BOM Parent Item Revision Systemlink Request : " + xaAction + Environment.NewLine);
                        blSuccess = clsSLR.ExecuteSLRequest(xaSession, xaAction, strItemEnv, ref errorMessage, BOMData, ref actionResult);

                        if (errorMessage.Trim().Length > 0) { logger.Info("Error while GetAllSingleLevelBOMRevision: " + errorMessage + Environment.NewLine); }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            logger.Info("Get all Single level BOM Parent Item Revision has completed" + Environment.NewLine);
            return BOMData;
        }

        #endregion

        #region Create BOM Header

        /// <summary>
        /// 
        /// </summary>
        /// <param name="site">Site.</param>
        /// <param name="parentItem">Parent item number.</param>
        /// <param name="parentItemRevision">Parent item revision.</param>
        /// <param name="alternateBOMId">Alternate BOM Id.</param>
        /// <param name="componentItem">Component item number</param>
        /// <param name="componentItemRevision">Component item revision.</param>
        /// <param name="userSequence">User Sequence (Balloon on grid).</param>
        /// <param name="qty">Quantity of component being added to parent item number.</param>
        /// <param name="environment">Environment to which the item belongs to.</param>
        /// <returns>success if added or failure if failed to create a new BOM.</returns>
        public string CreateBOMHeader(string site, string parentItem, string parentItemDescription, string parentItemRevisionNum, string environment, string createdby)
        {
            string xaAction = string.Empty;
            bool blSuccess = false;
            string token = string.Empty;
            string standardBatchQty = "1.000";
            string strItemEnv = environment.Substring(0, 2); // Read first two letters for ENV 
            logger.Info("Create BOM Header has begun" + Environment.NewLine);
            try
            {
                parentItem = parentItem.Trim().ToUpper();
                getXASLRSession(strItemEnv);
                //xaSession = clsSLR.XASession(xaUserName, xaPassword, maxIdleTime, strItemEnv, xaSystemName);
                if (!string.IsNullOrEmpty(xaSession))
                {
                    xaAction = clsSLR.CreateBOMHeader(site, parentItem, parentItemDescription, standardBatchQty);
                    if (!string.IsNullOrEmpty(xaAction))
                    {
                        xaAction = xaAction.Replace("^^site^^", site);
                        xaAction = xaAction.Replace("^^parentItem^^", parentItem);
                        xaAction = xaAction.Replace("^^description^^", parentItemDescription);
                        xaAction = xaAction.Replace("^^parentItemRevision^^", parentItemRevisionNum);
                        xaAction = xaAction.Replace("^^standardBatchQuantity^^", standardBatchQty);

                        blSuccess = clsSLR.ExecuteSLRequest(xaSession, xaAction, strItemEnv, ref errorMessage, null, ref actionResult);

                        if (blSuccess)
                        {
                            TrackBomUploadHistory(strItemEnv, "", site, parentItem, "", "", "", "", "", "0", "BOM Header", createdby);
                        }
                    }
                }
                logger.Info("Create BOM Header has ended. xaAction: " + xaAction + Environment.NewLine);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return actionResult;
        }

        #endregion

        #region Create BOM Component

        /// <summary>
        /// Add a new BOM component to the parent item number.
        /// </summary>
        /// <param name="site">Site.</param>
        /// <param name="parentItem">Parent item number.</param>
        /// <param name="parentItemRevision">Parent item revision.</param>
        /// <param name="alternateBOMId">Alternate BOM Id.</param>
        /// <param name="componentItem">Component item number</param>
        /// <param name="componentItemRevision">Component item revision.</param>
        /// <param name="userSequence">User Sequence (Balloon on grid).</param>
        /// <param name="qty">Quantity of component being added to parent item number.</param>
        /// <param name="environment">Environment to which the item belongs to.</param>
        /// <returns>success if added or failure if failed to create a new BOM.</returns>
        public string CreateBOMComponent(string site, string parentItem, string parentItemRevision, string alternateBOMId, string componentItem, string componentItemRevision, string userSequence, string qty, string environment, string createdby)
        {
            string xaAction = string.Empty;
            bool blSuccess = false;
            string token = string.Empty;
            string strItemEnv = environment.Substring(0, 2); // Read first two letters for ENV 
            logger.Info("Create BOM Component has begun" + Environment.NewLine);
            try
            {
                parentItem = parentItem.Trim().ToUpper();
                componentItem = componentItem.Trim().ToUpper();
                if (string.IsNullOrEmpty(alternateBOMId)) alternateBOMId = string.Empty;
                if (string.IsNullOrEmpty(componentItemRevision)) componentItemRevision = string.Empty;
                if (string.IsNullOrEmpty(parentItemRevision)) parentItemRevision = string.Empty;
                if (string.IsNullOrEmpty(qty)) qty = "1";
                if (!string.IsNullOrEmpty(userSequence))
                {
                    userSequence = userSequence.ToString().PadLeft(4, '0');
                }
                getXASLRSession(strItemEnv);
                //xaSession = clsSLR.XASession(xaUserName, xaPassword, maxIdleTime, strItemEnv, xaSystemName);
                if (!string.IsNullOrEmpty(xaSession))
                {
                    if (strItemEnv == "UU")
                        xaAction = clsSLR.CreateBOMComponentUU(site, parentItem, parentItemRevision, alternateBOMId, componentItem, componentItemRevision, userSequence, qty);
                    else
                        xaAction = clsSLR.CreateBOMComponent(site, parentItem, parentItemRevision, alternateBOMId, componentItem, componentItemRevision, userSequence, qty);

                    if (!string.IsNullOrEmpty(xaAction))
                    {
                        xaAction = xaAction.Replace("^^site^^", site);
                        xaAction = xaAction.Replace("^^parentItem^^", parentItem);
                        xaAction = xaAction.Replace("^^parentItemRevision^^", parentItemRevision);
                        xaAction = xaAction.Replace("^^alternateBomId^^", alternateBOMId);
                        xaAction = xaAction.Replace("^^componentItem^^", componentItem);
                        xaAction = xaAction.Replace("^^componentItemRevision^^", componentItemRevision);

                        if (strItemEnv == "UU")
                            xaAction = xaAction.Replace("^^userSequence2^^", userSequence);
                        else
                            xaAction = xaAction.Replace("^^userSequence1^^", userSequence);

                        xaAction = xaAction.Replace("^^quantityPerUnit^^", qty);

                        blSuccess = clsSLR.ExecuteSLRequest(xaSession, xaAction, strItemEnv, ref errorMessage, null, ref actionResult);

                        if (blSuccess)
                        {
                            TrackBomUploadHistory(strItemEnv, token, site, parentItem, parentItemRevision, alternateBOMId, componentItem, componentItemRevision, userSequence, qty, "Create", createdby);
                        }
                    }
                }
                logger.Info("Create BOM Component has ended" + Environment.NewLine);
                logger.Info("Create BOM Component xaAction: " + xaAction + Environment.NewLine); 
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return actionResult;
        }

        #endregion

        #region Update BOM Component

        /// <summary>
        /// Modify the existing component.
        /// </summary>
        /// <param name="token">Unique identifier of any component.</param>
        /// <param name="site">Site.</param>
        /// <param name="parentItem">Parent item number.</param>
        /// <param name="parentItemRevision">Parent item revision.</param>
        /// <param name="alternateBOMId">Alternate BOM Id.</param>
        /// <param name="componentItem">Component item number</param>
        /// <param name="componentItemRevision">Component item revision.</param>
        /// <param name="userSequence">User Sequence (Balloon on grid).</param>
        /// <param name="qty">Quantity of component being added to parent item number.</param>
        /// <param name="environment">Environment to which the item belongs to.</param>
        /// <returns>success if modified successfully or failure if failed to modify the component.</returns>
        public string UpdateBOMComponent(string token, string site, string parentItem, string parentItemRevision, string alternateBOMId, string componentItem, string componentItemRevision, string userSequence, string qty, string environment, string updatedby)
        {
            string xaAction = string.Empty;
            bool blSuccess = false;
            string strItemEnv = environment.Substring(0, 2); // Read first two letters for ENV 
            logger.Info("Update BOM Component has begun" + Environment.NewLine);
            try
            {
                parentItem = parentItem.Trim().ToUpper();
                componentItem = componentItem.Trim().ToUpper();
                if (string.IsNullOrEmpty(alternateBOMId)) alternateBOMId = string.Empty;
                if (string.IsNullOrEmpty(componentItemRevision)) componentItemRevision = string.Empty;
                if (string.IsNullOrEmpty(parentItemRevision)) parentItemRevision = string.Empty;
                if (!string.IsNullOrEmpty(userSequence))
                {
                    userSequence = userSequence.ToString().PadLeft(4, '0');
                }
                getXASLRSession(strItemEnv); 
                //xaSession = clsSLR.XASession(xaUserName, xaPassword, maxIdleTime, environment, xaSystemName);
                if (!string.IsNullOrEmpty(xaSession))
                {
                    if (strItemEnv == "UU")
                        xaAction = clsSLR.UpdateBOMComponentUU(site, parentItem, parentItemRevision, alternateBOMId, componentItem, componentItemRevision, token, userSequence, qty);
                    else
                        xaAction = clsSLR.UpdateBOMComponent(site, parentItem, parentItemRevision, alternateBOMId, componentItem, componentItemRevision, token, userSequence, qty);

                    if (!string.IsNullOrEmpty(xaAction))
                    {
                        xaAction = xaAction.Replace("^^site^^", site);
                        xaAction = xaAction.Replace("^^parentItem^^", parentItem);
                        xaAction = xaAction.Replace("^^parentItemRevision^^", parentItemRevision);
                        xaAction = xaAction.Replace("^^alternateBomId^^", alternateBOMId);
                        xaAction = xaAction.Replace("^^componentItem^^", componentItem);
                        xaAction = xaAction.Replace("^^componentItemRevision^^", componentItemRevision);
                        xaAction = xaAction.Replace("^^token^^", token);

                        if (strItemEnv == "UU")
                            xaAction = xaAction.Replace("^^userSequence2^^", userSequence);
                        else
                            xaAction = xaAction.Replace("^^userSequence1^^", userSequence);

                        xaAction = xaAction.Replace("^^quantityPerUnit^^", qty);

                        blSuccess = clsSLR.ExecuteSLRequest(xaSession, xaAction, environment, ref errorMessage, null, ref actionResult);
                        if (blSuccess)
                        {
                            TrackBomUploadHistory(environment, token, site, parentItem, parentItemRevision, alternateBOMId, componentItem, componentItemRevision, userSequence, qty, "Change", updatedby);
                        }
                    }
                }
                logger.Info("Update BOM Component has ended" + Environment.NewLine);
                logger.Info("Update BOM Component xaAction: " + xaAction + Environment.NewLine);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return actionResult;
        }

        #endregion

        #region Delete BOM Component

        /// <summary>
        /// Delete BOM component from the parent item number.
        /// </summary>
        /// <param name="token">Unique identifier of any component.</param>
        /// <param name="site">Site.</param>
        /// <param name="parentItem">Parent item number.</param>
        /// <param name="parentItemRevision">Parent item revision.</param>
        /// <param name="alternateBOMId">Alternate BOM Id.</param>
        /// <param name="componentItem">Component item number</param>
        /// <param name="componentItemRevision">Component item revision.</param>
        /// <param name="environment">Environment to which the item belongs to.</param>
        /// <returns>success if deleted succcessfully or failure if failed to delete the component.</returns>
        public string DeleteBOMComponent(string token, string site, string parentItem, string parentItemRevision, string alternateBOMId, string componentItem, string componentItemRevision, string userSequence, string qty, string environment, string deletedby)
        {
            string xaAction = string.Empty;
            bool blSuccess = false;
            logger.Info("Delete BOM Component has begun" + Environment.NewLine);
            try
            {
                parentItem = parentItem.Trim().ToUpper();
                componentItem = componentItem.Trim().ToUpper();
                if (string.IsNullOrEmpty(alternateBOMId)) alternateBOMId = string.Empty;
                if (string.IsNullOrEmpty(componentItemRevision)) componentItemRevision = string.Empty;
                if (string.IsNullOrEmpty(parentItemRevision)) parentItemRevision = string.Empty;
                getXASLRSession(environment);
                //xaSession = clsSLR.XASession(xaUserName, xaPassword, maxIdleTime, environment, xaSystemName);
                if (!string.IsNullOrEmpty(xaSession))
                {
                    xaAction = clsSLR.DeleteBOMComponent(site, parentItem, parentItemRevision, alternateBOMId, componentItem, componentItemRevision, token);
                    if (!string.IsNullOrEmpty(xaAction))
                    {
                        xaAction = xaAction.Replace("^^site^^", site);
                        xaAction = xaAction.Replace("^^parentItem^^", parentItem);
                        xaAction = xaAction.Replace("^^parentItemRevision^^", parentItemRevision);
                        xaAction = xaAction.Replace("^^alternateBomId^^", alternateBOMId);
                        xaAction = xaAction.Replace("^^componentItem^^", componentItem);
                        xaAction = xaAction.Replace("^^componentItemRevision^^", componentItemRevision);
                        xaAction = xaAction.Replace("^^token^^", token);

                        blSuccess = clsSLR.ExecuteSLRequest(xaSession, xaAction, environment, ref errorMessage, null, ref actionResult);
                        if (blSuccess)
                        {
                            actionResult = (errorMessage.Trim().Length == 0 ? "Deleted Successfully!" : actionResult);
                            actionResult = (actionResult.Trim() == "Created Successfully!" ? "Deleted Successfully!" : actionResult);
                            TrackBomUploadHistory(environment, token, site, parentItem, parentItemRevision, alternateBOMId, componentItem, componentItemRevision, userSequence, qty, "Delete", deletedby);
                        }
                    }
                }
                logger.Info("Delete BOM Component completed. xaAction: " + xaAction + Environment.NewLine);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return actionResult;
        }

        /// <summary>
        /// LogoutXASyslinkSessionHandle
        /// </summary>
        /// <param name="strXASessionHandle"></param>
        public void LogoutXASyslinkSessionHandle(string strXASessionHandle)  
        {
            string strXARequest = string.Empty;
            //string strXASessionHandle = string.Empty; 

            try
            {
                ////if (HttpContext.Current.Session["XASlrSession"] != null)
                ////{
                    strXASessionHandle = HttpContext.Current.Session["XASlrSession"].ToString();

                    strXARequest = clsSLR.XALogOut(strXASessionHandle);
                    strXARequest = clsSLR.CreateFinalSystemLinkRequest(strXARequest);
                    System.Xml.XmlDocument xaXmlResponse = clsSLR.FetchData(strXARequest);
                
                HttpContext.Current.Session["XASlrSession"] = null;

                if (xaXmlResponse.InnerXml.Contains("true"))
                    logger.Info("|XA Session: " + strXASessionHandle + "|LogoutXASyslinkSessionHandle - actionSucceeded: TRUE");
                else
                    logger.Info("|XA Session: " + strXASessionHandle + "|LogoutXASyslinkSessionHandle - actionSucceeded: FALSE" + Environment.NewLine + xaXmlResponse.InnerXml); 
                ////} 
            }
            catch (Exception ex) {

                throw ex;
            }

        }

        /// <summary>
        /// Any change to BOM component is captured and stored in maintenancehistory table for later reference.
        /// </summary>
        /// <param name="environment">Environment to which the item belongs to.</param>
        /// <param name="token">Unique identifier of any component.</param>
        /// <param name="site">Site.</param>
        /// <param name="parentItem">Parent item number.</param>
        /// <param name="parentItemRevision">Parent item revision.</param>
        /// <param name="alternateBOMId">Alternate BOM Id.</param>
        /// <param name="componentItem">Component item number</param>
        /// <param name="componentItemRevision">Component item revision.</param>
        /// <param name="userSequence">User Sequence (Balloon on grid).</param>
        /// <param name="qty">Quantity of component being added to parent item number.</param>
        /// <param name="action">Create/Change/Delete action on the BOM component.</param>
        /// <param name="bomchangedby">User who initiated the BOM change.</param>
        private void TrackBomUploadHistory(string environment, string token, string site, string parentItem, string parentItemRevision, string alternateBOMId, string componentItem, string componentItemRevision, string userSequence, string qty, string action, string bomchangedby)
        {
            logger.Info("Writing BOM Upload action to SQL table MaintenanceHistory for later reference" + Environment.NewLine);
            dbManager = new DBManager();
            try
            {
                dbManager.Open();
                dbManager.CreateParameters(12);
                dbManager.AddParameters(0, "@Environment", environment);
                dbManager.AddParameters(1, "@Token", token);
                dbManager.AddParameters(2, "@SiteId", site);
                dbManager.AddParameters(3, "@ParentItemNumber", parentItem);
                dbManager.AddParameters(4, "@ParentItemRevision", parentItemRevision);
                dbManager.AddParameters(5, "@AlternateBomId", alternateBOMId);
                dbManager.AddParameters(6, "@ComponentItemNumer", componentItem);
                dbManager.AddParameters(7, "@ComponentItemRevision", componentItemRevision);
                dbManager.AddParameters(8, "@UserSequence", userSequence);
                dbManager.AddParameters(9, "@QuantityPerUnit", qty);
                dbManager.AddParameters(10, "@Action", action);
                dbManager.AddParameters(11, "@UploadedBy", bomchangedby);
                dbManager.ExecuteNonQuery(CommandType.StoredProcedure, "dbo.[spSaveBomUploadTrackingHistory]");
                dbManager.Close();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                dbManager.Dispose();
            }
        }

        /// <summary>
        /// Get the list of environments sorted by user's previous selection of environment.
        /// </summary>
        /// <returns>Get all active environments.</returns>
        public IList<BOMUploadModel> GetAllEnvironments(string fromQueryString)
        {
            logger.Info("BOMUploadMasterModel.GetAllEnvironments: " + Environment.NewLine);
            IList<BOMUploadModel> environmentList = new List<BOMUploadModel>();
            dbManager = new DBManager();
            IDataReader drEnvironments;
            string environment = "All";
            try
            {
                dbManager.Open();
                dbManager.CreateParameters(1);
                //var cookie = context.Request.Cookies["EnvironmentCookie"];
                //var cookiePDM = context.Request.Cookies["EnvironmentCookiePDM"];

                dbManager.AddParameters(0, "@UserPreferredEnvironment", environment);
                drEnvironments = dbManager.ExecuteReader(CommandType.StoredProcedure, "dbo.[spGetAllActiveEnvironmentsByUserPreference]");
                while (drEnvironments.Read())
                {
                    BOMUploadModel environments = new BOMUploadModel();
                    environments.EnvironmentId = Convert.ToString(drEnvironments["EnvironmentId"]);
                    environments.EnvironmentCode = Convert.ToString(drEnvironments["SiteId"]);
                    environmentList.Add(environments);
                }
                logger.Info("BOMUploadMasterModel.GetAllEnvironments - count: " + environmentList.Count.ToString() + " | UserPreferredEnvironment: " + environment + Environment.NewLine);
                dbManager.Close();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                dbManager.Dispose();
            }
            return environmentList;
        }

        /// <summary>
        /// Get Site for the given environment.
        /// </summary>
        /// <param name="environment">Environment to which BOM actions are being done.</param>
        /// <returns>Site of the environment.</returns>
        public string GetSiteByEnvironment(string environment)
        {
            logger.Info("Get Site Id for environment : " + environment + Environment.NewLine);
            string site = string.Empty;
            dbManager = new DBManager();

            try
            {
                dbManager.Open();
                dbManager.CreateParameters(1);
                dbManager.AddParameters(0, "@EnvironmentId", environment);
                site = Convert.ToString(dbManager.ExecuteScalar(CommandType.StoredProcedure, "dbo.[spGetSiteByEnvironment]"));

                dbManager.Close();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                dbManager.Dispose();
            }
            return site;
        }

        /// <summary>
        /// Get PDM components from SQL temporary table for upload to XA.
        /// </summary>
        /// <param name="itemNumber">Parent item number.</param>
        /// <param name="environment">Environment.</param>
        /// <param name="siteId">Site.</param>
        /// <param name="loggedinUser">User logged into the system.</param>
        /// <returns></returns>
        public List<Item> GetPDMComponentsFromSQL(string itemNumber, string environment, string siteId, string loggedinUser)
        {
            logger.Info("Get PDM components from temporary SQL table" + Environment.NewLine);
            List<Item> componentList = new List<Item>();
            dbManager = new DBManager();
            IDataReader drComponents;

            try
            {
                dbManager.Open();
                dbManager.CreateParameters(1);

                dbManager.AddParameters(0, "@ParentItemNumber", itemNumber.Trim());
                drComponents = dbManager.ExecuteReader(CommandType.StoredProcedure, "dbo.[spGetBOMDataByParentItem]");
                while (drComponents.Read())
                {
                    Item componentItem = new Item();
                    componentItem.MasterCode = Convert.ToString(drComponents["ID"]);
                    componentItem.ParentItemNumber = Convert.ToString(drComponents["ParentItemNumber"]);
                    componentItem.ComponentItemNumber = Convert.ToString(drComponents["ChildItemNumber"]).Trim(); // Trailing space TRIM applied - Tested.
                    componentItem.CompleteComponentDescription = Convert.ToString(drComponents["ChildItemDescription"]);
                    if (!string.IsNullOrEmpty(Convert.ToString(drComponents["UserSequence"]).Trim()))
                        componentItem.UserSequence = Convert.ToString(drComponents["UserSequence"]).Trim().PadLeft(4, '0'); //Updated 
                    else
                        componentItem.UserSequence = Convert.ToString(drComponents["UserSequence"]);
                    componentItem.Qty = Convert.ToString(drComponents["Qty"]);
                    componentItem.Environment = environment;
                    componentItem.Site = siteId;

                    // NO Revision data from PDM SQL  
                    componentItem.ParentItemRevision = "";
                    componentItem.Revision = "";
                    componentItem.ComponentItemRevision = ""; 

                    componentList.Add(componentItem);
                }
                dbManager.Close();
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                dbManager.Dispose();
            }
            return componentList;
        }

        public List<Item> GetPDMComponentsRevisionFromSQL(string itemNumber, string environment, string siteId, string parentItemRevision, string loggedinUser)
        {
            logger.Info("Get PDM components from temporary SQL table" + Environment.NewLine);
            List<Item> componentList = new List<Item>();
            dbManager = new DBManager();
            IDataReader drComponents;

            try
            {
                dbManager.Open();
                dbManager.CreateParameters(2);

                dbManager.AddParameters(0, "@ParentItemNumber", itemNumber.Trim());
                dbManager.AddParameters(1, "@ParentItemRevision", parentItemRevision.Trim());
                drComponents = dbManager.ExecuteReader(CommandType.StoredProcedure, "dbo.[spGetBOMDataByParentItemRevision]");
                while (drComponents.Read())
                {
                    Item componentItem = new Item();
                    componentItem.MasterCode = Convert.ToString(drComponents["ID"]);
                    componentItem.ParentItemNumber = Convert.ToString(drComponents["ParentItemNumber"]);
                    componentItem.ComponentItemNumber = Convert.ToString(drComponents["ChildItemNumber"]).Trim(); // Trailing space TRIM applied - Tested.
                    componentItem.CompleteComponentDescription = Convert.ToString(drComponents["ChildItemDescription"]);
                    if (!string.IsNullOrEmpty(Convert.ToString(drComponents["UserSequence"]).Trim()))
                        componentItem.UserSequence = Convert.ToString(drComponents["UserSequence"]).Trim().PadLeft(4, '0'); //Updated 
                    else
                        componentItem.UserSequence = Convert.ToString(drComponents["UserSequence"]);
                    componentItem.Qty = Convert.ToString(drComponents["Qty"]);
                    componentItem.Environment = environment;
                    componentItem.Site = siteId;

                    // NO Revision data from PDM SQL  
                    componentItem.Revision = "";
                    componentItem.ParentItemRevision = Convert.ToString(drComponents["ParentItemRevision"]);  
                    componentItem.ComponentItemRevision = Convert.ToString(drComponents["ChildItemRevision"]);

                    componentList.Add(componentItem);
                }
                dbManager.Close();
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                dbManager.Dispose();
            }
            return componentList;
        }

        /// <summary>
        /// This is used end of Upload BOM to XA 
        /// </summary>
        /// <param name="strParentItemNo"></param>
        public void DELETE_PDMComponentsFromSQLByParentNumber(string strParentItemNo)
        {
            dbManager = new DBManager();
            try
            {
                dbManager.Open();
                dbManager.CreateParameters(1);

                dbManager.AddParameters(0, "@ParentItemNumber", strParentItemNo.Trim());
                dbManager.ExecuteNonQuery(CommandType.StoredProcedure, "dbo.spDelBOMDataByParentItem");

                dbManager.Close();
            }
            catch (Exception ex)
            {

                throw ex;
            }
            finally
            { dbManager.Dispose(); }
        }

        /// <summary>
        /// To save user environment selection to set default environment during next visit.
        /// </summary>
        /// <param name="loggedinUser">User logged into the BOM Upload application.</param>
        /// <param name="environment">Default environment chosen by the user.</param>
        private void SaveUserSelection(string loggedinUser, string environment)
        {
            logger.Info("Saving of user selection of environment in cookie has begun" + Environment.NewLine);
            //Create a Cookie with a suitable Key.
            HttpCookie EnvironmentCookie = new HttpCookie("EnvironmentCookie");

            //Set the Cookie value.
            EnvironmentCookie.Values["Environment"] = environment;

            //Set the Expiry date.
            EnvironmentCookie.Expires = DateTime.Now.AddDays(60);

            //Add the Cookie to Browser.

            context.Response.Cookies.Add(EnvironmentCookie);

            logger.Info("Saving of user selection of environment in cookie has been completed." + Environment.NewLine);

        }

        /// <summary>
        /// PDM SQL DB: To save user environment selection to set default environment during next visit.
        /// </summary>
        /// <param name="loggedinUser">User logged into the BOM Upload application.</param>
        /// <param name="environment">Default environment chosen by the user.</param>
        private void SaveUserSelectionPDM(string loggedinUser, string environment)
        {
            logger.Info("Saving of user selection of PDM environment in cookie has begun" + Environment.NewLine);
            //Create a Cookie with a suitable Key.
            HttpCookie EnvironmentCookiePDM = new HttpCookie("EnvironmentCookiePDM");

            //Set the Cookie value.
            EnvironmentCookiePDM.Values["EnvironmentPDM"] = environment;

            //Set the Expiry date.
            EnvironmentCookiePDM.Expires = DateTime.Now.AddDays(60);

            //Add the Cookie to Browser.

            context.Response.Cookies.Add(EnvironmentCookiePDM);

            logger.Info("Saving of user selection of PDM environment in cookie has been completed." + Environment.NewLine);

        }

        #endregion

        #endregion

        //SELECT_ITMRVA
        /// <summary>
        /// GetItmRevisionNumberExecuteScalar
        /// </summary>
        /// <param name="strSELECT"></param>
        /// <param name="strENV"></param>
        /// <param name="strPINBR"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public string GetItmRevisionNumberExecuteScalar(string strSELECT, string strENV, string strPINBR)
        {
            string retItmRVNo = "";
            string AMFLIBKey = string.Empty;
            string xaConnection = ConfigurationManager.AppSettings["XASysIConn"].ToString();
            using (OdbcConnection as400con = new OdbcConnection(xaConnection))
            {
                try
                {
                    logger.Info("Open connection to AS400 - GetItmRevisionNumberExecuteScalar");
                    as400con.Open();
                    logger.Info("AS400 connection established successfully - GetItmRevisionNumberExecuteScalar");

                    AMFLIBKey = (strENV.Trim().Length > 1 ? strENV.Substring(1, 1) : strENV.Trim()).ToString();
                    strSELECT = strSELECT.Replace("[AS400LIB]", AMFLIBKey);

                    using (OdbcCommand selCMD = new OdbcCommand(strSELECT, as400con))
                    {
                        selCMD.Parameters.Add("@PINBR", OdbcType.NVarChar).Value = strPINBR.Trim();
                        retItmRVNo = selCMD.ExecuteScalar().ToString();
                        logger.Info("GetItmRevisionNumberExecuteScalar: " + retItmRVNo + " | ENV: " + strENV + " | Parent Item Number: " + strPINBR);
                    }
                }
                catch (Exception ex)
                {
                    logger.Info($"Failed to execute XA GetItmRevisionNumberExecuteScalar from on AS400 for [ENV: " + strENV.Trim() + "] : " + ex.Message);
                    throw new Exception("Failed to execute XA GetItmRevisionNumberExecuteScalar from on AS400 for [ENV: " + strENV.Trim() + "] : " + ex.Message);
                }
                finally
                {
                    as400con.Close();
                    //GC.Collect(); // Optional: use with caution
                }
                return retItmRVNo;
            }
        }


        /// <summary>
        /// THis is to confirm whether BOM Header exists in XA or NOT 
        /// </summary>
        /// <param name="strSELECT"></param>
        /// <param name="strPINBR"></param>
        /// <returns></returns>
        public int XAHDRCountExecuteScalar(string strSELECT, string strSiteID, string strENV, string strPINBR, string strPITRevisionNum)
        {
            int intDataCnt = 0;
            string AMFLIBKey = string.Empty;
            string xaConnection = ConfigurationManager.AppSettings["XASysIConn"].ToString();
            using (OdbcConnection as400con = new OdbcConnection(xaConnection))
            {
                try
                {
                    logger.Info("Open connection to AS400 - XAHDRCountExecuteScalar");
                    as400con.Open();
                    logger.Info("AS400 connection established successfully - XAHDRCountExecuteScalar");

                    AMFLIBKey = (strENV.Trim().Length > 1 ? strENV.Substring(1, 1) : strENV.Trim()).ToString();
                    strSELECT = strSELECT.Replace("[AS400LIB]", AMFLIBKey);

                    using (OdbcCommand selCMD = new OdbcCommand(strSELECT, as400con))
                    {
                        selCMD.Parameters.Add("@STID", OdbcType.NVarChar).Value = strSiteID.Trim();
                        selCMD.Parameters.Add("@PINBR", OdbcType.NVarChar).Value = strPINBR.Trim();
                        selCMD.Parameters.Add("@PITR", OdbcType.NVarChar).Value = strPITRevisionNum.Trim();
                        intDataCnt = System.Convert.ToInt32(selCMD.ExecuteScalar());
                        logger.Info("XAHDRCountExecuteScalar|CommandText: " + selCMD.CommandText);
                        logger.Info("XAHDRCountExecuteScalar|CommandText: " + selCMD.Parameters);
                        logger.Info("XAHDRCountExecuteScalar Header count: " + intDataCnt.ToString() + " | SiteID : " + strSiteID + " | ENV: " + strENV + " | Parent Item Number: " + strPINBR + " | PITRevision: " + strPITRevisionNum);
                    }
                }
                catch (Exception ex)
                {
                    logger.Info($"Failed to execute XA HDR CountExecuteScalar from on AS400 for [ENV: " + strENV.Trim() + "] : " + ex.Message);
                    throw new Exception("Failed to execute XA HDR CountExecuteScalar from on AS400 for [ENV: " + strENV.Trim() + "] : " + ex.Message);
                }
                finally
                {
                    as400con.Close();
                    //GC.Collect(); // Optional: use with caution
                }
                return intDataCnt;
            }
        }

        public int XAExecuteNonQuery(string strSELECT)
        {
            int intDataCnt = 0;
            string AMFLIBKey = string.Empty;
            string xaConnection = ConfigurationManager.AppSettings["XASysIConn"].ToString();
            using (OdbcConnection as400con = new OdbcConnection(xaConnection))
            {
                try
                {
                    if (strSELECT.Trim().Length > 0)
                    {
                        logger.Info("Open connection to AS400 - XAExecuteNonQuery Start ...");
                        as400con.Open();
                        logger.Info("AS400 connection established successfully - XAExecuteNonQuery");

                        using (OdbcCommand selCMD = new OdbcCommand(strSELECT, as400con))
                        {
                            intDataCnt = System.Convert.ToInt32(selCMD.ExecuteNonQuery());
                            logger.Info("XAExecuteNonQuery End.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Info($"Failed to execute XA XAExecuteNonQuery from on AS400: " + ex.Message);
                    throw new Exception("Failed to execute XA XAExecuteNonQuery from on AS400" + ex.Message);
                }
                finally
                {
                    as400con.Close();
                    //GC.Collect(); // Optional: use with caution
                }
                return intDataCnt;
            }
        }

        /// <summary>
        /// THis is to confirm whether BOM Component exists in XA or NOT 
        /// </summary>
        /// <param name="strSELECT"></param>
        /// <param name="strPINBR"></param>
        /// <returns></returns>
        public int XADTLCountExecuteScalar(string strSELECT, string strSiteID, string strENV, string strPINBR, string strCINBR, string strUSRS, string strPITRevisionNum)
        {
            int intDataCnt = 0;
            string AMFLIBKey = string.Empty;
            string xaConnection = ConfigurationManager.AppSettings["XASysIConn"];
            using (OdbcConnection as400con = new OdbcConnection(xaConnection))
            {
                try
                {
                    logger.Info("Open connection to AS400 - XADTLCountExecuteScalar");
                    as400con.Open();
                    logger.Info("AS400 connection established successfully - XADTLCountExecuteScalar");

                    AMFLIBKey = (strENV.Trim().Length > 1 ? strENV.Substring(1, 1) : strENV.Trim()).ToString();
                    strSELECT = strSELECT.Replace("[AS400LIB]", AMFLIBKey);

                    strENV = strENV.Substring(0, 2); // Read first two letters for ENV 
                    if (strENV.Trim() == "UU")
                        { strSELECT = strSELECT.Replace("AND USRS1", "AND USRS2"); }

                    using (OdbcCommand selCMD = new OdbcCommand(strSELECT, as400con))
                    {
                        selCMD.Parameters.Add("@STID", OdbcType.NVarChar).Value = strSiteID.Trim(); 
                        selCMD.Parameters.Add("@PINBR", OdbcType.NVarChar).Value = strPINBR.Trim();
                        selCMD.Parameters.Add("@CINBR", OdbcType.NVarChar).Value = strCINBR.Trim();
                        selCMD.Parameters.Add("@USRS1O2", OdbcType.NVarChar).Value = strUSRS.Trim();
                        selCMD.Parameters.Add("@PITR", OdbcType.NVarChar).Value = strPITRevisionNum.Trim();
                        intDataCnt = System.Convert.ToInt32(selCMD.ExecuteScalar());
                        logger.Info("XADTLCountExecuteScalar|CommandText: " + selCMD.CommandText);
                        logger.Info("XADTLCountExecuteScalar|CommandText: " + selCMD.Parameters);
                        logger.Info("XADTLCountExecuteScalar Detail count: " + intDataCnt.ToString() + " | SiteID : " + strSiteID + " | ENV: " + strENV + " | Parent Item Number: " + strPINBR + " | CINBR: " + strCINBR + " | USRS: " + strUSRS);
                    }
                }
                catch (Exception ex)
                {
                    logger.Info($"Failed to execute XA DTL CountExecuteScalar from on AS400 for [ENV: " + strENV.Trim() + "] : " + ex.Message);
                    throw new Exception("Failed to execute XA DTL CountExecuteScalar from on AS400 for [ENV: " + strENV.Trim() + "] : " + ex.Message);
                }
                finally
                {
                    as400con.Close();
                    //GC.Collect(); // Optional: use with caution
                }
                return intDataCnt;
            }
        }
    }
}