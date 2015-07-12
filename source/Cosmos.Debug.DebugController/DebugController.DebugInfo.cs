using System;
using System.Linq;
using Cosmos.Debug.Common;
using Dapper;
using DapperExtensions;
using SQLinq;
using SQLinq.Dapper;

namespace Cosmos.Debug
{
    partial class DebugController
    {
        private readonly object mCacheLockObj = new Object();

        // for now, these just forward calls to the debuginfo class. This should be made more clever though....

        private SourceInfos mCachedSourceInfos;
        private uint? mCachedSourceInfosAddress;

        public SourceInfos GetSourceInfos(uint aAddress)
        {
            lock (mCacheLockObj)
            {
                Debug("GetSourceInfos. Address = 0x{0}", aAddress.ToString("X8"));
                if (mCachedSourceInfosAddress != null && mCachedSourceInfosAddress.Value == aAddress)
                {
                    Debug("Returning from cache");
                    return mCachedSourceInfos;
                }
                mCachedSourceInfos = mDebugInfoDb.GetSourceInfos(aAddress);
                mCachedSourceInfosAddress = aAddress;
                return mCachedSourceInfos;
            }
        }

        private string[] mCachedLabels;
        private uint? mCachedLabelsAddress;

        public string[] GetLabels(uint aAddress)
        {
            lock (mCacheLockObj)
            {
                Debug("GetLabels. Address = 0x{0}", aAddress.ToString("X8"));
                if (mCachedLabelsAddress != null && mCachedLabelsAddress.Value == aAddress)
                {
                    Debug("Returning from cache");
                    return mCachedLabels;
                }
                var xResult = mDebugInfoDb.GetLabels(aAddress);
                Debug("Result.Length = {0}", xResult.Length);
                mCachedLabels = xResult;
                mCachedLabelsAddress = aAddress;
                return xResult;
            }
        }

        private MethodIlOp mCachedFirstMethodIlOpByLabelName;

        public MethodIlOp TryGetFirstMethodIlOpByLabelName(string aLabelName)
        {
            lock (mCacheLockObj)
            {
                Debug("TryGetFirstMethodIlOpByLabelName. LabelName = '{0}'", aLabelName);
                if (mCachedFirstMethodIlOpByLabelName != null && mCachedFirstMethodIlOpByLabelName.LabelName.Equals(aLabelName, StringComparison.OrdinalIgnoreCase))
                {
                    Debug("Returning from cache");
                    return mCachedFirstMethodIlOpByLabelName;
                }
                var xResult = mDebugInfoDb.Connection.Query<MethodIlOp>(new SQLinq<MethodIlOp>().Where(q => q.LabelName == aLabelName)).FirstOrDefault();
                mCachedFirstMethodIlOpByLabelName = xResult;
                Debug("IL Offset = .IL_{0}", xResult.IlOffset.ToString("X4"));
                return xResult;
            }
        }

        private Method mCachedMethod;
        public Method GetMethod(long aMethodId)
        {
            lock (mCacheLockObj)
            {
                Debug("GetMethod. MethodID = {0}", aMethodId);
                if (mCachedMethod != null && mCachedMethod.ID == aMethodId)
                {
                    Debug("Returning from cache");
                    return mCachedMethod;
                }
                var xResult = mDebugInfoDb.Connection.Get<Method>(aMethodId);
                Debug("Method.LabelCall = '{0}'", xResult.LabelCall);
                mCachedMethod = xResult;
                return xResult;
            }
        }

        private LOCAL_ARGUMENT_INFO[] mCachedAllLocalsAndArgumentsInfosByMethodLabelName;
        private string mCachedAllLocalsAndArgumentsInfosByMethodLabelNameValue;
        public LOCAL_ARGUMENT_INFO[] GetAllLocalsAndArgumentsInfosByMethodLabelName(string aLabelName)
        {
            lock (mCacheLockObj)
            {
                Debug("GetAllLocalsAndArgumentsInfosByMethodLabelName. LabelName = '{0}'", aLabelName);
                if (StringComparer.OrdinalIgnoreCase.Equals(aLabelName, mCachedAllLocalsAndArgumentsInfosByMethodLabelNameValue))
                {
                    Debug("Returning from cache");
                    return mCachedAllLocalsAndArgumentsInfosByMethodLabelName;
                }

                var xResult = mDebugInfoDb.Connection.Query<LOCAL_ARGUMENT_INFO>(new SQLinq<LOCAL_ARGUMENT_INFO>().Where(q => q.METHODLABELNAME == aLabelName)).ToArray();
                mCachedAllLocalsAndArgumentsInfosByMethodLabelNameValue = aLabelName;
                mCachedAllLocalsAndArgumentsInfosByMethodLabelName = xResult;
                Debug("Result.Count = {0}", xResult.Length);
                return xResult;
            }
        }

