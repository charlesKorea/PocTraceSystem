using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PocTrace.Core.Models
{
    // HTML 리포트로 전달할 전체 데이터 꾸러미
    public class HtmlReportData
    {
        public string Title { get; set; }
        public string GeneratedDate { get; set; }
        public string SourceDbName { get; set; }

        // 상단 요약 카드용
        public int TotalEquipmentCount { get; set; }
        public int TotalPassCount { get; set; }
        public int TotalFailCount { get; set; }
        public double PassRate { get; set; }

        // 차트용 데이터 (예: 오류 유형별 카운트)
        public Dictionary<string, int> IssueTypeSummary { get; set; } = new Dictionary<string, int>();

        // 그리드용 상세 데이터 리스트 (기존 Summary 재활용 가능하지만, 가볍게 전송하기 위해 별도 정의 추천)
        public List<EquipmentTraceSummary> Details { get; set; }

        // [New] 시계열 데이터
        public List<object> History { get; set; } // object나 DailyStat 타입
    }
}
