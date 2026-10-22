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

        // [New] 2. 검수결과 (Logic: Issue Count > 0 ? FAIL : PASS)
        public string CheckResult => CountERR > 0 ? "FAIL" : "PASS";

        // [New] 4. 설비수량 (해당 Tag를 가진 GUID의 개수)
        public int EquipmentCount { get; set; }

                // 엑셀 경로 등 기존 필드 유지
        public string ExcelFilePath { get; set; }

        // [신규] 작업자 정보 (설비별 최신 Pipe 작업 정보)
        public string LatestOpTime { get; set; }      // 작업날짜 (yyyy-MM-dd 형식)
        public string LatestUserName { get; set; }    // 작업자 이름
        public string LatestCompanyName { get; set; } // 작업자 귀속 회사
    }
}