        private long? mCachedDocumentIdByName;
        private string mCachedDocumentIdByNameValue;
        public bool TryGetDocumentIdByName(string aDocumentName, out long oDocumentId)
        {
            lock (mCacheLockObj)
            {
                Debug("TryGetDocumentIdByName. DocumentName = '{0}'", aDocumentName);
                if (String.Equals(mCachedDocumentIdByNameValue, aDocumentName, StringComparison.OrdinalIgnoreCase))
                {
                    Debug("Returning from cache");
                    oDocumentId = mCachedDocumentIdByName.GetValueOrDefault();
                    return mCachedDocumentIdByName.HasValue;
                }
                var xResult = mDebugInfoDb.DocumentGUIDs.TryGetValue(aDocumentName, out oDocumentId);
                mCachedDocumentIdByNameValue = aDocumentName;
                mCachedDocumentIdByName = oDocumentId;
                Debug("Result = {0}, DocumentId = {1}", xResult, oDocumentId);
                return xResult;
            }
        }

        public Method GetMethodByDocumentIDAndLinePosition(long aDocID, long aStartPos, long aEndPos)
        {
            lock (mCacheLockObj)
            {
                Debug("GetMethodByDocumentIDAndLinePosition. DocID = {0}, StartPos = {1}, EndPos = {2}", aDocID, aStartPos, aEndPos);
                var xResult = mDebugInfoDb.Connection.Query(new SQLinq<Method>().Where(x => x.DocumentID == aDocID
                                                                                            && x.LineColStart <= aStartPos
                                                                                            && x.LineColEnd >= aEndPos)).Single();
                Debug("Result.LabelCall = '{0}'", xResult.LabelCall);
                return xResult;
            }
        }

        public AssemblyFile GetAssemblyFileById(long aId)
        {
            lock (mCacheLockObj)
            {
                Debug("GetAssemblyFileById. ID = {0}", aId);
                var xResult = mDebugInfoDb.Connection.Get<AssemblyFile>(aId);
                Debug("Result.PathName = '{0}'", xResult.Pathname);
                return xResult;
            }
        }

        public DebugInfo.SequencePoint[] GetSequencePoints(string aPathname, int aMethodToken)
        {
            lock (mCacheLockObj)
            {
                Debug("GetSequencePoints. Pathname = '{0}', MethodToken = '{1}'", aPathname, aMethodToken);
                var xResult = mDebugInfoDb.GetSequencePoints(aPathname, aMethodToken);
                Debug("Result.Count = {0}", xResult.Length);
                return xResult;
            }
        }

        public MethodIlOp GetFirstMethodIlOpByMethodIdAndILOffset(long aMethodId, long aILOffset)
        {
            lock (mCacheLockObj)
            {
                Debug("GetFirstMethodIlOpByMethodIdAndILOffset. MethodID = {0}, ILOffset = 0x{1}", aMethodId, aILOffset.ToString("X4"));
                var xResult = mDebugInfoDb.Connection.Query(new SQLinq<MethodIlOp>().Where(q => q.MethodID == aMethodId && q.IlOffset == aILOffset)).First();
                Debug("Result.LabelName = '{0}'", xResult.LabelName);
                return xResult;
            }
        }

        public uint GetAddressOfLabel(string aLabelName)
        {
            lock (mCacheLockObj)
            {
                Debug("GetAddressOfLabel. Label = '{0}'", aLabelName);
                var xResult = mDebugInfoDb.AddressOfLabel(aLabelName);
                Debug("Result = 0x{0}", xResult.ToString("X8"));
                return xResult;
            }
        }

        public DebugInfo.Field_Map GetFieldMap(string aTypeName)
        {
            lock (mCacheLockObj)
            {
                Debug("GetFieldMap. Typename = '{0}'", aTypeName);
                var xResult = mDebugInfoDb.GetFieldMap(aTypeName);
                Debug("Fieldnames.Count = {0}", xResult.FieldNames.Count);
                return xResult;
            }
        }

        public FIELD_INFO GetFieldInfoByName(string aName)
        {
            lock (mCacheLockObj)
            {
                Debug("GetFieldInfoByName. FieldName = '{0}'", aName);
                return mDebugInfoDb.Connection.Query(new SQLinq<Cosmos.Debug.Common.FIELD_INFO>().Where(q => q.NAME == aName)).First();
            }
        }
    }
}
