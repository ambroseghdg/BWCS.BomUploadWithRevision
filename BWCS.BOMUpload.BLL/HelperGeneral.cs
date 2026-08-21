using NLog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace BWCS.BOMUpload.BLL
{
    public static class HelperGeneral
    {
        private static Logger helplogger = LogManager.GetCurrentClassLogger();
        public static void SaveUserEnvInCookie(HttpResponseBase response, string urlKey, string environment)
        {
            var cookie = response.Cookies["EnvironmentCookie"] ?? new HttpCookie("EnvironmentCookie");
            cookie[urlKey] = environment;
            cookie.Expires = DateTime.Now.AddDays(60);
            cookie.HttpOnly = true;
            cookie.Secure = true;
            cookie.Path = "/";
            response.Cookies.Add(cookie);
        }

        public static string GetUserEnvironmentData(HttpRequestBase request, string urlKey)
        {
            var cookie = request.Cookies["EnvironmentCookie"];
            return cookie?[urlKey];
        }

        /// <summary>
        /// RecordBOMUploadToXAUserAction() - [DE_AppMetrics].[dbo].[AppLogins]  
        /// </summary>
        public static void RecordBOMUploadToXAUserAction(string strUserEmail, string userActivity, string itemNumber = "", string vaultName = "", string environmentname = "")
        {
            try
            {
                string xaServer = System.Configuration.ConfigurationManager.AppSettings["XASERVER"]; 
                string strConnection = Convert.ToString(ConfigurationManager.ConnectionStrings["SQLDBAppLog01"]); 
                string strEnvMachineName = Environment.MachineName.ToUpper();

                string loggedinUser_Email = strUserEmail;
                using (SqlConnection sqlConnPDM = new SqlConnection(strConnection))
                {
                    using (SqlCommand sqlCommPDM = new SqlCommand())
                    {
                        sqlConnPDM.Open();
                        sqlCommPDM.Connection = sqlConnPDM;
                        sqlCommPDM.CommandType = CommandType.StoredProcedure;
                        sqlCommPDM.CommandText = "dbo.usp_InsAppLogin"; 

                        sqlCommPDM.Parameters.Add("@LoginTime", SqlDbType.DateTime2).Value = DateTime.Now;
                        sqlCommPDM.Parameters.Add("@BwUSer", SqlDbType.NVarChar).Value = loggedinUser_Email;
                        sqlCommPDM.Parameters.Add("@Application", SqlDbType.NVarChar).Value = xaServer + "|BOM Upload to XA";
                        sqlCommPDM.Parameters.Add("@UserActivity", SqlDbType.NVarChar).Value = strEnvMachineName + "|" + userActivity;
                        sqlCommPDM.Parameters.Add("@ItemNumber", SqlDbType.NVarChar).Value = itemNumber;
                        sqlCommPDM.Parameters.Add("@VaultName", SqlDbType.NVarChar).Value = vaultName;
                        sqlCommPDM.Parameters.Add("@EnvironmentName", SqlDbType.NVarChar).Value = environmentname;

                        sqlCommPDM.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                helplogger.Error("Error in RecordBOMUploadToXAUserAction: " + ex.Message);
            }
        }
    }
}
