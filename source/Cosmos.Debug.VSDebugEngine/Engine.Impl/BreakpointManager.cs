using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Cosmos.Debug.Common;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Cosmos.Debug.VSDebugEngine {
    // This class manages breakpoints for the engine.
    public class BreakpointManager: BaseBreakpointManager {
        protected DebugConnector mDbgConnector;
        public List<AD7PendingBreakpoint> mPendingBPs = new List<AD7PendingBreakpoint>();
        private DebugController mController;

        public void SetDebugController(DebugController aController)
        {
            if (aController == null)
            {
                throw new ArgumentNullException("aController");
            }
            mController = aController;
        }

        // A helper method used to construct a new pending breakpoint.
        public void CreatePendingBreakpoint(IDebugBreakpointRequest2 pBPRequest, out IDebugPendingBreakpoint2 ppPendingBP) {
            var pendingBreakpoint = new AD7PendingBreakpoint(pBPRequest, (AD7Engine)mController.Owner, this);
            ppPendingBP = pendingBreakpoint;
            mPendingBPs.Add(pendingBreakpoint);
        }

        // Called from the engine's detach method to remove the debugger's breakpoint instructions.
        public void ClearBoundBreakpoints() {
            foreach (AD7PendingBreakpoint pendingBreakpoint in mPendingBPs) {
                pendingBreakpoint.ClearBoundBreakpoints();
            }
        }

        // Creates an entry and remotely enables the breakpoint in the debug stub
        public int RemoteEnable(AD7BoundBreakpoint aBBP)
        {
            return mController.SetBreakpoint(aBBP.mAddress);
        }

        public void RemoteDisable(AD7BoundBreakpoint aBBP)
        {
            mController.DeleteBreakpoint(aBBP.RemoteID);
        }

        public override uint[] GetPendingBreakpointAddresses()
        {
            var bps = mPendingBPs.Select(x => x.mBoundBPs).ToList();
            var bpAddressessUnified = new List<UInt32>();
            foreach (var bp in bps)
            {
                bpAddressessUnified.AddRange(bp.Select(x => x != null ? x.mAddress : 0));
            }
            return bpAddressessUnified.ToArray();
        }

        public override IEnumerable<KeyValuePair<uint, object>> GetBoundBreakpoints()
        {
            foreach (var xItem in mPendingBPs)
            {
                foreach (var xBound in xItem.mBoundBPs)
                {
                    yield return new KeyValuePair<uint, object>(xBound.mAddress, xBound);
                }
            }
        }
    }
}
