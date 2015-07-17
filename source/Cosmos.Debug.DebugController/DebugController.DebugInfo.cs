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
        private void InitializeCache()
        {
            mSourceInfosCache = new CacheHelper<uint, SourceInfos>(a => mDebugInfoDb.GetSourceInfos(a));
            mLabelsCache = new CacheHelper<uint, string[]>(a => mDebugInfoDb.GetLabels(a));
            mFirstMethodIlOpByLabelNameCache = new CacheHelper<string, MethodIlOp>(n => mDebugInfoDb.Connection.Query<MethodIlOp>(new SQLinq<MethodIlOp>().Where(q => q.LabelName == n)).FirstOrDefault());
            mMethodCache = new CacheHelper<long, Method>(i => mDebugInfoDb.Connection.Get<Method>(i));
            mAllLocalsAndArgumentsInfosByMethodLabelNameCache = new CacheHelper<string, LOCAL_ARGUMENT_INFO[]>(a => mDebugInfoDb.Connection.Query<LOCAL_ARGUMENT_INFO>(new SQLinq<LOCAL_ARGUMENT_INFO>().Where(q => q.METHODLABELNAME == a)).ToArray());
            mDocumentIdByNameCache = new CacheHelper<string, long?>(n =>
                                                                    {
                                                                        long xId;
                                                                        var xHasResult = mDebugInfoDb.DocumentGUIDs.TryGetValue(n, out xId);
                                                                        if (xHasResult)
                                                                        {
                                                                            return xId;
                                                                        }
                                                                        else
                                                                        {
                                                                            return null;
                                                                        }
                                                                    });
            mAssemblyFileByIdCache = new CacheHelper<long, AssemblyFile>(i => mDebugInfoDb.Connection.Get<AssemblyFile>(i));
            mAddressOfLabelCache = new CacheHelper<string, uint>(l => mDebugInfoDb.AddressOfLabel(l));
            mFieldMapCache = new CacheHelper<string, DebugInfo.Field_Map>(t => mDebugInfoDb.GetFieldMap(t));
            mFieldInfoByNameCache = new CacheHelper<string, FIELD_INFO>(n => mDebugInfoDb.Connection.Query(new SQLinq<Cosmos.Debug.Common.FIELD_INFO>().Where(q => q.NAME == n)).First());
        }

        private CacheHelper<uint, SourceInfos> mSourceInfosCache;
        public SourceInfos GetSourceInfos(uint aAddress)
        {
            return mSourceInfosCache.GetValue(aAddress);
        }

        private CacheHelper<uint, string[]> mLabelsCache;
        public string[] GetLabels(uint aAddress)
        {
            return mLabelsCache.GetValue(aAddress);
        }

        private CacheHelper<string, MethodIlOp> mFirstMethodIlOpByLabelNameCache;
        public MethodIlOp TryGetFirstMethodIlOpByLabelName(string aLabelName)
        {
            return mFirstMethodIlOpByLabelNameCache.GetValue(aLabelName);
        }

        private CacheHelper<long, Method> mMethodCache;
        public Method GetMethod(long aMethodId)
        {
            return mMethodCache.GetValue(aMethodId);
        }


        private CacheHelper<string, LOCAL_ARGUMENT_INFO[]> mAllLocalsAndArgumentsInfosByMethodLabelNameCache;
        public LOCAL_ARGUMENT_INFO[] GetAllLocalsAndArgumentsInfosByMethodLabelName(string aLabelName)
        {
            return mAllLocalsAndArgumentsInfosByMethodLabelNameCache.GetValue(aLabelName);
        }

        private CacheHelper<string, long?> mDocumentIdByNameCache;
        private long? mCachedDocumentIdByName;
        private string mCachedDocumentIdByNameValue;
        public bool TryGetDocumentIdByName(string aDocumentName, out long oDocumentId)
        {
            var xValue = mDocumentIdByNameCache.GetValue(aDocumentName);
            oDocumentId = xValue.GetValueOrDefault();
            return xValue != null;
        }

        public Method GetMethodByDocumentIDAndLinePosition(long aDocID, long aStartPos, long aEndPos)
        {
            Debug("GetMethodByDocumentIDAndLinePosition. DocID = {0}, StartPos = {1}, EndPos = {2}", aDocID, aStartPos, aEndPos);
            var xResult = mDebugInfoDb.Connection.Query(new SQLinq<Method>().Where(x => x.DocumentID == aDocID
                                                                                        && x.LineColStart <= aStartPos
                                                                                        && x.LineColEnd >= aEndPos)).Single();
            Debug("Result.LabelCall = '{0}'", xResult.LabelCall);
            return xResult;
        }

        private CacheHelper<long, AssemblyFile> mAssemblyFileByIdCache;
        public AssemblyFile GetAssemblyFileById(long aId)
        {
            return mAssemblyFileByIdCache.GetValue(aId);
        }

        public DebugInfo.SequencePoint[] GetSequencePoints(string aPathname, int aMethodToken)
        {
            Debug("GetSequencePoints. Pathname = '{0}', MethodToken = '{1}'", aPathname, aMethodToken);
            var xResult = mDebugInfoDb.GetSequencePoints(aPathname, aMethodToken);
            Debug("Result.Count = {0}", xResult.Length);
            return xResult;
        }

        public MethodIlOp GetFirstMethodIlOpByMethodIdAndILOffset(long aMethodId, long aILOffset)
        {
            Debug("GetFirstMethodIlOpByMethodIdAndILOffset. MethodID = {0}, ILOffset = 0x{1}", aMethodId, aILOffset.ToString("X4"));
            var xResult = mDebugInfoDb.Connection.Query(new SQLinq<MethodIlOp>().Where(q => q.MethodID == aMethodId && q.IlOffset == aILOffset)).First();
            Debug("Result.LabelName = '{0}'", xResult.LabelName);
            return xResult;
        }

        private CacheHelper<string, uint> mAddressOfLabelCache;
        public uint GetAddressOfLabel(string aLabelName)
        {
            return mAddressOfLabelCache.GetValue(aLabelName);
        }

        private CacheHelper<string, DebugInfo.Field_Map> mFieldMapCache;
        public DebugInfo.Field_Map GetFieldMap(string aTypeName)
        {
            return mFieldMapCache.GetValue(aTypeName);
        }

        private CacheHelper<string, FIELD_INFO> mFieldInfoByNameCache;
        public FIELD_INFO GetFieldInfoByName(string aName)
        {
            return mFieldInfoByNameCache.GetValue(aName);
        }
    }
}
