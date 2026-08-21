using BWCS.BOMUpload.BLL.Resource;
using NLog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Odbc; 
using System.IO;
using System.Linq;
using System.Management.Instrumentation;
using System.Net;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace BWCS.BOMUpload.BLL
{
    /// <summary>
    /// This class is a repository of XA operations.
    /// </summary>
    public class XASystemLink
    {
        #region Declarations

        /// <summary>
        /// Variable declarations. 
        /// </summary>
        readonly string XADBConnection = System.Configuration.ConfigurationManager.ConnectionStrings["XADB"].ToString();
        string strSLURL = System.Configuration.ConfigurationManager.AppSettings["XASLSRVLET"];
        string strXASystemRequest = string.Empty;
        string[] Parameters;
        string strRequest = string.Empty;
        string xaSystemLinkRequest = string.Empty;
        string xaAction = string.Empty;
        Logger logger = LogManager.GetCurrentClassLogger();

        #endregion

        #region Systemlink Process

        #region XA Login to Logout

        /// <summary>
        /// Login to XA
        /// </summary>
        /// <param name="userName">User credentials to connect XA.</param>
        /// <param name="password">Password of user to connect to XA.</param>
        /// <param name="MAXIdle">Maximum idle time allowed.</param>
        /// <param name="XAEnvironment">Environment where XA objects reside.</param>
        /// <param name="XAServer">Server on which XA resides.</param>
        /// <returns></returns>
        public string Login(string userName, string password, string maxIdle, string XAEnvironment, string XAServer)
        {
            Parameters = new string[] {
                userName,
                password,
                maxIdle,
                XAEnvironment,
                XAServer};
            return FormatXARequest(Resources.XALogin, Parameters);
        }

        /// <summary>
        /// Get the XA Session details.
        /// </summary>
        /// <returns></returns> 
        public string XASession(string userName, string password, string maxIdle, string xaEnvironment, string xaSystemName)

        {
            DataSet dsLogin = new DataSet();
            string strXASystemLogin = string.Empty;
            string strXASLSession = string.Empty;
            string strXAError = string.Empty;   
            try
            {
                logger.Info(" XASession(), Generating a login session to work with Systemlink");
                // Create Login Tag
                Parameters = new string[] {
                                                     userName
                                                  ,  password
                                                  ,  maxIdle
                                                  ,  xaEnvironment
                                                  ,  xaSystemName
                                             };
                strXASystemLogin = FormatXARequest(Resources.ResourceManager.GetString("XALogin"), Parameters);

                //Create Request Tag
                Parameters = new string[] {
                                               "*Current"
                                             ,   strXASystemRequest
                                            };

                strXASystemRequest = strXASystemLogin + FormatXARequest(strRequest, Parameters);

                // Create Systemlink Request ( XML )
                Parameters = new string[] {
                                               strXASystemRequest
                                             };
                xaSystemLinkRequest = FormatXARequest(Resources.ResourceManager.GetString("XASystemLinkRequest"), Parameters);

                // Execute the SystemLink Request
                dsLogin.ReadXml(new XmlNodeReader(FetchData(xaSystemLinkRequest)));
                if (dsLogin.Tables[1].TableName != null)
                {
                    if (dsLogin.Tables[0].TableName == "Exception")
                    {
                        strXASLSession = string.Empty;
                        if (dsLogin.Tables[0].Rows.Count >= 1)
                        {
                            strXAError = dsLogin.Tables[0].Rows[0][2].ToString();
                            strXAError += (strXAError.Length > 7 ? " | " : "") + dsLogin.Tables[0].Rows[0][0].ToString();
                            throw new Exception(strXAError);
                        }
                    }
                    else if (dsLogin.Tables[2].TableName == "SessionHandle")
                    {
                        strXASLSession = Convert.ToString(dsLogin.Tables[2].Rows[0][0]);
                    }
                    else if (dsLogin.Tables[2].TableName == "Exception")
                    {
                        strXASLSession = string.Empty;
                        strXAError = "The Systemlink request execution error";

                        if (dsLogin.Tables[2].Rows.Count > 0)
                            strXAError += " | " + dsLogin.Tables[2].Rows[0][0].ToString();

                        strXAError += " | Please contact your ERP team.";

                        throw new Exception(strXAError);
                    }
                }
            }
            catch (Exception)
            {
                logger.Error(" XASession(), Failed to create session to connect to XA via Systemlink.");
                throw;
            }
            return strXASLSession;
        }

        /// <summary>
        /// Systemlink Request Tag.
        /// </summary>
        /// <param name="XACurRequest"></param>
        /// <param name="XASession"></param>
        /// <returns></returns>
        public string RequestTag(string XACurRequest, string XASession)
        {
            Parameters = new string[] {
                    XASession,
                    XACurRequest};
            return FormatXARequest(Resources.XARequest, Parameters);
        }

        /// <summary>
        /// XA Systemlink Final Request.
        /// </summary>
        /// <param name="XACurRequest">Action to be carried out.</param>
        /// <returns></returns>
        public string CreateFinalSystemLinkRequest(string XACurRequest)
        {
            Parameters = new string[] {
                    XACurRequest};
            return FormatXARequest(Resources.XASystemLinkRequest, Parameters);
        }

        /// <summary>
        /// Format XA Request Parameters
        /// </summary>
        public string FormatXARequest(string resString, string[] parameters)
        {
            int paramcount = Parameters.Length;
            int icount;
            try
            {
                logger.Info(" FormatXARequest(), Formatting the XA request");
                for (icount = 0; icount <= paramcount - 1; icount++)
                {
                    resString = Convert.ToString(resString.Replace("{" + icount + "}", Parameters[icount]));
                }
            }
            catch (Exception ex)
            {
                logger.Error(" FormatXARequest(), Failed to format XA request.");
                throw ex;
            }
            return resString;
        }

        /// <summary>
        /// Fetch the data using Http Request
        /// </summary>
        public XmlDocument FetchData(string xaslRequest)
        {
            XmlDocument XMlResponse = new XmlDocument();
            XmlDocument xdSystemLinkResponse = new XmlDocument();

            try
            {
                logger.Info(" FetchData(), Fetch data from XA.");
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(strSLURL);
                request.Method = "POST";
                request.ContentType = "text/xml;";
                request.Timeout = 200000;
                UTF8Encoding encoding = new UTF8Encoding();
                byte[] postBytes = encoding.GetBytes(xaslRequest);
                request.ContentLength = postBytes.Length;
                Stream postStream = request.GetRequestStream();
                postStream.Write(postBytes, 0, postBytes.Length);
                postStream.Close();
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream r_stream = response.GetResponseStream();

                XMlResponse.Load(r_stream);
                r_stream.Close();
                r_stream.Dispose();
                response.Close();
            }
            catch (Exception ex)
            {
                logger.Error(" FetchData(), Failed to FetchData from XA.");
                throw ex;
            }

            return XMlResponse;
        }

        /// <summary>
        /// Logout from XA system.
        /// </summary>
        /// <param name="XASession">XALogOut Session to connect to XA.</param>
        /// <returns></returns>
        public string XALogOut(string XASession)
        {
            Parameters = new string[] {
                    XASession};
            return FormatXARequest(Resources.XALogOut, Parameters);
        }

        #endregion

        #region SystemLink Requests

        /// <summary>
        /// Execute the SystemLink Request.
        /// </summary>
        /// <param name="xaSession">Login session.</param>
        /// <param name="xaAction">Login or LogOff.</param>
        /// <returns>XA response - success or error message.</returns>
        public bool ExecuteSLRequest(string xaSession, string xaAction, string environment, ref string errorMessage, IList<Item> itemList, ref string actionResult)
        {

            string strXARequest = string.Empty;
            string nodeXml = string.Empty;

            XmlDocument xaXmlResponse = new XmlDocument();
            XmlNodeList xmlNodeList;
            string dictKey = string.Empty;
            bool blSuccess = true;
            Item itemData = new Item();

            try
            {
                logger.Info(" ExecuteSLRequest(), Process the Systemlink request." + Environment.NewLine);

                strXARequest = CreateFinalSystemLinkRequest(RequestTag(xaAction, xaSession));
                logger.Info("|XA Session: ["+ xaSession + "]|XA Action: ["+ xaAction + "]" + Environment.NewLine + "|XA Systemlink Final request: " + strXARequest); 
                xaXmlResponse = FetchData(strXARequest);
                logger.Info("Systemlink response : " + xaXmlResponse.InnerXml.ToString() + Environment.NewLine);

                xmlNodeList = xaXmlResponse.SelectNodes("/System-Link/Response/QueryListResponse");

                if (xmlNodeList != null)
                {
                    if (xmlNodeList.Count > 0)
                    {
                        XmlNode getNamedItem = xmlNodeList[0];
                        string strNamedItem = getNamedItem.Attributes.GetNamedItem("name").Value.ToString().ToUpper();

                        if (strNamedItem.Contains("QUERYLISTITEMREVISION_BOMUPLOADTOOL"))
                        {
                            blSuccess = GetItemData(xmlNodeList, ref errorMessage, itemList, xaAction);
                            // blSuccess = GetItemData(xmlNodeList, ref errorMessage, itemList);
                        }
                        else if (strNamedItem.Contains("QUERYLISTBILLOFMATERIALCOMPONENT_BOMUPLOAD01"))
                        {
                            blSuccess = GetSingleBOMData(xmlNodeList, environment, ref errorMessage, itemList);
                        }
                        else if (strNamedItem.Contains("QUERYLISTBILLOFMATERIAL_GENERAL"))  // Read BOM Header - Revision   
                        {
                            blSuccess = GetSingleBOMParentItemRevisionData(xmlNodeList, environment, ref errorMessage, itemList);
                        }
                    }
                }

                xmlNodeList = xaXmlResponse.SelectNodes("/System-Link/Response/UpdateResponse");
                foreach (XmlNode node in xmlNodeList)
                {
                    if (node.Attributes.GetNamedItem("name").Value.ToString().ToUpper().Contains("UPDATE"))
                    {
                        if ((node.Attributes.GetNamedItem("actionSucceeded").Value.ToString().ToUpper() == "FALSE"))
                        {
                            errorMessage = node.InnerText.ToString().Trim();

                            actionResult = errorMessage;

                            blSuccess = false;
                        }
                        else
                        {
                            actionResult = "Updated Successfully!";
                        }
                    }
                }

                xmlNodeList = xaXmlResponse.SelectNodes("/System-Link/Response/CreateResponse");
                foreach (XmlNode node in xmlNodeList)
                {
                    if (node.Attributes.GetNamedItem("name").Value.ToString().ToUpper().Contains("CREATE"))
                    {
                        if ((node.Attributes.GetNamedItem("actionSucceeded").Value.ToString().ToUpper() == "FALSE"))
                        {
                            errorMessage = node.InnerText.ToString().Trim();

                            actionResult = errorMessage;
                            blSuccess = false;
                        }
                        else
                        {
                            actionResult = "Created Successfully!";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(" ExecuteSLRequest(), Failed to process Systemlink Request.");
                throw ex;
            }

            return blSuccess;
        }

        private bool GetSingleBOMData(XmlNodeList xmlNodeList, string environment, ref string errorMessage, IList<Item> itemList)
        {
            string nodeXml = string.Empty;
            XmlDocument xaXmlResponse = new XmlDocument();
            bool blSuccess = true;
            int componentCount = 0;

            foreach (XmlNode node in xmlNodeList)
            {

                if ((node.Attributes.GetNamedItem("actionSucceeded").Value.ToString().ToUpper() == "FALSE"))
                {
                    errorMessage = node.InnerText.ToString().Trim();

                    blSuccess = false;
                }
                else
                {
                    nodeXml = node.OuterXml;

                    if (nodeXml.Contains("DomainEntity"))
                    {
                        string componentDescription = string.Empty;
                        string extendedDescription1 = string.Empty;
                        string extendedDescription2 = string.Empty;
                        string completeComponentDescription = string.Empty;
                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(nodeXml);

                        componentCount = doc.SelectNodes("QueryListResponse/DomainEntity").Count;

                        for (int i = 0; i < componentCount; i++)
                        {
                            Item itemData = new Item();

                            XmlNodeList siteNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='site']");
                            itemData.Site = siteNode[i].InnerText;

                            XmlNodeList parentItemNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='parentItem']");
                            itemData.ParentItemNumber = parentItemNode[i].InnerText;

                            XmlNodeList parentItemRevNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='parentItemRevision']");
                            itemData.ParentItemRevision = parentItemRevNode[i].InnerText;

                            XmlNodeList AltBOMIdNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='alternateBomId']");
                            itemData.AlternateBOMId = AltBOMIdNode[i].InnerText;

                            XmlNodeList componentItemNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='componentItem']");
                            itemData.ComponentItemNumber = componentItemNode[i].InnerText;

                            XmlNodeList componentNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='componentItem']");
                            itemData.ComponentItem = componentNode[i].InnerText;

                            XmlNodeList componentDescriptionNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='relatedComponentItemRevision.description']");
                            itemData.ComponentDescription = componentDescriptionNode[i].InnerText;
                            componentDescription = componentDescriptionNode[i].InnerText;

                            XmlNodeList revNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='componentItemRevision']");
                            itemData.Revision = revNode[i].InnerText;

                            XmlNodeList compRevNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='componentItemRevision']");
                            itemData.ComponentItemRevision = compRevNode[i].InnerText;

                            XmlNodeList tokenNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='token']");
                            itemData.MasterCode = tokenNode[i].InnerText;

                            XmlNodeList userSeqNode = null;
                            if (environment != "UU")
                                userSeqNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='userSequence1']");
                            else if (environment == "UU")
                                userSeqNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='userSequence2']");

                            itemData.UserSequence = userSeqNode[i].InnerText;

                            XmlNodeList qtyNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='quantityPerUnit']");
                            itemData.Qty = qtyNode[i].InnerText;

                            XmlNodeList uomNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='relatedComponentItemRevision.currentBasePriceUm']");
                            itemData.UnitOfMeasure = uomNode[i].InnerText;

                            // relatedComponentItemRevision.stockingUm - it included in all the ENV - SystemLink   
                            uomNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='relatedComponentItemRevision.stockingUm']");
                            itemData.UnitOfMeasure = uomNode[i].InnerText;

                            XmlNodeList itemTypeNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='relatedComponentItemRevision.itemType']");
                            itemData.ItemType = itemTypeNode[i].InnerText;

                            XmlNodeList extDesc1 = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='relatedComponentItemRevision.relatedItemRevisionPurchase.extendedDescription1']");
                            itemData.ExtendedDescrption1 = extDesc1[i].InnerText;
                            extendedDescription1 = extDesc1[i].InnerText;

                            XmlNodeList extDesc2 = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='relatedComponentItemRevision.relatedItemRevisionPurchase.extendedDescription2']");
                            itemData.ExtendedDescrption2 = extDesc2[i].InnerText;
                            extendedDescription2 = extDesc2[i].InnerText;

                            // ExtendedDescription add with PIPE | 
                            completeComponentDescription = componentDescription + (extendedDescription1.Trim().Length > 0 ? " | " : "") + extendedDescription1 + (extendedDescription2.Trim().Length > 0 ? " | " : "") + extendedDescription2;
                            itemData.CompleteComponentDescription = completeComponentDescription;

                            itemData.Environment = environment;

                            itemList.Add(itemData);

                        }
                    }
                }
            }
            return blSuccess;
        }

        private bool GetItemData(XmlNodeList xmlNoteList, ref string errorMessage, IList<Item> itemList)
        {
            string nodeXml = string.Empty;
            XmlDocument xaXmlResponse = new XmlDocument();
            bool blSuccess = true;
            Item itemData = new Item();

            foreach (XmlNode node in xmlNoteList)
            {
                if (node.Attributes.GetNamedItem("name").Value.ToString().ToUpper().Contains("QUERYLISTITEMREVISION_BOMUPLOADTOOL"))
                {
                    if ((node.Attributes.GetNamedItem("actionSucceeded").Value.ToString().ToUpper() == "FALSE"))
                    {
                        errorMessage = node.InnerText.ToString().Trim();

                        blSuccess = false;
                    }
                    else
                    {
                        string mfgno1, extendedDescription1, extendedDescription2, oepnix, userFieldText40, completeDescription = string.Empty;

                        nodeXml = node.OuterXml;

                        if (nodeXml.Contains("DomainEntity"))
                        {
                            XmlDocument doc = new XmlDocument();
                            doc.LoadXml(nodeXml);

                            XmlNodeList itemNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='item']");
                            itemData.ItemNumber = itemNode[0].InnerText;

                            XmlNodeList descNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='description']");
                            itemData.ItemDescription = descNode[0].InnerText;

                            XmlNodeList drawNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='drawingNumber']");
                            itemData.DrawingNumber = drawNode[0].InnerText;

                            //XmlNodeList mfgno1Node = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='relatedZ1786B280000678224288AAAAAAAAB00.mfgno1']");
                            //mfgno1 = mfgno1Node[0].InnerText;

                            XmlNodeList extDesc1Node = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='relatedItemRevisionPurchase.extendedDescription1']");
                            extendedDescription1 = extDesc1Node[0].InnerText;

                            XmlNodeList extDesc2Node = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='relatedItemRevisionPurchase.extendedDescription2']");
                            extendedDescription2 = extDesc2Node[0].InnerText;

                            XmlNodeList userField40Node = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='userFieldText40']");
                            userFieldText40 = userField40Node[0].InnerText;

                            XmlNodeList oepnixNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='relatedZ1786B280289768220314AAAAAAAAACZ.oepnix']");
                            oepnix = oepnixNode[0].InnerText;

                            XmlNodeList itemTypeNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='itemType']");
                            itemData.ItemType = itemTypeNode[0].InnerText;

                            XmlNodeList itemUoMNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='stockingUm']");
                            itemData.UnitOfMeasure = itemUoMNode[0].InnerText;

                            XmlNodeList parentItemRevNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='revision']");
                            itemData.ParentItemRevision = parentItemRevNode[0].InnerText;

                            //if (string.IsNullOrEmpty(mfgno1))
                            //{
                            //    completeDescription = descNode[0].InnerText + extendedDescription1 + extendedDescription2 + userFieldText40;
                            //}
                            //else
                            //{
                            //    completeDescription = descNode[0].InnerText + mfgno1 + oepnix;
                            //}
 
                            completeDescription = itemData.ItemDescription + (extendedDescription1.Trim().Length > 0 ? " | " : "") + extendedDescription1 + (extendedDescription2.Trim().Length > 0 ? " | " : "") + extendedDescription2;
                            itemData.CompleteDescription = completeDescription;
                        }
                        else
                        {
                            itemData.ItemNumber = string.Empty;
                        }

                        itemList.Add(itemData);
                    }
                }
            }
            return blSuccess;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlNoteList"></param>
        /// <param name="errorMessage"></param>
        /// <param name="itemList"></param>
        /// <param name="xaActionRevisionText"></param>
        /// <returns></returns>
        private bool GetItemData(XmlNodeList xmlNoteList, ref string errorMessage, IList<Item> itemList, string xaActionRevisionText)
        {
            string nodeXml = string.Empty;
            XmlDocument xaXmlResponse = new XmlDocument();
            bool blSuccess = true;
            Item itemData = new Item();

            foreach (XmlNode node in xmlNoteList)
            {
                if (node.Attributes.GetNamedItem("name").Value.ToString().ToUpper().Contains("QUERYLISTITEMREVISION_BOMUPLOADTOOL"))
                {
                    if ((node.Attributes.GetNamedItem("actionSucceeded").Value.ToString().ToUpper() == "FALSE"))
                    {
                        errorMessage = node.InnerText.ToString().Trim();

                        blSuccess = false;
                    }
                    else
                    {
                        string mfgno1, extendedDescription1, extendedDescription2, oepnix, userFieldText40, completeDescription = string.Empty;

                        nodeXml = node.OuterXml;

                        if (nodeXml.Contains("DomainEntity"))
                        {
                            XmlDocument doc = new XmlDocument();
                            doc.LoadXml(nodeXml);

                            XmlNodeList itemNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='item']");
                            itemData.ItemNumber = itemNode[0].InnerText;

                            XmlNodeList descNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='description']");
                            itemData.ItemDescription = descNode[0].InnerText;

                            XmlNodeList drawNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='drawingNumber']");
                            itemData.DrawingNumber = drawNode[0].InnerText;

                            //XmlNodeList mfgno1Node = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='relatedZ1786B280000678224288AAAAAAAAB00.mfgno1']");
                            //mfgno1 = mfgno1Node[0].InnerText;

                            XmlNodeList extDesc1Node = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='relatedItemRevisionPurchase.extendedDescription1']");
                            extendedDescription1 = extDesc1Node[0].InnerText;

                            XmlNodeList extDesc2Node = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='relatedItemRevisionPurchase.extendedDescription2']");
                            extendedDescription2 = extDesc2Node[0].InnerText;

                            XmlNodeList userField40Node = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='userFieldText40']");
                            userFieldText40 = userField40Node[0].InnerText;

                            XmlNodeList oepnixNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='relatedZ1786B280289768220314AAAAAAAAACZ.oepnix']");
                            oepnix = oepnixNode[0].InnerText;

                            XmlNodeList itemTypeNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='itemType']");
                            itemData.ItemType = itemTypeNode[0].InnerText;

                            XmlNodeList itemUoMNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='stockingUm']");
                            itemData.UnitOfMeasure = itemUoMNode[0].InnerText;

                            // check xaAction has and revision in systemlink's WHERE clause
                            XmlNodeList parentItemRevNode = null;
                            if ( xaActionRevisionText.Contains("and revision") )
                                parentItemRevNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='revision']");
                            else
                                parentItemRevNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='relatedCurrentItemRevision.currentRevision']");

                            if (parentItemRevNode != null && parentItemRevNode.Count > 0)
                                itemData.ParentItemRevision = parentItemRevNode[0].InnerText;

                            completeDescription = itemData.ItemDescription + (extendedDescription1.Trim().Length > 0 ? " | " : "") + extendedDescription1 + (extendedDescription2.Trim().Length > 0 ? " | " : "") + extendedDescription2;
                            itemData.CompleteDescription = completeDescription;
                        }
                        else
                        {
                            itemData.ItemNumber = string.Empty;
                        }

                        itemList.Add(itemData);
                    }
                }
            }
            return blSuccess;
        }

        private bool GetSingleBOMParentItemRevisionData(XmlNodeList xmlNodeList, string environment, ref string errorMessage, IList<Item> itemList)
        {
            string nodeXml = string.Empty;
            XmlDocument xaXmlResponse = new XmlDocument();
            bool blSuccess = true;
            int componentCount = 0;

            foreach (XmlNode node in xmlNodeList)
            {

                if ((node.Attributes.GetNamedItem("actionSucceeded").Value.ToString().ToUpper() == "FALSE"))
                {
                    errorMessage = node.InnerText.ToString().Trim();

                    blSuccess = false;
                }
                else
                {
                    nodeXml = node.OuterXml;

                    if (nodeXml.Contains("DomainEntity"))
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(nodeXml);

                        componentCount = doc.SelectNodes("QueryListResponse/DomainEntity").Count;

                        for (int i = 0; i < componentCount; i++)
                        {
                            Item itemData = new Item();


                            XmlNodeList itemNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='parentItem']");
                            itemData.ItemNumber = itemNode[0].InnerText; 

                            XmlNodeList siteNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='site']");
                            itemData.Site = siteNode[i].InnerText;

                            XmlNodeList parentItemNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='parentItem']");
                            itemData.ParentItemNumber = parentItemNode[i].InnerText;

                            XmlNodeList parentItemRevNode = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='parentItemRevision']");
                            itemData.ParentItemRevision = parentItemRevNode[i].InnerText;

                            XmlNodeList parentItemDesc = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='relatedItemRevision.description']");
                            itemData.ItemDescription = parentItemDesc[i].InnerText;
                            itemData.CompleteDescription = itemData.ItemDescription;

                            XmlNodeList parentItemType = doc.SelectNodes("QueryListResponse/DomainEntity/Property[@path='relatedItemRevision.itemType']");
                            itemData.ItemType = parentItemType[i].InnerText;

                            itemData.Environment = environment;

                            itemList.Add(itemData);

                        }
                    }
                }
            }

            return blSuccess;
        }

        #endregion

        #region Item and BOM changes

        /// <summary>
        /// Get item details with Latest Revision from XA if it exists. 
        /// </summary>
        /// <param name="itemNumber">User input.</param>
        /// <returns></returns>
        public string GetItemDetails(string itemNumber)
        {
            Parameters = new string[] {
                                        itemNumber
                                   };
            return FormatXARequest(Resources.GetItemDetailsCurRevision, Parameters);
        }

        /// <summary>
        /// GetParentItemRevisionByParentItemSite Header level 
        /// </summary>
        /// <param name="itemNumber"></param>
        /// <returns></returns>
        public string GetParentItemRevisionByParentItemSite(string itemNumber)
        {
            Parameters = new string[] {
                                        itemNumber
                                   };
            return FormatXARequest(Resources.GetParentItemRevisionByParentItemSite, Parameters);
        }
        
        /// <summary>
        /// Get item details with input Revision from XA if it exists.  
        /// </summary>
        /// <param name="itemNumber"></param>
        /// <returns></returns>
        public string GetItemDetailsByRevision(string itemNumber)
        {
            Parameters = new string[] { itemNumber  };
            return FormatXARequest(Resources.GetItemDetailsByRevision, Parameters);
        }

        /// <summary>
        /// Get Single Level BOM details from XA for the parent item number.
        /// </summary>
        /// <param name="itemNumber">Parent item number.</param>
        /// <returns>Systemlink request skeleton for Single level BOM.</returns>
        public string GetSingleLevelBOM(string itemNumber)
        {
            Parameters = new string[] {
                                        itemNumber
                                   };
            return FormatXARequest(Resources.GetSingleLevelBOM, Parameters);
        }
        public string GetSingleLevelBOMRevision(string itemNumber)
        {
            Parameters = new string[] { itemNumber }; 
            return FormatXARequest(Resources.GetSingleLevelBOMParentItemRevision, Parameters);
        }

        public string UUGetSingleLevelBOM(string itemNumber)
        {
            Parameters = new string[] {
                                        itemNumber
                                   };
            return FormatXARequest(Resources.UUGetSingleLevelBOM, Parameters);
        }

        public string CreateBOMHeader(string site, string parentItem, string parentItemDescription, string standardBatchQty)
        {
            Parameters = new string[] {
                                        site,
                                        parentItem,
                                        parentItemDescription,
                                        standardBatchQty
                                   };
            return FormatXARequest(Resources.CreateBOMHeader, Parameters);
        }

        /// <summary>
        /// Create BOM component.
        /// </summary>
        /// <param name="site">Site.</param>
        /// <param name="parentItem">Parent Item number.</param>
        /// <param name="parentItemRevision">Parent Item Revision.</param>
        /// <param name="alternateBOMId">Alternate BOM Id.</param>
        /// <param name="componentItem">Component Item Number.</param>
        /// <param name="componentItemRevision">Component Item Revision.</param>
        /// <param name="userSequence1">User Sequence.</param>
        /// <param name="quantityPerUnit">Quantity Per Unit.</param>
        /// <returns>Systemlink request to create BOM component.</returns>
        public string CreateBOMComponent(string site, string parentItem, string parentItemRevision, string alternateBOMId, string componentItem, string componentItemRevision, string userSequence1, string quantityPerUnit)
        {
            Parameters = new string[] {
                                        site,
                                        parentItem,
                                        parentItemRevision,
                                        alternateBOMId,
                                        componentItem,
                                        componentItemRevision,
                                        userSequence1,
                                        quantityPerUnit
                                   };
            return FormatXARequest(Resources.CreateBOMComponent, Parameters);
        }

        /// <summary>
        /// Create BOM component for ENV UU 
        /// </summary>
        /// <param name="site">Site.</param>
        /// <param name="parentItem">Parent Item number.</param>
        /// <param name="parentItemRevision">Parent Item Revision.</param>
        /// <param name="alternateBOMId">Alternate BOM Id.</param>
        /// <param name="componentItem">Component Item Number.</param>
        /// <param name="componentItemRevision">Component Item Revision.</param>
        /// <param name="userSequence2"> NO User Sequence Property - REMOVED from SystemLink.</param>
        /// <param name="quantityPerUnit">Quantity Per Unit.</param>
        /// <returns>Systemlink request to create BOM component.</returns>
        public string CreateBOMComponentUU(string site, string parentItem, string parentItemRevision, string alternateBOMId, string componentItem, string componentItemRevision, string userSequence2, string quantityPerUnit)
        {
            Parameters = new string[] {
                                        site,
                                        parentItem,
                                        parentItemRevision,
                                        alternateBOMId,
                                        componentItem,
                                        componentItemRevision,
                                        userSequence2,
                                        quantityPerUnit
                                   };
            return FormatXARequest(Resources.UUCreateBOMComponent, Parameters);
        }

        /// <summary>
        /// Update BOM component.
        /// </summary>
        /// <param name="site">Site.</param>
        /// <param name="parentItem">Parent Item number.</param>
        /// <param name="parentItemRevision">Parent Item Revision.</param>
        /// <param name="alternateBOMId">Alternate BOM Id.</param>
        /// <param name="componentItem">Component Item Number.</param>
        /// <param name="componentItemRevision">Component Item Revision.</param>
        /// <param name="token">Token.</param>
        /// <param name="userSequence1">User Sequence.</param>
        /// <param name="quantityPerUnit">Quantity Per Unit.</param>
        /// <returns>Systemlink request to update BOM component.</returns>
        public string UpdateBOMComponent(string site, string parentItem, string parentItemRevision, string alternateBOMId, string componentItem, string componentItemRevision, string token, string userSequence1, string quantityPerUnit)
        {
            Parameters = new string[] {
                                        site,
                                        parentItem,
                                        parentItemRevision,
                                        alternateBOMId,
                                        componentItem,
                                        componentItemRevision,
                                        token,
                                        userSequence1,
                                        quantityPerUnit
                                   };
            return FormatXARequest(Resources.UpdateBOMComponent, Parameters);
        }

        //UUUpdateBOMComponent
        /// <summary>
        /// Update BOM component for ENV UU 
        /// </summary>
        /// <param name="site">Site.</param>
        /// <param name="parentItem">Parent Item number.</param>
        /// <param name="parentItemRevision">Parent Item Revision.</param>
        /// <param name="alternateBOMId">Alternate BOM Id.</param>
        /// <param name="componentItem">Component Item Number.</param>
        /// <param name="componentItemRevision">Component Item Revision.</param>
        /// <param name="token">Token.</param>
        /// <param name="userSequence2">User Sequence.</param>
        /// <param name="quantityPerUnit">Quantity Per Unit.</param>
        /// <returns>Systemlink request to update BOM component.</returns>
        public string UpdateBOMComponentUU(string site, string parentItem, string parentItemRevision, string alternateBOMId, string componentItem, string componentItemRevision, string token, string userSequence2, string quantityPerUnit)
        {
            Parameters = new string[] {
                                        site,
                                        parentItem,
                                        parentItemRevision,
                                        alternateBOMId,
                                        componentItem,
                                        componentItemRevision,
                                        token,
                                        userSequence2,
                                        quantityPerUnit
                                   };
            return FormatXARequest(Resources.UUUpdateBOMComponent, Parameters);
        }

        /// <summary>
        /// Delete BOM component.
        /// </summary>
        /// <param name="site">Site.</param>
        /// <param name="parentItem">Parent Item number.</param>
        /// <param name="parentItemRevision">Parent Item Revision.</param>
        /// <param name="alternateBOMId">Alternate BOM Id.</param>
        /// <param name="componentItem">Component Item Number.</param>
        /// <param name="componentItemRevision">Component Item Revision.</param>
        /// <param name="token">Token.</param>
        /// <returns>Systemlink request to delete BOM component.</returns>
        public string DeleteBOMComponent(string site, string parentItem, string parentItemRevision, string alternateBOMId, string componentItem, string componentItemRevision, string token)
        {
            Parameters = new string[] {
                                        site,
                                        parentItem,
                                        parentItemRevision,
                                        alternateBOMId,
                                        componentItem,
                                        componentItemRevision,
                                        token
                                   };
            return FormatXARequest(Resources.DeleteBOMComponent, Parameters);
        }

        #endregion

        #endregion
    }
}
