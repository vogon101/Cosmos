using System;
using System.Data;
using System.Text;

namespace Cosmos.Debug.Common.TracingDriver
{
    public class TracingCommand: IDbCommand
    {
        private readonly IDbCommand mInternalCommand;
        private TracingConnection mConnection;

        public TracingCommand(IDbCommand internalCommand, TracingConnection connection)
        {
            if (internalCommand == null)
            {
                throw new ArgumentNullException("internalCommand");
            }
            if (connection == null)
            {
                throw new ArgumentNullException("connection");
            }
            mInternalCommand = internalCommand;
            mConnection = connection;
        }

        public void Dispose()
        {
            mInternalCommand.Dispose();
        }

        public void Prepare()
        {
            mInternalCommand.Prepare();
        }

        public void Cancel()
        {
            mInternalCommand.Cancel();
        }

        public IDbDataParameter CreateParameter()
        {
            return mInternalCommand.CreateParameter();
        }

        public int ExecuteNonQuery()
        {
            DumpQuery();
            var xResult = mInternalCommand.ExecuteNonQuery();
            QueryDone();
            return xResult;
        }

        public IDataReader ExecuteReader()
        {
            DumpQuery();
            var xResult = mInternalCommand.ExecuteReader();
            QueryDone();
            return xResult;
        }

        private void DumpQuery()
        {
            var xSB = new StringBuilder();
            xSB.AppendLine("--- Query: ");
            xSB.AppendLine(CommandText);
            if (mInternalCommand.Parameters.Count > 0)
            {
                xSB.AppendLine("--- Parameters:");
                foreach (IDataParameter xParam in mInternalCommand.Parameters)
                {
                    string xValueStr = xParam.Value.ToString();
                    if (xParam.Value is DBNull)
                    {
                        xValueStr = "**DBNull**";
                    }
                    else
                    {
                        xValueStr = "'" + xValueStr + "'";
                    }
                    xSB.AppendLine(String.Format("\t{0} ({1}) Value = '{2}'", xParam.ParameterName, xParam.DbType, xValueStr));
                }
            }
            mConnection.DoLogStart(xSB.ToString());
        }

        private void QueryDone()
        {
            mConnection.DoLogEnd("Query done");
        }

        public IDataReader ExecuteReader(CommandBehavior behavior)
        {
            DumpQuery();
            var xResult = mInternalCommand.ExecuteReader(behavior);
            QueryDone();
            return xResult;
        }

        public object ExecuteScalar()
        {
            DumpQuery();
            var xResult = mInternalCommand.ExecuteScalar();
            QueryDone();
            return xResult;
        }

        public IDbConnection Connection
        {
            get
            {
                return mConnection;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public IDbTransaction Transaction
        {
            get
            {
                throw new System.NotImplementedException();
            }
            set
            {
                throw new System.NotImplementedException();
            }
        }

        public string CommandText
        {
            get
            {
                return mInternalCommand.CommandText;
            }
            set
            {
                mInternalCommand.CommandText = value;
            }
        }

        public int CommandTimeout
        {
            get
            {
                return mInternalCommand.CommandTimeout;
            }
            set
            {
                mInternalCommand.CommandTimeout = value;
            }
        }

        public CommandType CommandType
        {
            get
            {
                return mInternalCommand.CommandType;
            }
            set
            {
                mInternalCommand.CommandType = value;
            }
        }

        public IDataParameterCollection Parameters
        {
            get
            {
                return mInternalCommand.Parameters;
            }
        }

        public UpdateRowSource UpdatedRowSource
        {
            get
            {
                return mInternalCommand.UpdatedRowSource;
            }
            set
            {
                mInternalCommand.UpdatedRowSource = value;
            }
        }
    }
}
