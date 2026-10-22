using System;
using System.Collections.Generic;
using System.Linq; // LastOrDefault 사용을 위해 필요
using PocTrace.Core.Enums;

namespace PocTrace.Core.Models
{
        // [신규] 경로상의 한 스텝을 상세히 표현하는 클래스
    public class TraceStep
    {
        public int StepIndex { get; set; }
        public Guid NodeId { get; set; }
        public string NodeName { get; set; } // 설비/피팅 이름 (Pipe는 Empty)
        public string NodeType { get; set; } // "Nozzle", "Pipe", "Fitting" 등
        public Guid? OutletUsed { get; set; } // 다음 단계로 넘어가기 위해 사용한 출구 ID

        // 💡 [신규] 배관 길이를 기록하기 위한 필드 추가
        public double Length { get; set; } = 0.0;

        // 💡 [신규] 자재 코드(QCode) 저장을 위한 필드 추가
        public string QCode { get; set; }

        // 🌟 [신규 추가] 훅업 검증을 위한 메타데이터
        public string Division { get; set; }
        public string HookupNumber { get; set; }

        // [신규] 작업자 정보 (Pipe 기준)
        public DateTime? OpTime { get; set; }          // 작업날짜
        public string UserName { get; set; }           // 작업자 이름
        public string CompanyName { get; set; }        // 작업자 귀속 회사

        public override string ToString() => $"[{NodeType}] {NodeName}";
    }

    public class TraceResult
    {
        public string StartEquipmentTag { get; set; }
        public string StartNodeName { get; set; } // [신규] 설비 이름 (Grid/Sheet2 구분용)
        public Guid StartNodeGUID { get; set; }   // [신규] 설비 고유 ID (Grouping Key)
        public Guid TargetNodeGUID { get; set; }   // [신규] Target 설비 고유 ID (Grouping Key)
        public Guid StartPocId { get; set; }

        // [New] 도착점 POC ID (Nozzle인 경우 필수, 그 외에는 null일 수 있음)
        public Guid? EndPocId { get; set; }

        // 결과 타입 (내부 로직용)
        public EndPointType EndType { get; set; }
        
        // 결과 노드 이름 (예: Damper[Tag], Nozzle[Tag])
        public string EndName { get; set; } 

        // [명칭 변경] 기존 Status -> IsMatched
        public ValidationStatus IsMatched { get; set; }

        // 상세 경로
        public List<TraceStep> Steps { get; set; } = new List<TraceStep>();
        
        // 추적 시간
        public DateTime TracedAt { get; set; }
        
        // 홉 수
        public long HopCount => Steps.Count;

        // [신규] 배관 길이 합산 (단위: mm 등 정수 변환값)
        public double TotalLength { get; set; }

        // ---------------------------------------------------------------------
        // [UI/Excel Presentation Layer] (사용자 요청 반영)
        // ---------------------------------------------------------------------

        // 1. [S: Tag]
        public string StartNodeTag => StartEquipmentTag;

        // 2. [S: Connector GUID]
        public string StartPocGUID => StartPocId.ToString();

		public string Status => STATUS; 

        // 3. [Status] (CAP, EMP, NOZ, TOF, ING, ERR)
        public string STATUS
        {
            get
            {
                switch (EndType)
                {
                    case EndPointType.Cap:      return "CAP";
                    case EndPointType.Empty:    return "EMP";
                    case EndPointType.Nozzle:   return "NOZ";
                    case EndPointType.Takeoff:  return "TOF";
                    case EndPointType.Equipment:return "EQP";
                    case EndPointType.Broken:   return "ING";
                    case EndPointType.Cycle:    return "CYL";
                    default:                    return "ERR"; 
                }
            }
        }
        /*
        // 4. [T: Name] (ObjectInstanceLog Name)
        // 로직: EndName(Tag) 대신, 실제 도달한 마지막 Node의 'NodeName'을 반환
        public string TargetInstanceName
        {
            get
            {
                if (this.STATUS == "ING")
                {
                    return "Open Pipe";
                }

                // Steps가 있다면 마지막 단계의 NodeName(DB에서 가져온 Name)을 반환
                var lastStep = Steps.LastOrDefault();
                return lastStep?.NodeName ?? EndName ?? "-";
            }
        }*/

        // 5. [Tag Valid] (IsMatched)
        public string TagValidDisplay => IsMatched.ToString();

        // 6. [Hop Count] (Start Point 제외, -1 보정)
        // 0보다 작아지지 않도록 Max 처리
        public long HopCountDisplay => Math.Max(0, Steps.Count - 1);

        // 7. [Length]
        public double LengthDisplay => TotalLength;

        // 8. [Traced Time]
        public string TracedTimeDisplay => TracedAt.ToString("yyyy-MM-dd HH:mm:ss");


        public string FinalNodeName => EndName;
		
