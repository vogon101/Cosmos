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
            return mDebugInfoDb.GetSourceInfos(aAddress);
        }

        public string[] GetLabels(uint aAddress)
        {
            return mDebugInfoDb.GetLabels(aAddress);
        }

        public MethodIlOp TryGetFirstMethodIlOpByLabelName(string aLabelName)
        {
            return mDebugInfoDb.Connection.Query<MethodIlOp>(new SQLinq<MethodIlOp>().Where(q => q.LabelName == aLabelName)).FirstOrDefault();
        }

        public Method GetMethod(long aMethodId)
        {
            return mDebugInfoDb.Connection.Get<Method>(aMethodId);
        }

        public LOCAL_ARGUMENT_INFO[] GetAllLocalsAndArgumentsInfosByMethodLabelName(string aLabelName)
        {
            return mDebugInfoDb.Connection.Query<LOCAL_ARGUMENT_INFO>(new SQLinq<LOCAL_ARGUMENT_INFO>().Where(q => q.METHODLABELNAME == aLabelName)).ToArray();
        }

        public bool TryGetDocumentIdByName(string aDocumentName, out long oDocumentId)
        {
            return mDebugInfoDb.DocumentGUIDs.TryGetValue(aDocumentName, out oDocumentId);
        }

        public Method GetMethodByDocumentIDAndLinePosition(long aDocID, long aStartPos, long aEndPos)
        {
            return mDebugInfoDb.Connection.Query(new SQLinq<Method>().Where(x => x.DocumentID == aDocID
                                                                                 && x.LineColStart <= aStartPos
                                                                                 && x.LineColEnd >= aEndPos)).Single();
        }

        public AssemblyFile GetAssemblyFileById(long aId)
        {
            return mDebugInfoDb.Connection.Get<AssemblyFile>(aId);
        }

        public DebugInfo.SequencePoint[] GetSequencePoints(string aPathname, int aMethodToken)
        {
            return mDebugInfoDb.GetSequencePoints(aPathname, aMethodToken);
        }

        public MethodIlOp GetFirstMethodIlOpByMethodIdAndILOffset(long aMethodId, long aILOffset)
        {
            return mDebugInfoDb.Connection.Query(new SQLinq<MethodIlOp>().Where(q => q.MethodID == aMethodId && q.IlOffset == aILOffset)).First();
        }

        public uint GetAddressOfLabel(string aLabelName)
        {
            return mDebugInfoDb.AddressOfLabel(aLabelName);
        }

        public DebugInfo.Field_Map GetFieldMap(string aTypeName)
        {
            return mDebugInfoDb.GetFieldMap(aTypeName);
        }

        public FIELD_INFO GetFieldInfoByName(string aName)
        {
            return mDebugInfoDb.Connection.Query(new SQLinq<Cosmos.Debug.Common.FIELD_INFO>().Where(q => q.NAME == aName)).First();
        }
    }
}
