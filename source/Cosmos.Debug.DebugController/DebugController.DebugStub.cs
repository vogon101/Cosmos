using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Cosmos.Debug.Common;

namespace Cosmos.Debug
{
    partial class DebugController
    {
        public byte[] GetMemoryData(uint address, uint length, int dataElementSize = 1)
        {
            return mDbgConnector.GetMemoryData(address, length, dataElementSize);
        }

        public byte[] GetStackData(int offset, uint length)
        {
            return mDbgConnector.GetStackData(offset, length);
        }

        public void PingDebugStub()
        {
            mDbgConnector.Ping();
        }

        public void SendBatchEndCmd()
        {
            mDbgConnector.SendCmd(Vs2Ds.BatchEnd);
        }

        public void SendStepIntoCmd()
        {
            mDbgConnector.SendCmd(Vs2Ds.AsmStepInto);
        }

        public void SendBreakCmd()
        {
            mDbgConnector.SendCmd(Vs2Ds.Break);
        }

        public void RequestFullDebugStubUpdate()
        {
            // We catch and resend data rather than using a second serial port because
            // while this would work fine in a VM, it would require 2 serial ports
            // when real hardware is used.
            new System.Threading.Tasks.Task(() =>
            {
                SendAssembly();
                mDbgConnector.SendRegisters();
                mDbgConnector.SendFrame();
                mDbgConnector.SendStack();
            }).Start();
        }

        private readonly ManualResetEvent mAsmWindow_CurrentLineUpdated = new ManualResetEvent(false);
        private readonly ManualResetEvent mAsmWindow_NextLine1Updated = new ManualResetEvent(false);
        private readonly ManualResetEvent mAsmWindow_NextAddress1Updated = new ManualResetEvent(false);

        public void SendAssembly(bool noDisplay = false)
        {
            Debug("SendAssembly");
            mAsmWindow_CurrentLineUpdated.Reset();
            mAsmWindow_NextAddress1Updated.Reset();
            mAsmWindow_NextLine1Updated.Reset();

            UInt32 xAddress = CurrentAddress.Value;
            var xSourceInfos = mDebugInfoDb.GetSourceInfos(xAddress);
            Debug("SendAssembly - SourceInfos retrieved for address 0x{0}", xAddress.ToString("X8"));
            if (xSourceInfos.Count > 0)
            {
                //We should be able to display the asesembler source for any address regardless of whether a C#
                //line is associated with it.
                //However, we do not store all labels in the debug database because that would make the compile
                //time insane.
                //So:
                // - We take the current address amd find the method it is part of
                // - We use the method header label as a start point and find all asm labels till the method footer label
                // - We then find all the asm for these labels and display it.

                Label[] xLabels = mDebugInfoDb.GetMethodLabels(xAddress);
                Debug("SendAssembly - MethodLabels retrieved");
                // get the label of our current position, or the closest one before
                var curPosLabel = xLabels.Where(i => i.Address <= xAddress).OrderByDescending(i => i.Address).FirstOrDefault();
                // if curPosLabel is null, grab the first one.
                if (curPosLabel == null)
                {
                    curPosLabel = xLabels[0];
                }

                var curPosIndex = Array.IndexOf(xLabels, curPosLabel);
                // we want 50 items before and after the current item, so 100 in total.
                var itemsBefore = 10;
                var itemsAfter = 10;

                if (curPosIndex < itemsBefore)
                {
                    // there are no 50 items before the current one, so adjust
                    itemsBefore = curPosIndex;
                }
                if ((curPosIndex + itemsAfter) >= xLabels.Length)
                {
                    // there are no 50 items after the current one, so adjust
                    itemsAfter = xLabels.Length - curPosIndex;
                }

                var newArr = new Label[itemsBefore + itemsAfter];
                for (int i = 0; i < newArr.Length; i++)
                {
                    newArr[i] = xLabels[(curPosIndex - itemsBefore) + i];
                }
                xLabels = newArr;

                //The ":" has to be added in because labels in asm code have it on the end - it's easier to add it here than
                //strip them out of the read asm
                var xLabelNames = xLabels.Select(x => x.Name + ":").ToList();

                // Get assembly source
                var xCode = AsmSource.GetSourceForLabels(Path.ChangeExtension(ISOFile, ".asm"), xLabelNames);
                Debug("SendAssembly - SourceForLabels retrieved");

                // Get label for current address.
                // A single address can have multiple labels (IL, Asm). Because of this we search
                // for the one with the Asm tag. We dont have the tags in this debug info though,
                // so instead if there is more than one label we use the longest one which is the Asm tag.
                string xCurrentLabel = "";
                var xCurrentLabels = mDebugInfoDb.GetLabels(xAddress);
                if (xCurrentLabels.Length > 0)
                {
                    xCurrentLabel = xCurrentLabels.OrderBy(q => q.Length).Last();
                }
                if (string.IsNullOrEmpty(xCurrentLabel))
                {
                    xCurrentLabel = "NO_METHOD_LABEL_FOUND";
                }

                // Insert filter labels list as THIRD(!) line of our data stream
                string filterLabelsList = "";
                foreach (var addressInfo in mINT3sSet)
                {
                    //"We have to add the ".00:" because of how the ASM window works...
                    filterLabelsList += "|" + addressInfo.Value + ".00";
                }
                if (filterLabelsList.Length > 0)
                {
                    filterLabelsList = filterLabelsList.Substring(1);
                }
                xCode.Insert(0, filterLabelsList + "\r\n");
                // Insert parameters as SECOND(!) line of our data stream
                xCode.Insert(0, (noDisplay ? "NoDisplay" : "") + "|" + (ASMSteppingMode ? "AsmStepMode" : "") + "\r\n");
                // Insert current line's label as FIRST(!) line of our data stream
                xCode.Insert(0, xCurrentLabel + "\r\n");
                //THINK ABOUT THE ORDER that he above lines occur in and where they insert data into the stream - don't switch it!
                Debug("SendAssembly - Sending through pipe now");
                OnAssemblySource(xCode.ToString());
                Debug("SendAssembly - Done");
            }
        }

        public void WaitForAssemblyUpdate()
        {
            Debug("WaitForAssemblyUpdate");
            mAsmWindow_CurrentLineUpdated.WaitOne(5000);
            mAsmWindow_NextAddress1Updated.WaitOne(5000);
            mAsmWindow_NextLine1Updated.WaitOne(5000);
        }

        public void SetCurrentLine(string aLine)
        {
            mCurrentASMLine = aLine;
            mAsmWindow_CurrentLineUpdated.Set();
        }

        public void SetNextLine(string aLine)
        {
            if (aLine == null)
            {
                mNextASMLine1 = null;
                mNextAddress1 = null;
            }
            else
            {
                mNextASMLine1 = aLine;
            }
            mAsmWindow_NextLine1Updated.Set();
        }

        public void SetNextAddress(string aLabel)
        {
            mNextAddress1 = GetAddressOfLabel(aLabel);
            mAsmWindow_NextAddress1Updated.Set();
        }
    }
}
