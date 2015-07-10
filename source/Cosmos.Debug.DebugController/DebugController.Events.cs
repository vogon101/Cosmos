using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cosmos.Debug
{
    partial class DebugController
    {

        public event Action<byte[]> RegistersReceived;

        protected virtual void OnRegistersReceived(byte[] obj)
        {
            var xHandler = RegistersReceived;
            if (xHandler != null)
            {
                xHandler(obj);
            }
        }

        public event Action<byte[]> FrameReceived;

        protected virtual void OnFrameReceived(byte[] obj)
        {
            var xHandler = FrameReceived;
            if (xHandler != null)
            {
                xHandler(obj);
            }
        }

        public event Action<byte[]> PongReceived;

        protected virtual void OnPongReceived(byte[] obj)
        {
            var xHandler = PongReceived;
            if (xHandler != null)
            {
                xHandler(obj);
            }
        }

        public event Action<byte[]> StackReceived;

        protected virtual void OnStackReceived(byte[] obj)
        {
            var xHandler = StackReceived;
            if (xHandler != null)
            {
                xHandler(obj);
            }
        }

        public event Action DebugConnectorConnected;

        protected virtual void OnDebugConnectorConnected()
        {
            var xHandler = DebugConnectorConnected;
            if (xHandler != null)
            {
                xHandler();
            }
        }

        public event Action<string> AssemblySource;

        protected virtual void OnAssemblySource(string obj)
        {
            var xHandler = AssemblySource;
            if (xHandler != null)
            {
                xHandler(obj);
            }
        }

        public event Action<IEnumerable<object>> BreakpointHit;

        protected virtual void OnBreakpointHit(IEnumerable<object> obj)
        {
            var xHandler = BreakpointHit;
            if (xHandler != null)
            {
                xHandler(obj);
            }
        }

        public event Action Break;

        protected virtual void OnBreak()
        {
            var xHandler = Break;
            if (xHandler != null)
            {
                xHandler();
            }
        }

        public event Action StepComplete;

        protected virtual void OnStepComplete()
        {
            var xHandler = StepComplete;
            if (xHandler != null)
            {
                xHandler();
            }
        }

        public event Action<uint> CmdTrace;

        protected virtual void OnCmdTrace(uint obj)
        {
            var xHandler = CmdTrace;
            if (xHandler != null)
            {
                xHandler(obj);
            }
        }

        public event Action<string> CmdText;

        protected virtual void OnCmdText(string obj)
        {
            var xHandler = CmdText;
            if (xHandler != null)
            {
                xHandler(obj);
            }
        }

        public event Action CmdStarted;

        protected virtual void OnCmdStarted()
        {
            var xHandler = CmdStarted;
            if (xHandler != null)
            {
                xHandler();
            }
        }

        public event Action<string> DebugMsg;

        protected virtual void OnDebugMsg(string obj)
        {
            Action<string> xHandler = DebugMsg;
            if (xHandler != null)
            {
                xHandler(obj);
            }
        }

        public event Action<Exception> ConnectionLost;

        protected virtual void OnConnectionLost(Exception obj)
        {
            var xHandler = ConnectionLost;
            if (xHandler != null)
            {
                xHandler(obj);
            }
        }

        public event Action<uint> StackCorruptionOccurred;

        protected virtual void OnStackCorruptionOccurred(uint obj)
        {
            var xHandler = StackCorruptionOccurred;
            if (xHandler != null)
            {
                xHandler(obj);
            }
        }

        public event Action<uint> NullReferenceOccurred;

        protected virtual void OnNullReferenceOccurred(uint obj)
        {
            var xHandler = NullReferenceOccurred;
            if (xHandler != null)
            {
                xHandler(obj);
            }
        }

        public event Action<string> MessageBox;

        protected virtual void OnMessageBox(string obj)
        {
            var xHandler = MessageBox;
            if (xHandler != null)
            {
                xHandler(obj);
            }
        }

        public event Action<byte, byte, byte[]> CmdChannel;

        protected virtual void OnCmdChannel(byte arg1, byte arg2, byte[] arg3)
        {
            var xHandler = CmdChannel;
            if (xHandler != null)
            {
                xHandler(arg1, arg2, arg3);
            }
        }
    }
}
