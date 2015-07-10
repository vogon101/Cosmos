using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Cosmos.Build.Common;
using Cosmos.Debug.Common;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.Win32;
using Label = Cosmos.Debug.Common.Label;

namespace Cosmos.Debug.VSDebugEngine
{
    public partial class AD7Process : IDebugProcess2
    {
        public Guid ID = Guid.NewGuid();
        protected EngineCallback mCallback;
        public AD7Thread mThread;
        protected AD7Engine mEngine;
        protected readonly NameValueCollection mDebugInfo;
        protected LaunchType mLaunch;

        protected int mProcessExitEventSent = 0;
        // Cached stack frame. See comments in AD7Thread regading this.
        public IEnumDebugFrameInfo2 mStackFrame;

        private ManualResetEvent StackDataUpdated = new ManualResetEvent(false);

        // These are static because we need them persistent between debug
        // sessions to avoid reconnection issues. But they are not created
        // until the debug session is ready the first time so that we know
        // the debug window pipes are already ready.
        //
        // Pipe for writing responses to communicate with Cosmos.VS.Windows
        static private Cosmos.Debug.Common.PipeClient mDebugDownPipe = null;
        // Pipe to receive messages from Cosmos.VS.Windows
        static private Cosmos.Debug.Common.PipeServer mDebugUpPipe = null;

        Host.Base mHost;

        public string mISO;
        public string mProjectFile;

        private void DebugConnectorConnected()
        {
            OutputText("Connected to DebugStub.");
        }

        private void DbgCmdChannel(byte aChannel, byte aCommand, byte[] aData)
        {
            mDebugDownPipe.SendRawToChannel(aChannel, aCommand, aData);
        }

        private void DbgCmdStackCorruptionOccurred(uint lastEIPAddress)
        {
            MessageBox.Show(String.Format("Stack corruption occurred at address 0x{0:X8}! Halting now.", lastEIPAddress));
        }

        private void DbgCmdNullReferenceOccurred(uint lastEIPAddress)
        {
            MessageBox.Show(String.Format("NullReferenceException occurred at address 0x{0:X8}! Halting now.", lastEIPAddress));
        }

        private void DbgCmdMessageBox(string message)
        {
            MessageBox.Show("Message from your Cosmos operating system:\r\n\r\n" + message);
        }

        internal AD7Process(NameValueCollection aDebugInfo, EngineCallback aCallback, AD7Engine aEngine, IDebugPort2 aPort)
        {
            mCallback = aCallback;
            mDebugInfo = aDebugInfo;

            mLaunch = (LaunchType)Enum.Parse(typeof(LaunchType), aDebugInfo[BuildProperties.LaunchString]);

            if (mDebugDownPipe == null)
            {
                mDebugDownPipe = new Cosmos.Debug.Common.PipeClient(Pipes.DownName);

                mDebugUpPipe = new Cosmos.Debug.Common.PipeServer(Pipes.UpName);
                mDebugUpPipe.DataPacketReceived += mDebugUpPipe_DataPacketReceived;
                mDebugUpPipe.Start();
            }
            else
            {
                mDebugUpPipe.CleanHandlers();
                mDebugUpPipe.DataPacketReceived += mDebugUpPipe_DataPacketReceived;
            }

            // Must be after mDebugDownPipe is initialized
            OutputClear();
            OutputText("Debugger process initialized.");

            mISO = mDebugInfo["ISOFile"];
            OutputText("Using ISO file " + mISO + ".");
            mProjectFile = mDebugInfo["ProjectFile"];
            //
            bool xUseGDB = string.Equals(mDebugInfo[BuildProperties.EnableGDBString], "true", StringComparison.InvariantCultureIgnoreCase);
            OutputText("GDB " + (xUseGDB ? "Enabled" : "Disabled") + ".");
            //
            var xGDBClient = false;
            Boolean.TryParse(mDebugInfo[BuildProperties.StartCosmosGDBString], out xGDBClient);

            switch (mLaunch)
            {
                case LaunchType.VMware:
                    mHost = new Host.VMware(mDebugInfo, xUseGDB);
                    break;
                case LaunchType.Slave:
                    mHost = new Host.Slave(mDebugInfo, xUseGDB);
                    break;
                case LaunchType.Bochs:
                    // The project has been created on another machine or Bochs has been uninstalled since the project has
                    // been created.
                    if (!BochsSupport.BochsEnabled) { throw new Exception(ResourceStrings.BochsIsNotInstalled); }
                    string bochsConfigurationFileName = mDebugInfo[BuildProperties.BochsEmulatorConfigurationFileString];
                    if (string.IsNullOrEmpty(bochsConfigurationFileName))
                    {
                        bochsConfigurationFileName = BuildProperties.BochsDefaultConfigurationFileName;
                    }
                    if (!Path.IsPathRooted(bochsConfigurationFileName))
                    {
                        // Assume the configuration file name is relative to project output path.
                        bochsConfigurationFileName = Path.Combine(new FileInfo(mDebugInfo["ProjectFile"]).Directory.FullName, mDebugInfo["OutputPath"], bochsConfigurationFileName);
                    }
                    FileInfo bochsConfigurationFile = new FileInfo(bochsConfigurationFileName);
                    // TODO : What if the configuration file doesn't exist ? This will throw a FileNotFoundException in
                    // the Bochs class constructor. Is this appropriate behavior ?
                    mHost = new Host.Bochs(mDebugInfo, xUseGDB, bochsConfigurationFile);

                    //((Host.Bochs)mHost).FixBochsConfiguration(new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("IsoFileName", mISO) });
                    break;
                case LaunchType.IntelEdison:
                    mHost = new Host.IntelEdison(mDebugInfo, false);
                    break;
                default:
                    throw new Exception("Invalid Launch value: '" + mLaunch + "'.");
            }
            mHost.OnShutDown += HostShutdown;

            mEngine = aEngine;

            CreateDebugController();

            mThread = new AD7Thread(aEngine, this);
            mCallback.OnThreadStart(mThread);
            mPort = aPort;

            if (xUseGDB && xGDBClient)
            {
                LaunchGdbClient();
            }
        }


