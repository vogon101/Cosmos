using System;
using System.Linq;
using Cosmos.Debug.Common;

namespace Cosmos.Debug
{
    partial class DebugController
    {
        public bool ASMSteppingMode
        {
            get;
            private set;
        }

        private Tuple<UInt32, UInt32, int> ASMBPToStepTo = null;
        private string mCurrentASMLine = null;
        public UInt32? mNextAddress1 = null;
        public string mNextASMLine1 = null;
        private bool mASMSteppingOut = false;
        private int mASMSteppingOut_NumEndMethodLabelsPassed = 0;
        private bool mStepping = false;
        
        public void ContinueRunning()
        {
            mDbgConnector.Continue();
        }

        public void ToggleStepMode()
        {
            ASMSteppingMode = !ASMSteppingMode;
        }

        public void SetStepModeAssembler()
        {
            ASMSteppingMode = true;
        }

        public void SetStepModeSource()
        {
            ASMSteppingMode = false;
        }

        public void Continue()
        { // F5
            ClearINT3sOnCurrentMethod();

            //Check for a future asm BP on current line
            //If there is, don't do continue, do AsmStepOver

            // The current address may or may not be a C# line due to asm stepping
            //So get the C# INT3 address
            UInt32 csAddress = mDebugInfoDb.GetClosestCSharpBPAddress(CurrentAddress.Value);
            //Get any Asm BPs for this address
            var bps = GetAsmBreakpointInfoFromCSAddress(csAddress).Where(x => x.Item2 > CurrentAddress.Value).ToList();
            //If there are any, do AsmStepOver on the next one after current address
            if (bps.Count > 0)
            {
                var bp = bps.OrderBy(x => x.Item2).First();
                ASMBPToStepTo = bp;

                ASMStepOver();

                CurrentAddress = null;
                mCurrentASMLine = null;
            }
            else
            {
                CurrentAddress = null;
                mCurrentASMLine = null;

                mDbgConnector.Continue();
            }
        }

        public void ASMStepOver()
        {
            //ASM Step over : Detect calls and treat them specially.
            //If current line has been stepped, get next line (since that is what we will step over)
            //If current line hasn't been stepped, use current line

            //If current line is CALL, set INT3 on next line and do continue
            //Else do asm step-into

            string currentASMLine = mCurrentASMLine;

            if (string.IsNullOrEmpty(currentASMLine))
            {
                mDbgConnector.SendCmd(Vs2Ds.AsmStepInto);
            }
            else
            {
                currentASMLine = currentASMLine.Trim();
                string currentASMOp = currentASMLine.Split(' ')[0].ToUpper();
                if (currentASMOp == "CALL")
                {
                    //Get the line after the call
                    string nextASMLine = mNextASMLine1;
                    UInt32? nextAddress = mNextAddress1;
                    if (string.IsNullOrEmpty(nextASMLine) || !nextAddress.HasValue)
                    {
                        mDbgConnector.SendCmd(Vs2Ds.AsmStepInto);
                    }
                    else
                    {
                        //Set the INT3 at next address
                        mDbgConnector.SetAsmBreakpoint(nextAddress.Value);
                        mDbgConnector.Continue();
                    }
                }
                else
                {
                    mDbgConnector.SendCmd(Vs2Ds.AsmStepInto);
                }
            }
        }

        public void StepInto()
        {
            mStepping = true;
            if (ASMSteppingMode)
            {
                mDbgConnector.SendCmd(Vs2Ds.AsmStepInto);
                mDbgConnector.SendRegisters();
            }
            else
            {
                SetINT3sOnCurrentMethod();
                mDbgConnector.SendCmd(Vs2Ds.StepInto);
            }
        }

        public void StepOver()
        {
            mStepping = true;
            if (ASMSteppingMode)
            {
                ASMStepOver();
            }
            else
            {
                SetINT3sOnCurrentMethod();
                mDbgConnector.SendCmd(Vs2Ds.StepOver);
            }
        }

        public void StepOut()
        {
            ClearINT3sOnCurrentMethod();

            mStepping = true;
            if (ASMSteppingMode)
            {
                mASMSteppingOut = true;
                mASMSteppingOut_NumEndMethodLabelsPassed = 0;
                mDbgConnector.SendCmd(Vs2Ds.AsmStepInto);

                //Set a condition to say we should be doing step out
                //On break, check line just stepped.

                //If current line is RET - do one more step then break.
                //Else do another step
            }
            else
            {
                mDbgConnector.SendCmd(Vs2Ds.StepOut);
            }
        }
    }
}
