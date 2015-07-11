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
        // for now, these just forward calls to the debuginfo class. This should be made more clever though....


        public SourceInfos GetSourceInfos(uint aAddress)
        {
            Debug("GetSourceInfos. Address = 0x{0}", aAddress.ToString("X8"));
            return mDebugInfoDb.GetSourceInfos(aAddress);
        }

        public string[] GetLabels(uint aAddress)
        {
            Debug("GetLabels. Address = 0x{0}", aAddress.ToString("X8"));
            return mDebugInfoDb.GetLabels(aAddress);
        }

        public MethodIlOp TryGetFirstMethodIlOpByLabelName(string aLabelName)
        {
            Debug("TryGetFirstMethodIlOpByLabelName. LabelName = '{0}'", aLabelName);
            var xResult = mDebugInfoDb.Connection.Query<MethodIlOp>(new SQLinq<MethodIlOp>().Where(q => q.LabelName == aLabelName)).FirstOrDefault();
            Debug("IL Offset = .IL_{0}", xResult.IlOffset.ToString("X4"));
            return xResult;
        }

        public Method GetMethod(long aMethodId)
        {
            Debug("GetMethod. MethodID = {0}", aMethodId);
            var xResult = mDebugInfoDb.Connection.Get<Method>(aMethodId);
            Debug("Method.LabelCall = '{0}'", xResult.LabelCall);
            return xResult;
        }

        public LOCAL_ARGUMENT_INFO[] GetAllLocalsAndArgumentsInfosByMethodLabelName(string aLabelName)
        {
            Debug("GetAllLocalsAndArgumentsInfosByMethodLabelName. LabelName = '{0}'", aLabelName);
            var xResult = mDebugInfoDb.Connection.Query<LOCAL_ARGUMENT_INFO>(new SQLinq<LOCAL_ARGUMENT_INFO>().Where(q => q.METHODLABELNAME == aLabelName)).ToArray();
            Debug("Result.Count = {0}", xResult.Length);
            return xResult;
        }

        public bool TryGetDocumentIdByName(string aDocumentName, out long oDocumentId)
        {
            Debug("TryGetDocumentIdByName. DocumentName = '{0}'", aDocumentName);
            var xResult = mDebugInfoDb.DocumentGUIDs.TryGetValue(aDocumentName, out oDocumentId);
            Debug("Result = {0}, DocumentId = {1}", xResult, oDocumentId);
            return xResult;
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

        public AssemblyFile GetAssemblyFileById(long aId)
        {
            Debug("GetAssemblyFileById. ID = {0}", aId);
            var xResult = mDebugInfoDb.Connection.Get<AssemblyFile>(aId);
            Debug("Result.PathName = '{0}'", xResult.Pathname);
            return xResult;
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

        public uint GetAddressOfLabel(string aLabelName)
        {
            Debug("GetAddressOfLabel. Label = '{0}'", aLabelName);
            var xResult = mDebugInfoDb.AddressOfLabel(aLabelName);
            Debug("Result = 0x{0}", xResult.ToString("X8"));
            return xResult;
        }

        public DebugInfo.Field_Map GetFieldMap(string aTypeName)
        {
            Debug("GetFieldMap. Typename = '{0}'", aTypeName);
            var xResult = mDebugInfoDb.GetFieldMap(aTypeName);
            Debug("Fieldnames.Count = {0}", xResult.FieldNames.Count);
            return xResult;
        }

        public FIELD_INFO GetFieldInfoByName(string aName)
        {
            Debug("GetFieldInfoByName. FieldName = '{0}'", aName);
            return mDebugInfoDb.Connection.Query(new SQLinq<Cosmos.Debug.Common.FIELD_INFO>().Where(q => q.NAME == aName)).First();
        }
    }
}