		// [New] 1. 검수 결과 필드
        public bool IsValidationTarget { get; set; } = true; // 검수 대상 여부
        public long FailCount { get; set; } = 0;              // 오류 개수
        public List<string> IssueList { get; set; } = new List<string>(); // 발생한 이슈 목록 (예: "연결누락", "규격불일치")
        
        // 엑셀/Grid 출력을 위한 문자열 (이슈들을 콤마로 연결)
        public string IssuesDisplay => IssueList != null && IssueList.Any() ? string.Join(", ", IssueList) : "Pass";
		
		// [New] 2. Source (Start POC) 상세 정보 - 엑셀 S: 헤더용
        public string StartPocTag { get; set; }        // S: Connector Tag
        public string StartPocName { get; set; }       // S: Connector Name
        /// <summary>S: Connector Use — WEBDB CONNECTORUSE를 Y/N으로 정규화. InstanceId 조회.</summary>
        public string StartPocConnectorUse { get; set; }
        public string StartPocUtilityName { get; set; }       // S: Utility
        public string StartPocUtilityGroup { get; set; }  // S: Utility Group
        // [Modified] S: Size (double -> string) "25mm" 형태
        public string StartPocSize { get; set; }
        // S: Division 등은 StartNodeInfo에서 가져옴 (아래 Initialize 메서드 참고)

        public string StartPocInformation { get; set; } //S :Connector Information
        public string TargetConnectorInformation { get; set; } //T :Connector Information

                // [신규] S: Module Name, T: Module Name (26.05.01)
        public string StartPocModuleName { get; set; }
        public string TargetPocModuleName { get; set; }

        // [신규] S: PocNo, T: PocNo (26.05.01)
        public string StartPocNo { get; set; }
        public string TargetPocNo { get; set; }

        public string TargetPocGUID { get; set; }

        // [New] 3. Target (End Node) 상세 정보 - 엑셀 T: 헤더용
        public string TargetNodeTag { get; set; }           // T: Tag
        public string TargetNodeDivision { get; set; } // T: Division (MEP 변환 로직 적용됨)
        public string TargetNodeSubDivision1 { get; set; }          // T: Subdivision1
        public string TargetNodeSubDivision2 { get; set; }          // T: Subdivision2
        public string TargetPocConnectionTag { get; set; } // T: Connection Tag (EndPoc의 Reference)
        public string StartPocConnectionTag { get; set; } // S: Connection Tag (StartPoc의 Reference)

        public string TargetPocName { get; set; } //26.07.08
        /// <summary>T: Connector Tag — End POC.Tag (S: Connector Tag와 대칭).</summary>
        public string TargetPocTag { get; set; } //26.07.17
        /// <summary>T: Construction Code — End Node ObjectInstanceLog.CONSTRUCTIONCODE.</summary>
        public string TargetConstructionCode { get; set; }

        // [New] 1. Check Result Logic
        // FailCount가 0이면 Pass, 아니면 Fail
        public string CheckResult => FailCount == 0 ? "Pass" : "Fail";

        // [New] 2. Source Node Info (S: Subdivision / Division)
        public string StartNodeDivision { get; set; } // S: Division (Equipment / PlumbingComponent / …)
        public string StartNodeSubDivision1 { get; set; }
        public string StartNodeSubDivision2 { get; set; }
        /// <summary>Start node GetDotNetHashCode (Sheet1 Instance Id). From NodeInfo / cache v6+.</summary>
        public long InstanceNumericId { get; set; }
        /// <summary>S:BIM ID — ObjectInstanceLog.BIMID (cache v7+).</summary>
        public string BimId { get; set; }
        // (StartPocTag, Name, Utility, Group, Size는 기존 유지)

        // [New] 3. Target Info (T: Utility, T: Name 명확화)
        public string TargetNodeName { get; set; }    // T: Name (Node Name)
        public string TargetPocUtilityName { get; set; } // T: Utility (EndPoc Utility)

        // [Updated] 엑셀용 Issue Type (Fail Count 옆)
        // IssueList 내용을 콤마로 연결하여 보여줌
        public string IssueType => (IssueList != null && IssueList.Any()) ? string.Join(", ", IssueList) : "";

        public string Line { get; set; }

        //public string InspectResult { get; set; }
        public string feedback { get; set; }

        public int totalInvalidCount { get; set; }
        public int invalidCountForHU { get; set; }

        public string StartPocConnectedUtility { get; set; }
        public string TargetPocConnectedUtility { get; set; }

        public string TargetLateralNumber { get; set; }

        /// <summary>S: Construction Code — Start Node ObjectInstanceLog.CONSTRUCTIONCODE.</summary>
        public string ConstructionCode { get; set; }

        public string IssueBelongTo { get; set; }

        public int BranchedPipes { get; set; }

        public long StartEquipmentPZ { get; set; }
    }

}
