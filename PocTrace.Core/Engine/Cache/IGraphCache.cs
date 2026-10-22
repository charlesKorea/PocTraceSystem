using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PocTrace.Core.Enums;
using PocTrace.Core.Models;

namespace PocTrace.Core.Engine.Cache
{
        public interface IGraphCache
    {
        // [New] 캐시 초기화 상태 확인
        bool IsInitialized { get; }

        bool TryGetNode(Guid instanceId, out NodeInfo node);
        bool TryGetNodeByTag(string tag, out NodeInfo node);
        bool TryGetNodeByBimId(string bimId, out NodeInfo node);
        bool TryGetNodeByInstanceNumericId(long instanceNumericId, out NodeInfo node);
        bool TryGetPoc(Guid instanceId, out PocData poc);
        bool TryGetPipe(Guid pipeId, out PipeData pipe);
        IEnumerable<PocData> GetPocsByOwner(Guid ownerId);
        NodeType GetNodeType(Guid instanceId);
        // [누락된 메서드 추가 - 여기를 복사해 넣으세요]
        string GetNodeName(Guid nodeId);
        string GetNodeTag(Guid nodeId);
        bool TryGetTakeoff(Guid takeoffId, out TakeoffData takeoff);
        bool TryFindPipeIdByOutlet(Guid outletId, out Guid pipeId);

        // [신규 추가] 태그로 시작점(POC) 찾기
        // includePartialMatch=true(Viewer 대화형): 설비 Tag를 ','로 분리한 토큰 중 하나가 equipTag와
        //   정확히 일치하는 설비(예: 스크러버 "dama01,dama02")의 POC도 함께 반환한다.
        // includePartialMatch=false(기본, Admin 스케줄러): 정확 일치만 반환(중복 검수 방지).
        IEnumerable<PocData> FindStartPocs(string equipTag, bool includePartialMatch = false);

        // [New] ID로 NodeInfo 전체 조회 (Validator용)
        NodeInfo GetNode(Guid nodeId);

        /// <summary>캐시 내 전체 POC 열거 (Rule03 Nozzle ConnectionTag 스캔 등).</summary>
        IEnumerable<PocData> EnumeratePocs();

        /// <summary>캐시 내 전체 Takeoff 열거 (Rule05 Damper[EQPID] 스캔).</summary>
        IEnumerable<TakeoffData> EnumerateTakeoffs();

        /// <summary>캐시 내 전체 Pipe 열거 (Rule06 Hookup Number 스캔).</summary>
        IEnumerable<PipeData> EnumeratePipes();

        /// <summary>캐시 내 전체 Node 열거 (Rule07 BIM Group Name 스캔).</summary>
        IEnumerable<NodeInfo> EnumerateNodes();

        // [New] ID로 PocData 전체 조회 (Validator용)
        PocData GetPoc(Guid pocId);

        // [Fix] 컴파일 오류 해결: GetPipe 추가
        PipeData GetPipe(Guid pipeId);

        // [New] 현재 로드된 DB 이름 (TraceEngine이 이걸 갖다 씁니다)
        string DbName { get; }

        // 2. [Fix] 컴파일 오류 해결: 전체 매핑 정보 접근용 프로퍼티
        // [Type Update] Key: DbName -> Value: (Key: GroupName -> Value: (Key: Ut -> Value: MappedUt))
        Dictionary<string, Dictionary<string, Dictionary<string, string>>> UtilityMaps { get; }

        string GetSubdivision1(Guid nodeId);
        string GetSubdivision2(Guid nodeId);
        string GetQCode(Guid nodeId);
        string GetDivision(Guid nodeId);
                string GetHookupNumber(Guid nodeId);

        // [신규] DynamicPropertiesData 조회 (PocNo: PocID, ConnectorUse: POC InstanceId)
        DynamicPropertiesData GetDynamicProperties(Guid ownerId);
    }
}
