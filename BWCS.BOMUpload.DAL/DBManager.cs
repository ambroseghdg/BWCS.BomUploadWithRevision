using System;
using System.Configuration;
using System.Data;

namespace BWCS.BOMUpload.DAL
{
    public sealed class DBManager : IDBManager, IDisposable
    {
        private IDbConnection idbConnection;
        private IDataReader idataReader;
        private IDbCommand idbCommand;
        private DataProvider providerType;
        private IDbTransaction idbTransaction = null;
        private IDbDataParameter[] idbParameters = null;
        public string strConnection;

        public DBManager()
        {
            strConnection = Convert.ToString(ConfigurationManager.ConnectionStrings["SQLDB"]);
            providerType = DataProvider.SqlServer;
        }

        public DBManager(DataProvider providerType)
        {
            this.providerType = providerType;
        }

        public DBManager(DataProvider providerType, string connectionString)
        {
            this.providerType = providerType;
            this.strConnection = connectionString;
        }

        public IDbConnection Connection
        {
            get { return idbConnection; }
        }

        public IDataReader DataReader
        {
            get { return idataReader; }
            set { idataReader = value; }
        }

        public DataProvider ProviderType
        {
            get { return providerType; }
            set { providerType = value; }
        }

        public string ConnectionString
        {
            get { return strConnection; }
            set { strConnection = value; }
        }

        public IDbCommand Command
        {
            get { return idbCommand; }
        }

        public IDbTransaction Transaction
        {
            get { return idbTransaction; }
        }

        public IDbDataParameter[] Parameters
        {
            get { return idbParameters; }
        }


