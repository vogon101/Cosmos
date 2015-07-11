using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cosmos.Debug
{
    partial class DebugController
    {
        private const int MaxBP = 256;
        private uint?[] mActiveBPs = new uint?[MaxBP];
        private bool mBreaking = false;

        //ASM Breakpoints stored as C# Address -> ASM Address, C# BP ID
        //Allows quick look-up on INT3 occurring
        private List<Tuple<UInt32, UInt32, int>> ASMBreakpoints = new List<Tuple<UInt32, UInt32, int>>();

        private List<KeyValuePair<UInt32, string>> mINT3sSet = new List<KeyValuePair<UInt32, string>>();

        public int SetBreakpoint(uint aAddress)
        {
            for (int xID = 0; xID < MaxBP; xID++)
            {
                if (mActiveBPs[xID] == null)
                {
                    mActiveBPs[xID] = aAddress;

                    var label = mDebugInfoDb.GetLabels(aAddress)[0];
                    mINT3sSet.Add(new KeyValuePair<uint, string>(aAddress, label));
                    mDbgConnector.SetBreakpoint(xID, aAddress);
                    return xID;
                }
            }

            throw new Exception(string.Format("Maximum number of active breakpoints exceeded! ({0})", MaxBP));
        }

        public void DeleteBreakpoint(int aRemoteId)
        {
            var xAddress = mActiveBPs[aRemoteId].Value;
            mActiveBPs[xAddress] = null;

            int index = mINT3sSet.FindIndex(x => x.Key == xAddress);
            mINT3sSet.RemoveAt(index);
            mDbgConnector.DeleteBreakpoint(aRemoteId);
        }

        public void SetAssemblerBreakpointAndContinueRunning(string aLabel)
        {
            UInt32 xAddress = GetAddressOfLabel(aLabel);
            mDbgConnector.SetAsmBreakpoint(xAddress);
            mDbgConnector.Continue();
        }

        public void SetAssemblerBreakpoint(uint aAddress)
        {
            var xSet = false;
            for (int xID = 0; xID < MaxBP; xID++)
            {
                if (mActiveBPs[xID] == null)
                {
                    UInt32 CSBPAddress = mDebugInfoDb.GetClosestCSharpBPAddress(aAddress);
                    ASMBreakpoints.Add(new Tuple<UInt32, UInt32, int>(CSBPAddress, aAddress, xID));

                    mActiveBPs[xID] = CSBPAddress;
                    var label = mDebugInfoDb.GetLabels(CSBPAddress)[0];
                    mINT3sSet.Add(new KeyValuePair<uint, string>(CSBPAddress, label));
                    mDbgConnector.SetBreakpoint(xID, CSBPAddress);

                    xSet = true;
                    break;
                }
            }
            if (!xSet)
            {
                throw new Exception("Maximum number of active breakpoints exceeded (" + MaxBP + ").");
            }
        }

        public List<Tuple<UInt32, UInt32, int>> GetAsmBreakpointInfoFromCSAddress(UInt32 csAddress)
        {
            return ASMBreakpoints.Where(x => x.Item1 == csAddress).ToList();
        }

        public Tuple<UInt32, UInt32, int> TryGetAsmBreakpointInfoFromAsmAddress(UInt32 asmAddress)
        {
            Tuple<UInt32, UInt32, int> result = null;

            var posBPs = ASMBreakpoints.Where(x => x.Item2 == asmAddress);
            if (posBPs.Any())
            {
                result = posBPs.First();
            }

            return result;
        }
        public void SetASMBreakpoint(UInt32 aAddress)
        {
            if (TryGetAsmBreakpointInfoFromAsmAddress(aAddress) == null)
            {
                bool set = false;
                SetAssemblerBreakpoint(aAddress);
            }
        }
        public void ClearASMBreakpoint(UInt32 aAddress)
        {
            var bp = TryGetAsmBreakpointInfoFromAsmAddress(aAddress);
            if (bp != null)
            {
                var xID = bp.Item3;
                int index = mINT3sSet.FindIndex(x => x.Key == bp.Item1);
                mINT3sSet.RemoveAt(index);
                mDbgConnector.DeleteBreakpoint(xID);
                mActiveBPs[xID] = null;
                ASMBreakpoints.Remove(bp);
            }
        }

        public void ActivateBoundBreakpoint(int aRemoteId, uint aAddress)
        {
            var label = GetLabels(aAddress)[0];
            mINT3sSet.Add(new KeyValuePair<uint, string>(aAddress, label));
            mDbgConnector.SetBreakpoint(aRemoteId, aAddress);
        }
        internal void SetINT3sOnCurrentMethod()
        {
            //Set all the CS Tracepoints to INT3 but without setting them like BPs
            //Don't bother setting existing CS BPs

            ChangeINT3sOnCurrentMethod(false);
        }
        internal void ClearINT3sOnCurrentMethod()
        {
            //Clear all the CS Tracepoints to NOP but without treating them like BPs
            //Don't clear existing/actual CS BPs

            ChangeINT3sOnCurrentMethod(true);
        }

        internal void ChangeINT3sOnCurrentMethod(bool clear)
        {
            if (CurrentAddress.HasValue)
            {
                var currMethod = mDebugInfoDb.GetMethod(CurrentAddress.Value);
                //Clear out the full list so we don't accidentally accumulate INT3s all over the place
                //Or set INT3s for all places in current method
                var tpAdresses = clear ? new List<KeyValuePair<UInt32, string>>(mINT3sSet.Count) : mDebugInfoDb.GetAllINT3AddressesForMethod(currMethod, true);
                //If we just do a stright assigment then we get a collection modified exception in foreach loop below
                if (clear)
                {
                    tpAdresses.AddRange(mINT3sSet);
                }

                var bpAddressessUnified = new List<UInt32>();
                bpAddressessUnified.AddRange(BreakpointManager.GetPendingBreakpointAddresses());
                bpAddressessUnified.AddRange(mActiveBPs.Select(x => x ?? 0));

                foreach (var addressInfo in tpAdresses)
                {
                    var address = addressInfo.Key;

                    //Don't set/clear actual BPs
                    if (!bpAddressessUnified.Contains(address))
                    {
                        int index = mINT3sSet.FindIndex(x => x.Key == address);
                        bool set = index != -1;

                        if (clear && set)
                        {
                            //Clear the INT3
                            mDbgConnector.ClearINT3(address);
                            mINT3sSet.RemoveAt(index);
                        }
                        else if (!clear && !set)
                        {
                            //Set the INT3
                            mDbgConnector.SetINT3(address);
                            mINT3sSet.Add(addressInfo);
                        }
                    }
                }
            }
        }

        public void CauseBreak()
        {
            mBreaking = true;
            SendBreakCmd();
        }
    }
}
