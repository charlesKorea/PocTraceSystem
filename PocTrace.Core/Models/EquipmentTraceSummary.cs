using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PocTrace.Core.Models
{
    // [UI 통계용 모델] Grid에 300줄 대신 보여질 요약 행
    public class EquipmentTraceSummary
    {
        public string EquipmentTag { get; set; }
        public string EquipmentName { get; set; }
        public string EquipmentGuid { get; set; }

        public int TotalPocCount { get; set; }      // 설비의 순수 POC 개수
        public int DerivedBranchCount { get; set; } // 추적 결과 총 개수 (분기 포함)

        public int CountNOZ { get; set; }
        public int CountTOF { get; set; }
        public int CountEQP { get; set; }
        public int CountCAP { get; set; }
        public int CountEMP { get; set; }
        public long CountERR { get; set; } // ERR + Unknown
        public int CountING { get; set; } // Broken

        public long TotalHopCount { get; set; }
        public double TotalLength { get; set; }

        /// <summary>배치 분류. 미배치설비, 미설계 배치 설비.</summary>
        public string Placement { get; set; }
        public bool DdwColumns { get; set; }
        public string MajorClass { get; set; }
        public string MinorClass { get; set; }
        public string UploadConstructionCode { get; set; }
        public string DesignCompany { get; set; }
        public string MoveInDate { get; set; }
        public string DrawingNeedDate { get; set; }
        public string Remark { get; set; }

        // [New] 2. 검수결과 (Logic: Issue Count > 0 ? FAIL : PASS). 배치 분류가 있으면 그 문구.
        public string CheckResult => !string.IsNullOrEmpty(Placement) ? Placement : (CountERR > 0 ? "FAIL" : "PASS");

        // [New] 4. 설비수량 (해당 Tag를 가진 GUID의 개수)
        public int EquipmentCount { get; set; }

                // 엑셀 경로 등 기존 필드 유지
        public string ExcelFilePath { get; set; }

        // [신규] 작업자 정보 (설비별 최신 Pipe 작업 정보)
        public string LatestOpTime { get; set; }      // 작업날짜 (yyyy-MM-dd 형식)
        public string LatestUserName { get; set; }    // 작업자 이름
        public string LatestCompanyName { get; set; } // 작업자 귀속 회사

        /// <summary>같은 Tag 안의 설비 이름별 수량. Summary 아래 표와 Web 결과에서 쓴다.</summary>
        public List<EquipmentNamePart> Names { get; set; } = new List<EquipmentNamePart>();
    }

    public class EquipmentNamePart
    {
        public string Name { get; set; }
        public string Division { get; set; }
        public string Subdivision1 { get; set; }
        public string Subdivision2 { get; set; }
        public int PocCount { get; set; }
        public int ErrorPocCount { get; set; }
        public double LengthM { get; set; }
    }
}