        /// <summary>
        /// This method is used to open the database connection.
        /// </summary>
        public void Open()
        {
            try
            {
                idbConnection = DBManagerFactory.GetConnection(this.providerType);
                idbConnection.ConnectionString = this.ConnectionString;
                if (idbConnection.State != ConnectionState.Open)
                    idbConnection.Open();
                this.idbCommand = DBManagerFactory.GetCommand(this.ProviderType);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        /// <summary>
        /// This method is used to close the database connection.
        /// </summary>
        public void Close()
        {
            try
            {
                if (idbConnection.State != ConnectionState.Closed)
                    idbConnection.Close();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        /// <summary>
        /// This method acts as a destructor to clear the objects/connections to garbage.
        /// </summary>
        public void Dispose()
        {
            try
            {
                GC.SuppressFinalize(this);
                this.Close();
                this.idbCommand = null;
                this.idbTransaction = null;
                this.idbConnection = null;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// This method is used to create the parameters based on the count provided as input.
        /// </summary>
        /// <param name="paramsCount"></param>

        public void CreateParameters(int paramsCount)
        {
            try
            {
                idbParameters = new IDbDataParameter[paramsCount];
                idbParameters = DBManagerFactory.GetParameters(this.ProviderType, paramsCount);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        /// <summary>
        /// This method is used to add the parameter and the values associated to idbDataParameter object.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="paramName"></param>
        /// <param name="objValue"></param>
        public void AddParameters(int index, string paramName, object objValue)
        {
            try
            {
                if (index < idbParameters.Length)
                {
                    idbParameters[index].ParameterName = paramName;
                    idbParameters[index].Value = objValue;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        /// <summary>
        /// This method is used to initiate the transaction.
        /// </summary>
        public void BeginTransaction()
        {
            try
            {
                if (this.idbTransaction == null)
                    idbTransaction = DBManagerFactory.GetTransaction(this.ProviderType);
                this.idbCommand.Transaction = idbTransaction;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        /// <summary>
        /// This method is used to commit the transaction.
        /// </summary>
        public void CommitTransaction()
        {
            try
            {
                if (this.idbTransaction != null)
                    this.idbTransaction.Commit();
                idbTransaction = null;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        /// <summary>
        /// This method is used to execute the stored procedure by passing the input parameters and return the data as datareader object.
        /// </summary>
        /// <param name="commandType"></param>
        /// <param name="commandText"></param>
        /// <returns>DataReader</returns>
        public IDataReader ExecuteReader(CommandType commandType, string commandText)
        {
            try
            {
                this.idbCommand = DBManagerFactory.GetCommand(this.ProviderType);
                idbCommand.Connection = this.Connection;
                PrepareCommand(idbCommand, this.Connection, this.Transaction, commandType, commandText, this.Parameters);
                this.DataReader = idbCommand.ExecuteReader();
                idbCommand.Parameters.Clear();
                return this.DataReader;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        /// <summary>
        /// This method is used to close the datareader object.
        /// </summary>
        public void CloseReader()
        {
            try
            {
                if (this.DataReader != null)
                    this.DataReader.Close();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        /// <summary>
        /// This method is used attach the parameters to the command object.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="commandParameters"></param>
        private void AttachParameters(IDbCommand command, IDbDataParameter[] commandParameters)
        {
            try
            {
                foreach (IDbDataParameter idbParameter in commandParameters)
                {
                    if ((idbParameter.Direction == ParameterDirection.InputOutput) && (idbParameter.Value == null))
                    {
                        idbParameter.Value = DBNull.Value;
                    }
                    command.Parameters.Add(idbParameter);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        /// <summary>
        /// This method is used to pass the values to the command and attach the parameters.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="connection"></param>
        /// <param name="transaction"></param>
        /// <param name="commandType"></param>
        /// <param name="commandText"></param>
        /// <param name="commandParameters"></param>
        private void PrepareCommand(IDbCommand command, IDbConnection connection, IDbTransaction transaction, CommandType commandType, string commandText, IDbDataParameter[] commandParameters)
        {
            try
            {
                command.Connection = connection;
                command.CommandText = commandText;
                command.CommandType = commandType;

                if (transaction != null)
                {
                    command.Transaction = transaction;
                }

                if (commandParameters != null)
                {
                    AttachParameters(command, commandParameters);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        /// <summary>
        /// This method is used to insert or update any data to the database.
        /// </summary>
        /// <param name="commandType"></param>
        /// <param name="commandText"></param>
        /// <returns>status as integer vallue</returns>
        public int ExecuteNonQuery(CommandType commandType, string commandText)
        {
            try
            {
                this.idbCommand = DBManagerFactory.GetCommand(this.ProviderType);
                PrepareCommand(idbCommand, this.Connection, this.Transaction, commandType, commandText, this.Parameters);
                int returnValue = idbCommand.ExecuteNonQuery();
                idbCommand.Parameters.Clear();
                return returnValue;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// This method is used to execute a stored procedure and return a single value .
        /// </summary>
        /// <param name="commandType"></param>
        /// <param name="commandText"></param>
        /// <returns>scalar value</returns>
        public object ExecuteScalar(CommandType commandType, string commandText)
        {
            try
            {
                this.idbCommand = DBManagerFactory.GetCommand(this.ProviderType);
                PrepareCommand(idbCommand, this.Connection, this.Transaction, commandType, commandText, this.Parameters);
                object returnValue = idbCommand.ExecuteScalar();
                idbCommand.Parameters.Clear();
                return returnValue;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        /// <summary>
        /// This method is used to get the data in the form of dataset by passing the input parameters such as commandType and commandText .
        /// </summary>
        /// <param name="commandType"></param>
        /// <param name="commandText"></param>
        /// <returns>Dataset object</returns>
        public DataSet ExecuteDataSet(CommandType commandType, string commandText)
        {
            try
            {
                this.idbCommand = DBManagerFactory.GetCommand(this.ProviderType);
                PrepareCommand(idbCommand, this.Connection, this.Transaction, commandType, commandText, this.Parameters);
                IDbDataAdapter dataAdapter = DBManagerFactory.GetDataAdapter(this.ProviderType);
                dataAdapter.SelectCommand = idbCommand;
                DataSet dataSet = new DataSet();
                dataAdapter.Fill(dataSet);
                idbCommand.Parameters.Clear();
                return dataSet;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}

