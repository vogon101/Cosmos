using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cosmos.Debug.Common;

namespace Cosmos.Debug
{
    public partial class DebugController: IDisposable
    {
        private DebugConnector mDbgConnector;
        private DebugInfo mDebugInfoDb;

        #region configuration

        public string DebugPort;
        public string DebugInfoPath;
        public string LaunchString;
        public string ISOFile;
        public BaseBreakpointManager BreakpointManager;

        #endregion configuration

        public object Owner;

        public void Initialize()
        {
            // todo: check config

            mDebugInfoDb = new DebugInfo(DebugInfoPath);
            mDebugInfoDb.LoadLookups();
            CreateDebugConnector();
            InitializeCache();
        }

        public void Dispose()
        {
            if (mDebugInfoDb != null)
            {
                mDebugInfoDb.Dispose();
                mDebugInfoDb = null;
            }
            if (mDbgConnector != null)
            {
                mDbgConnector.Dispose();
                mDbgConnector = null;
            }
        }

        /// <summary>Instanciate the <see cref="DebugConnector"/> that will handle communications
        /// between this debug engine hosted process and the emulation environment used to run the
        /// debugged Cosmos kernel. Actual connector to be instanciated is discovered from Cosmos
        /// project properties.</summary>
        private void CreateDebugConnector()
        {
            mDbgConnector = null;

            var xParts = (null == DebugPort) ? null : DebugPort.Split(' ');
            if ((null == xParts) || (2 > xParts.Length))
            {
                throw new Exception(string.Format("Unable to parse VS debug port: '{0}'", DebugPort));
            }
            string xPortType = xParts[0].ToLower();
            string xPortParam = xParts[1].ToLower();

            Debug("Starting debug connector.");
            switch (xPortType)
            {
                case "pipe:":
                    mDbgConnector = new Cosmos.Debug.Common.DebugConnectorPipeServer(xPortParam);
                    break;
                case "serial:":
                    if (LaunchString == "IntelEdison")
                    {
                        mDbgConnector = new Cosmos.Debug.Common.DebugConnectorEdison(xPortParam, Path.ChangeExtension(ISOFile, ".bin"));
                    }
                    else
                    {
                        mDbgConnector = new Cosmos.Debug.Common.DebugConnectorSerial(xPortParam);
                    }
                    break;
                default:
                    throw new Exception("No debug connector found for port type '" + xPortType + "'");

            }
            mDbgConnector.SetConnectionHandler(DbgConnectorConnected);
            mDbgConnector.CmdBreak += DbgCmdBreak;
            mDbgConnector.CmdTrace += DbgCmdTrace;
            mDbgConnector.CmdText += DbgCmdText;
            mDbgConnector.CmdStarted += DbgCmdStarted;
            mDbgConnector.OnDebugMsg += DbgDebugMsg;
            mDbgConnector.ConnectionLost += DbgConnector_ConnectionLost;
            mDbgConnector.CmdRegisters += DbgCmdRegisters;
            mDbgConnector.CmdFrame += DbgCmdFrame;
            mDbgConnector.CmdStack += DbgCmdStack;
            mDbgConnector.CmdPong += DbgCmdPong;
            mDbgConnector.CmdStackCorruptionOccurred += DbgCmdStackCorruptionOccurred;
            mDbgConnector.CmdNullReferenceOccurred += DbgCmdNullReferenceOccurred;
            mDbgConnector.CmdMessageBox += DbgCmdMessageBox;
            mDbgConnector.CmdChannel += DbgCmdChannel;
        }

        private readonly StreamWriter mOut = new StreamWriter(@"c:\data\CosmosDebugController.log", true);

        private void Debug(string message, params object[] args)
        {
            Debug(String.Format(message, args));
        }

        private void Debug(string message)
        {
            // for now:
            DbgCmdText("DEBUG: "+ message);

            var x = DateTime.Now.ToString("HH:mm:ss.ffffff ") + message;
            mOut.WriteLine(x);
            mOut.Flush();
        }
    }
}