        public DebugController DbgController;
        private void CreateDebugController()
        {
            DbgController = new DebugController();
            string xPort = mDebugInfo[BuildProperties.VisualStudioDebugPortString];

            if (String.IsNullOrWhiteSpace(xPort))
            {
                xPort = mDebugInfo[BuildProperties.CosmosDebugPortString];
            }

            DbgController.DebugPort = xPort;

            string xDbPath = Path.ChangeExtension(mISO, "cdb");
            if (!File.Exists(xDbPath))
            {
                throw new Exception("Debug data file " + xDbPath + " not found. Could be a omitted build process of Cosmos project so that it has not been created.");
            }
            DbgController.DebugInfoPath = xDbPath;
            DbgController.ISOFile = mISO;

            DbgController.AssemblySource += DbgControllerOnAssemblySource;
            DbgController.Break += DbgControllerOnBreak;
            DbgController.BreakpointHit += DbControllerBreakpointHit;
            DbgController.CmdChannel += DbgCmdChannel;
            DbgController.CmdStarted += DbgCmdStarted;
            DbgController.CmdText += DbgCmdText;
            DbgController.CmdTrace += DbgCmdTrace;
            DbgController.ConnectionLost += DbgConnector_ConnectionLost;
            DbgController.DebugMsg += DebugMsg;
            DbgController.DebugConnectorConnected += DebugConnectorConnected;
            DbgController.FrameReceived += DbgControllerOnFrameReceived;
            DbgController.MessageBox += DbgCmdMessageBox;
            DbgController.NullReferenceOccurred += DbgCmdNullReferenceOccurred;
            DbgController.PongReceived += DbgControllerOnPongReceived;
            DbgController.RegistersReceived += DbgControllerOnRegistersReceived;
            DbgController.StepComplete += DbgControllerStepComplete;
            DbgController.StackCorruptionOccurred += DbgCmdStackCorruptionOccurred;
            DbgController.StackReceived += DbgControllerOnStackReceived;

            DbgController.BreakpointManager = mEngine.BPMgr;
            DbgController.Owner = mEngine;
            DbgController.Initialize();

            mEngine.BPMgr.SetDebugController(DbgController);
        }

