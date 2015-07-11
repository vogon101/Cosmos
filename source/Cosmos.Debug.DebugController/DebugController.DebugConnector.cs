using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cosmos.Debug.Common;

namespace Cosmos.Debug
{
    partial class DebugController
    {
        public uint? CurrentAddress
        {
            get;
            private set;
        }
        protected void DbgCmdRegisters(byte[] aData)
        {
            Debug("DbgCmdRegisters. aData = {" + aData.Aggregate("", (a, b) => a + " " + b.ToString("X2").ToUpper()) + "}");
            OnRegistersReceived(aData);

            if (aData.Length < 40)
            {
                CurrentAddress = null;
            }
            else
            {
                UInt32 x32 = (UInt32)
                    (aData[39] << 24 |
                     aData[38] << 16 |
                     aData[37] << 8 |
                     aData[36]);
                CurrentAddress = x32;
                //SendAssembly(true);
            }
        }

        protected void DbgCmdFrame(byte[] aData)
        {
            OnFrameReceived(aData);
        }

        protected void DbgCmdPong(byte[] aData)
        {
            OnPongReceived(aData);
        }

        protected void DbgCmdStack(byte[] aData)
        {
            OnStackReceived(aData);
        }

        private void DbgConnectorConnected()
        {
            OnDebugConnectorConnected();
        }

        private void DbgCmdBreak(uint aAddress)
        {
            // aAddress will be actual address. Call and other methods push return to (after op), but DS
            // corrects for us and sends us actual op address.
            Debug("DbgCmdBreak " + aAddress + " / " + aAddress.ToString("X8").ToUpper());

            if (mASMSteppingOut)
            {
                string[] currentASMLabels = GetLabels(aAddress);
                foreach (string aLabel in currentASMLabels)
                {
                    if (aLabel.Contains("END__OF__METHOD_EXCEPTION__2"))
                    {
                        mASMSteppingOut_NumEndMethodLabelsPassed++;
                        break;
                    }
                }
                if (mASMSteppingOut_NumEndMethodLabelsPassed >= 2)
                {
                    mASMSteppingOut = false;
                }
                new System.Threading.Tasks.Task(() => SendStepIntoCmd()).Start();
            }
            else
            {
                bool fullUpdate = true;


                var xActionPoints = new List<object>();
                var xBoundBreakpoints = new List<object>();

                if (!mBreaking)
                {
                    // Search the BPs and find ones that match our address.
                    foreach (var xBP in BreakpointManager.GetBoundBreakpoints())
                    {
                        if (xBP.Key == aAddress)
                            {
                                xBoundBreakpoints.Add(xBP.Value);
                            }
                    }
                }

                CurrentAddress = aAddress;
                mCurrentASMLine = null;
                if (xBoundBreakpoints.Count == 0)
                {
                    // If no matching breakpoints are found then its one of the following:
                    //   - VS Break
                    //   - Stepping operation
                    //   - Asm break


                    //We _must_ respond to the VS commands via callback if VS is waiting on one so check this first...
                    if (mBreaking)
                    {
                        OnBreak();
                        mBreaking = false;
                    }
                    else if (mStepping)
                    {
                        OnStepComplete();
                        mStepping = false;
                    }
                    else
                    {
                        //Check if current address is the ASM BP we might be looking for
                        if (ASMBPToStepTo != null && ASMBPToStepTo.Item2 == aAddress)
                        {
                            //There is an ASM BP at this address so break
                            OnBreak();
                            //Clear what we are stepping towards
                            ASMBPToStepTo = null;
                        }
                        else
                        {
                            fullUpdate = false;

                            //Check we aren't already stepping towards an ASM BP
                            if (ASMBPToStepTo == null)
                            {
                                //Check for future ASM breakpoints...

                                //Since we got this far, we know this must be an INT3 for a future ASM BP that has to be in current C# line.
                                //So get the ASM BP based off current address
                                var bp = GetAsmBreakpointInfoFromCSAddress(aAddress).First();
                                //Set it as address we are looking for
                                ASMBPToStepTo = bp;
                            }

                            //Step towards the ASM BP(step-over since we don't want to go through calls or anything)

                            //We must check we haven't just stepped and address jumped wildely out of range (e.g. conditional jumps)
                            if (aAddress < ASMBPToStepTo.Item1 || aAddress > ASMBPToStepTo.Item2)
                            {
                                //If we have, just continue execution as this BP won't be hit.
                                ContinueRunning();
                                ASMBPToStepTo = null;
                            }
                            else
                            {
                                //We must do an update of ASM window so Step-Over can function properly
                                SendAssembly(true);
                                //Delay / wait for asm window to update
                                WaitForAssemblyUpdate();
                                //Do the step-over
                                ASMStepOver();
                            }
                        }
                    }
                }
                else
                {
                    OnBreakpointHit(xBoundBreakpoints);
                }
                if (fullUpdate)
                {
                    RequestFullDebugStubUpdate();
                }
            }
        }

        private void DbgCmdTrace(uint obj)
        {
            OnCmdTrace(obj);
        }

        private void DbgCmdText(string obj)
        {
            OnCmdText(obj);
        }

        private void DbgCmdStarted()
        {
            OnCmdStarted();
        }

        private void DbgDebugMsg(string obj)
        {
            //Debug(obj);
            OnDebugMsg(obj);
        }

        private void DbgConnector_ConnectionLost(Exception obj)
        {
            OnConnectionLost(obj);
        }

        private void DbgCmdStackCorruptionOccurred(uint obj)
        {
            OnStackCorruptionOccurred(obj);
        }

        private void DbgCmdNullReferenceOccurred(uint obj)
        {
            OnNullReferenceOccurred(obj);
        }

        private void DbgCmdMessageBox(string obj)
        {
            OnMessageBox(obj);
        }

        private void DbgCmdChannel(byte aChannel, byte aCommand, byte[] aData)
        {
            OnCmdChannel(aChannel, aCommand, aData);
        }
    }
}
