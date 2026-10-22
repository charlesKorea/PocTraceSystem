using System;

namespace PocTrace.Core.Enums
{
    public enum EndPointType
    {
        Match,      // 성공: 목적지 노즐 찾음 (태그 일치)
        Mismatch,   // 성공: 노즐은 찾았으나 태그 불일치
        Nozzle,     // (일반 노즐)
        Cycle,      // 실패: 순환 참조
        Equipment,  // 실패: 다른 장비 도달
        Broken,     // 실패: 끊김 (일반적인 Dead End)
        Cap,        // [New] 성공: '기타' 이름의 컴포넌트로 마감 (정상 종료)
        DataError,  // 실패: 데이터 오류
        Empty,      // 실패: 시작하자마자 없음
        Takeoff     // 성공: 댐퍼(Takeoff)로 종료
    }

    // [설계서 5.2 준수] Pipe, Fitting 부활
    public enum NodeType
    {
        Unknown = -1,
        Equipment = 0, // 설비
        Nozzle = 1,    // 노즐
        Pipe = 2,      // [복구] 일반 배관 (Spool 등) 또는 미분류 배관 객체
        Fitting = 3,    // [복구] 밸브, 엘보, 티, 리듀서 등 부속품
        
        Damper = 4     // [New] 댐퍼 객체 추가
    }

    public enum ValidationStatus
    {
        Match,
        Mismatch,
        Warning,
        Error,
        NotApplicable
    }
}