        private void DbControllerBreakpointHit(IEnumerable<object> obj)
        {
            // Found a bound breakpoint
            mCallback.OnBreakpoint(mThread, obj.Cast<IDebugBoundBreakpoint2>().ToList());
        }

        private void DbgControllerStepComplete()
        {
            mCallback.OnStepComplete();
            mStackFrame = null;
        }

        private void DbgControllerOnBreak()
        {
            mStackFrame = null;
            mCallback.OnBreak(mThread);
        }

        private void DbgControllerOnAssemblySource(string aCode)
        {
            mDebugDownPipe.SendCommand(Debugger2Windows.AssemblySource, Encoding.UTF8.GetBytes(aCode.ToString()));
        }

        private void DbgControllerOnFrameReceived(byte[] bytes)
        {
            mDebugDownPipe.SendCommand(Debugger2Windows.Frame, bytes);
        }

        private void DbgControllerOnStackReceived(byte[] bytes)
        {
            mDebugDownPipe.SendCommand(Debugger2Windows.Stack, bytes);
        }

        private void DbgControllerOnPongReceived(byte[] bytes)
        {
            mDebugDownPipe.SendCommand(Debugger2Windows.PongDebugStub, bytes);
        }

        private void DbgControllerOnRegistersReceived(byte[] bytes)
        {
            mDebugDownPipe.SendCommand(Debugger2Windows.Registers, bytes);
        }

        private void mDebugUpPipe_DataPacketReceived(ushort aCmd, byte[] aData)
        {
            try
            {
                if (aCmd <= 127)
                {
                    switch (aCmd)
                    {
                        case Windows2Debugger.Noop:
                            // do nothing
                            break;

                        case Windows2Debugger.PingVSIP:
                            mDebugDownPipe.SendCommand(Debugger2Windows.PongVSIP);
                            break;

                        case Windows2Debugger.PingDebugStub:
                            DbgController.PingDebugStub();
                            break;

                        case Windows2Debugger.SetAsmBreak:
                            {
                                string xLabel = Encoding.UTF8.GetString(aData);
                                DbgController.SetAssemblerBreakpoint(xLabel);
                            }
                            break;

                        case Windows2Debugger.ToggleAsmBreak2:
                            {
                                string xLabel = Encoding.UTF8.GetString(aData);
                                UInt32 xAddress = DbgController.GetAddressOfLabel(xLabel);
                                if (DbgController.TryGetAsmBreakpointInfoFromAsmAddress(xAddress) == null)
                                {
                                    DbgController.SetASMBreakpoint(xAddress);
                                }
                                else
                                {
                                    DbgController.ClearASMBreakpoint(xAddress);
                                }
                                break;
                            }
                        case Windows2Debugger.ToggleStepMode:
                            DbgController.ToggleStepMode();
                            break;

                        case Windows2Debugger.SetStepModeAssembler:
                            DbgController.SetStepModeAssembler();
                            break;

                        case Windows2Debugger.SetStepModeSource:
                            DbgController.SetStepModeSource();
                            break;

                        case Windows2Debugger.CurrentASMLine:
                            {
                                DbgController.SetCurrentLine(Encoding.UTF8.GetString(aData));
                                break;
                            }
                        case Windows2Debugger.NextASMLine1:
                            {
                                if (aData.Length == 0)
                                {
                                    DbgController.SetNextLine(null);
                                }
                                else
                                {
                                    DbgController.SetNextLine(Encoding.UTF8.GetString(aData));
                                }
                                break;
                            }
                        case Windows2Debugger.NextLabel1:
                            {
                                string nextLabel = Encoding.UTF8.GetString(aData);
                                DbgController.SetNextAddress(nextLabel);
                                break;
                            }
                        //cmd used from assembler window
                        case Windows2Debugger.Continue:
                            Step(enum_STEPKIND.STEP_OVER);
                            break;
                        //cmd used from assembler window
                        case Windows2Debugger.AsmStepInto:
                            Step(enum_STEPKIND.STEP_INTO);
                            break;
                        default:
                            throw new Exception(String.Format("Command value '{0}' not supported in method AD7Process.mDebugUpPipe_DataPacketReceived.", aCmd));
                    }
                }
                else
                {
                    throw new NotImplementedException("Sending other channels not yet supported!");
                }
            }
            catch (Exception ex)
            {
                //We cannot afford to silently break the pipe!
                OutputText("AD7Process UpPipe receive error! " + ex.Message);
                System.Diagnostics.Debug.WriteLine("AD7Process UpPipe receive error! " + ex.ToString());
            }
        }


