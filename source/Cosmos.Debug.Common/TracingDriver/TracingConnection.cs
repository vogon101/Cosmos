using System;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Cosmos.Debug.Common.TracingDriver
{
    public class TracingConnection : IDbConnection
    {
        private static readonly object mLogDirLock = new Object();
        public TracingConnection(string connectionString, string baseDir, out SQLiteConnection realConnection)
        {
            realConnection = mInnerConnection;
            mInnerConnection.ConnectionString = connectionString;
            lock (mLogDirLock)
            {
                for (int i = 0; i < Int32.MaxValue; i++)
                {
                    mBaseDir = Path.Combine(baseDir, i.ToString("X8"));
                    if (!Directory.Exists(mBaseDir))
                    {
                        Directory.CreateDirectory(mBaseDir);
                        break;
                    }
                    mBaseDir = null;
                }
            }


            //for (int i = 0; i < Int32.MaxValue; i++)
            //{
            //    try
            //    {
            //        mOut = new StreamWriter(new FileStream(Path.Combine(baseDir, i.ToString("X8").ToUpper() + ".log"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None), Encoding.UTF8, 1024, false);
            //        break;
            //    }
            //    catch
            //    {
            //        // try next one
            //    }
            //}
            //// fail-safe:
            //if (mOut == null)
            //{
            //    throw new InvalidOperationException("Unable to create log file!");
            //}
        }

        private readonly SQLiteConnection mInnerConnection = new SQLiteConnection();
        private readonly object mLockObj = new object();
        private long CurrentItem = 0;
        private StreamWriter mCurrentOut;
        private readonly string mBaseDir;
        private Stopwatch mStopwatch = new Stopwatch();

        internal void DoLogStart(string message)
        {
            if (!Monitor.TryEnter(mLockObj))
            {
                throw new InvalidOperationException("Multiple threads are accessing this connection!");
            }
            try
            {
                if (message == null)
                {
                    throw new ArgumentNullException("message");
                }

                var xId = Interlocked.Increment(ref CurrentItem);
                mCurrentOut = new StreamWriter(new FileStream(Path.Combine(mBaseDir, xId.ToString("X8")), FileMode.CreateNew));

                var xLines = message.Split(new string[] {"\r\n"}, StringSplitOptions.RemoveEmptyEntries);
                var xTimeString = DateTime.Now.ToString("HH:mm:ss.ffff");
                foreach (var xLine in xLines)
                {
                    mCurrentOut.Write(xTimeString);
                    mCurrentOut.Write(" ");
                    mCurrentOut.WriteLine(xLine);
                }
                mCurrentOut.WriteLine();
                mCurrentOut.WriteLine("Stack trace:");
                mCurrentOut.WriteLine(Environment.StackTrace);

                mCurrentOut.Flush();
                mStopwatch.Restart();
            }
            finally
            {
                Monitor.Exit(mLockObj);
            }
        }

        internal void DoLogEnd(string message)
        {
            if (!Monitor.TryEnter(mLockObj))
            {
                throw new InvalidOperationException("Multiple threads are accessing this connection!");
            }
            try
            {
                if (message == null)
                {
                    throw new ArgumentNullException("message");
                }
                mStopwatch.Stop();

                var xLines = message.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                var xTimeString = DateTime.Now.ToString("HH:mm:ss.ffff");
                xTimeString += " End " + Interlocked.Read(ref CurrentItem).ToString() + " ";
                foreach (var xLine in xLines)
                {
                    mCurrentOut.Write(xTimeString);
                    mCurrentOut.Write(" ");
                    mCurrentOut.WriteLine(xLine);
                }
                mCurrentOut.WriteLine();
                mCurrentOut.WriteLine("Took {0}", mStopwatch.Elapsed);
                mCurrentOut.Flush();
                mCurrentOut.Close();
                mCurrentOut = StreamWriter.Null;
            }
            finally
            {
                Monitor.Exit(mLockObj);
            }
        }

        public void Dispose()
        {
            mInnerConnection.Dispose();
        }

        public IDbTransaction BeginTransaction()
        {
            throw new System.NotImplementedException();
        }

        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            throw new System.NotImplementedException();
        }

        public void Close()
        {
            mInnerConnection.Close();
        }

        public void ChangeDatabase(string databaseName)
        {
            mInnerConnection.ChangeDatabase(databaseName);
        }

        public IDbCommand CreateCommand()
        {
            return new TracingCommand(mInnerConnection.CreateCommand(), this);
        }

        public void Open()
        {
            mInnerConnection.Open();
        }

        public string ConnectionString
        {
            get
            {
                return mInnerConnection.ConnectionString;
            }
            set
            {
                mInnerConnection.ConnectionString = value;
            }
        }

        public int ConnectionTimeout
        {
            get
            {
                return mInnerConnection.ConnectionTimeout;
            }
        }

        public string Database
        {
            get
            {
                return mInnerConnection.Database;
            }
        }

        public ConnectionState State
        {
            get
            {
                return mInnerConnection.State;
            }
        }
    }
}