        protected void LaunchGdbClient()
        {
            OutputText("Launching GDB client.");
            if (File.Exists(Cosmos.Build.Common.CosmosPaths.GdbClientExe))
            {
                var xPSInfo = new ProcessStartInfo(Cosmos.Build.Common.CosmosPaths.GdbClientExe);
                xPSInfo.Arguments = "\"" + Path.ChangeExtension(mProjectFile, ".cgdb") + "\"" + @" /Connect";
                xPSInfo.UseShellExecute = false;
                xPSInfo.RedirectStandardInput = false;
                xPSInfo.RedirectStandardError = false;
                xPSInfo.RedirectStandardOutput = false;
                xPSInfo.CreateNoWindow = false;
                Process.Start(xPSInfo);
            }
            else
            {
                MessageBox.Show(string.Format(
                    "The GDB-Client could not be found at \"{0}\". Please deactivate it under \"Properties/Debug/Enable GDB\"",
                    Cosmos.Build.Common.CosmosPaths.GdbClientExe), "GDB-Client", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
            }
        }

        private void DbgConnector_ConnectionLost(Exception e)
        {
            if (Interlocked.CompareExchange(ref mProcessExitEventSent, 1, 0) == 1)
            {
                return;
            }
            if (DbgController != null)
            {
                mEngine.Callback.OnProcessExit(0);
            }
        }

        // Shows a message in the output window of VS. Needs special treatment,
        // because normally VS only shows msgs from debugged process, not internal
        // stuff like us.
        public void DebugMsg(string aMsg)
        {
            mCallback.OnOutputString(aMsg + "\n");
        }

        protected void DbgCmdStarted()
        {
            OutputText("DebugStub handshake completed.");
            DebugMsg("RmtDbg: Started");

            // OK, now debugger is ready. Send it a list of breakpoints that were set before
            // program run.
            foreach (var xBP in mEngine.BPMgr.mPendingBPs)
            {
                foreach (var xBBP in xBP.mBoundBPs)
                {
                    DbgController.ActivateBoundBreakpoint(xBBP.RemoteID, xBBP.mAddress);
                }
            }
            DbgController.SendBatchEndCmd();
        }

        void DbgCmdText(string obj)
        {
            mCallback.OnOutputStringUser(obj + "\r\n");
        }

        internal AD7Thread Thread
        {
            get
            {
                return mThread;
            }
        }

        void DbgCmdTrace(UInt32 aAddress)
        {
            DebugMsg("TraceReceived: " + aAddress);
        }

        public int Attach(IDebugEventCallback2 pCallback, Guid[] rgguidSpecificEngines, uint celtSpecificEngines, int[] rghrEngineAttach)
        {
            Trace.WriteLine(new StackTrace(false).GetFrame(0).GetMethod().GetFullName());
            throw new NotImplementedException();
        }

        public int CanDetach()
        {
            throw new NotImplementedException();
        }

        public int CauseBreak()
        {
            DbgController.CauseBreak();
            return VSConstants.S_OK;
        }

        public int Detach()
        {
            throw new NotImplementedException();
        }

        public int EnumPrograms(out IEnumDebugPrograms2 ppEnum)
        {
            throw new NotImplementedException();
        }

        public int EnumThreads(out IEnumDebugThreads2 ppEnum)
        {
            var xEnum = new AD7ThreadEnum(new IDebugThread2[] { mThread });
            ppEnum = xEnum;
            return VSConstants.S_OK;
        }

        public int GetAttachedSessionName(out string pbstrSessionName)
        {
            throw new NotImplementedException();
        }

        public int GetInfo(enum_PROCESS_INFO_FIELDS Fields, PROCESS_INFO[] pProcessInfo)
        {
            throw new NotImplementedException();
        }

        public int GetName(enum_GETNAME_TYPE gnType, out string pbstrName)
        {
            throw new NotImplementedException();
        }

        public readonly Guid PhysID = Guid.NewGuid();
        public int GetPhysicalProcessId(AD_PROCESS_ID[] pProcessId)
        {
            // http://blogs.msdn.com/b/jacdavis/archive/2008/05/01/what-to-do-if-your-debug-engine-doesn-t-create-real-processes.aspx
            // http://social.msdn.microsoft.com/Forums/en/vsx/thread/fe809686-e5f9-439d-9e52-00017e12300f
            pProcessId[0].guidProcessId = PhysID;
            pProcessId[0].ProcessIdType = (uint)enum_AD_PROCESS_ID.AD_PROCESS_ID_GUID;

            return VSConstants.S_OK;
        }

        private IDebugPort2 mPort = null;
        public int GetPort(out IDebugPort2 ppPort)
        {
            if (mPort == null)
            {
                throw new Exception("Error");
            }
            ppPort = mPort;
            return VSConstants.S_OK;
        }

        public int GetProcessId(out Guid pguidProcessId)
        {
            pguidProcessId = ID;
            return VSConstants.S_OK;
        }

        public int GetServer(out IDebugCoreServer2 ppServer)
        {
            throw new NotImplementedException();
        }

        public int Terminate()
        {
            OutputText("Debugger terminating.");

            try
            {
                mHost.Stop();

                OutputText("Debugger terminated.");
                return VSConstants.S_OK;
            }
            catch (ApplicationException)
            {
                OutputText("Failed to stop debugger!");
                OutputText("\r\n");
                OutputText("You need to install the VMWare VIX API!");

                MessageBox.Show("Failed to stop debugger! You need to install the VMWare VIX API!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                OutputText("Failed to stop debugger! Exception message: " + ex.Message);
                OutputText("\r\n");
                OutputText("You probably need to install the VMWare VIX API!");

                MessageBox.Show("Failed to stop debugger! You probably need to install the VMWare VIX API!\r\n\r\nCheck Output window for more details.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return VSConstants.E_FAIL;
        }

        internal void ResumeFromLaunch()
        {
            mHost.Start();
        }

        void HostShutdown(object sender, EventArgs e)
        {
            //AD7ThreadDestroyEvent.Send(mEngine, mThread, (uint)mProcess.ExitCode);
            //mCallback.OnProgramDestroy((uint)mProcess.ExitCode);

            // We dont use process info any more, but have to call this to tell
            // VS to stop debugging.
            if (Interlocked.CompareExchange(ref mProcessExitEventSent, 1, 0) == 0)
            {
                try
                {
                    mCallback.OnProcessExit(0);
                }
                catch
                {
                    // swallow exceptions here?
                }
            }

            if (DbgController != null)
            {
                DbgController.Dispose();
                DbgController = null;
            }
        }

        internal void Step(enum_STEPKIND aKind)
        {
            if (aKind == enum_STEPKIND.STEP_INTO)
            { // F11
                DbgController.StepInto();
            }
            else if (aKind == enum_STEPKIND.STEP_OVER)
            { // F10
                DbgController.StepOver();
            }
            else if (aKind == enum_STEPKIND.STEP_OUT)
            { // Shift-F11
                DbgController.StepOut();
            }
            else if (aKind == enum_STEPKIND.STEP_BACKWARDS)
            {
                // STEP_BACKWARDS - Supported at all by VS?
                //
                // Possibly, by dragging the execution location up
                // or down through the source code? -Orvid
                MessageBox.Show("Step backwards is not supported.");
                mCallback.OnStepComplete(); // Have to call this otherwise VS gets "stuck"
            }
            else
            {
                MessageBox.Show("Unknown step type requested.");
                mCallback.OnStepComplete(); // Have to call this otherwise VS gets "stuck"
            }
        }

        //TODO: At some point this will probably need to be exposed for access outside of AD7Process
        protected void OutputText(string aText)
        {
            // With Bochs this method may be invoked before the pipe is created.
            if (null == mDebugDownPipe) { return; }
            mDebugDownPipe.SendCommand(Debugger2Windows.OutputPane, Encoding.UTF8.GetBytes(aText + "\r\n"));
        }

        protected void OutputClear()
        {
            // With Bochs this method may be invoked before the pipe is created.
            if (null == mDebugDownPipe) { return; }
            mDebugDownPipe.SendCommand(Debugger2Windows.OutputClear);
        }
    }
}
