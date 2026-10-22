/**
 * PocTrace Language Resource (V3.4)
 */
const TRANSLATIONS = {
    'zh': {
        'nav_summary': '综合摘要',
        'kpi_total_eq': '验收设备总数量',
        'kpi_target': 'EQPID别',
        'kpi_eq_pass': '设备合格率',
        'kpi_poc_pass': 'POC合格率 (核心)',
        'kpi_issue_fail': '问题发生 (不良率)',
        'chart_trend_comparative': '各线质量趋势比较 (平均合格率)',
        'chart_throughput': '每日验证量 (POC + H/U)',
        'label_hookup_pass': 'Hookup合格',
        'label_quality_point': '质量分(%)',
        'label_hu_no': 'H/U编号',
        'label_issue_belong': '问题归属',
        'label_rfu': 'RFU (预留)',
        'label_hookup_pass_rate': 'H/U合格率(%)',
        'label_hookup_error': 'H/U错误',
        'label_hu_validation': 'H/U编号验证',
        'chart_trend_dual': '每日趋势分析',
        'chart_error': '错误类型分析',
        'chart_process': '各工艺别现况',
        'btn_reset': '重置',
        'btn_export': '导出Excel',
        'btn_view': '查看',
        'header_eqp': '设备 ID',
        'header_cc': '建设Code',
        'header_proc': '工艺',
        'header_res': '验证结果',
        'header_issue': '问题 POC',
        'header_qty': '设备数量',
        'header_poc': 'POC数量',
        'header_detail': '详细数据',
        'label_pass': 'PASS',
        'label_fail': 'FAIL',
        'unit_sets': '台',
        'unit_ea': '个',
        'th_eq_qty': '设备数量',
        'card_poc_pass_rate': 'POC合格率',
        'card_eq_pass_rate': '设备合格率',
        'card_issue_fail': '问题POC数量',
        'header_poc_rate': 'PASS率',
        'lbl_similar_model': '系列设备分析',
        'lbl_main_name': '主设备名称',
        'lbl_pass_rate': 'PASS率',
        'lbl_issue_poc': '问题POC数量',
        'lbl_diff': '和昨天对比',
        'btn_excel_sim': '分析结果导出Excel',
        'lbl_type': '区分',
        'lbl_target': '基准对象',
        'lbl_similar': '系列设备',
        'lbl_avg': '平均值',
        'lbl_issue_belong': 'Issue归属',
    },
    'en': {
        'nav_summary': 'Summary',
        'kpi_total_eq': 'TOTAL EQP ID',
        'kpi_target': 'Target Sets',
        'kpi_eq_pass': 'EQP PASS RATE',
        'kpi_poc_pass': 'POC PASS RATE',
        'kpi_issue_fail': 'ISSUE (FAIL RATE)',
        'chart_trend_comparative': 'Line Comparative Trend (Avg Pass Rate)',
        'chart_throughput': 'Daily Throughput (POC + H/U)',
        'label_hookup_pass': 'Hookup Pass',
        'label_quality_point': 'Quality Point(%)',
        'label_hu_no': 'H/U No',
        'label_issue_belong': 'Issue Attribution',
        'label_rfu': 'RFU (Reserved)',
        'label_hookup_pass_rate': 'H/U Pass(%)',
        'label_hookup_error': 'H/U Error',
        'label_hu_validation': 'H/U No Validation',
        'chart_trend_dual': 'Daily Trend',
        'chart_error': 'Error Types',
        'chart_process': 'Process Breakdown',
        'btn_reset': 'Reset',
        'btn_export': 'Export All',
        'btn_view': 'View',
        'header_eqp': 'EQP ID',
        'header_cc': 'Const. Code',
        'header_proc': 'PROCESS',
        'header_res': 'RESULT',
        'header_issue': 'ISSUE POC',
        'header_qty': 'EQP QTY',
        'header_poc': 'POC QTY',
        'header_detail': 'DETAIL',
        'label_pass': 'PASS',
        'label_fail': 'FAIL',
        'unit_sets': 'Sets',
        'unit_ea': 'EA',
        'th_eq_qty': 'EQP QTY',
        'card_poc_pass_rate': 'POC PASS%',
        'card_eq_pass_rate': 'EQP PASS%',
        'card_issue_fail': 'ISSUE POC',
        'header_poc_rate': 'PASS%',
        'lbl_similar_model': 'Similar Model Analysis',
        'lbl_main_name': 'Main Eq Name',
        'lbl_pass_rate': 'Pass Rate',
        'lbl_issue_poc': 'Issue POCs',
        'lbl_diff': 'vs Yesterday',
        'btn_excel_sim': 'Export Analysis',
        'lbl_type': 'Type',
        'lbl_target': 'Target',
        'lbl_similar': 'Similar',
        'lbl_avg': 'Average',
        'lbl_issue_belong': 'Issue Attribution Analysis'
    },
    'ko': {
        'nav_summary': '종합 요약',
        'kpi_total_eq': '총 설비 수량',
        'kpi_target': '검수 대상 설비',
        'kpi_eq_pass': '설비合格率',
        'kpi_poc_pass': 'POC合格率 (핵심)',
        'kpi_issue_fail': '이슈 발생 (불량률)',
        'chart_trend_comparative': '라인별 품질 추세 비교 (평균 Pass Rate)',
        'chart_throughput': '일일 검수 물량 (POC + H/U)',
        'label_hookup_pass': 'Hookup Pass',
        'label_quality_point': 'Quality Point(%)',
        'label_hu_no': 'H/U No',
        'label_issue_belong': '오류 원인 귀속',
        'label_rfu': 'RFU (예비)',
        'label_hookup_pass_rate': 'H/U Pass(%)',
        'label_hookup_error': 'H/U Error',
        'label_hu_validation': 'H/U No 검증',
        'chart_trend_dual': '일별 추세 분석',
        'chart_error': '오류 유형 분석',
        'chart_process': '공정별 검수 현황',
        'btn_reset': '초기화',
        'btn_export': '전체 엑셀 저장',
        'btn_view': '상세보기',
        'header_eqp': '설비 ID',
        'header_cc': '건설코드',
        'header_proc': '공정명',
        'header_res': '검수결과',
        'header_issue': '이슈 POC',
        'header_qty': '설비 수',
        'header_poc': 'POC 수',
        'header_detail': '상세보기',
        'label_pass': 'PASS',
        'label_fail': 'FAIL',
        'unit_sets': '대',
        'unit_ea': '건',
        'th_eq_qty': '설비 수량',
        'card_poc_pass_rate': 'POC PASS%',
        'card_eq_pass_rate': 'EQP PASS%',
        'card_issue_fail': 'ISSUE POC',
        'header_poc_rate': 'PASS%',
        'lbl_similar_model': '유사 모델 비교 분석',
        'lbl_main_name': '메인 설비명',
        'lbl_pass_rate': '합격률',
        'lbl_issue_poc': '이슈 POC 수',
        'lbl_diff': '전일 대비',
        'btn_excel_sim': '분석결과 내보내기',
        'lbl_type': '구분',
        'lbl_target': '기준 설비',
        'lbl_similar': '유사 설비',
        'lbl_avg': '평균값',
        'lbl_issue_belong': '오류 원인 귀속',
    }
};

// 현재 언어 가져오기 Helper
function getLang() {
    return localStorage.getItem('poc_lang') || 'ko';
}

// 텍스트 반환 Helper (JS 템플릿 리터럴용)
function T(key) {
    const lang = getLang();
    return (TRANSLATIONS[lang] && TRANSLATIONS[lang][key]) ? TRANSLATIONS[lang][key] : key;
}

// [수정] window 객체에도 T 함수 등록 (app.js에서 접근 가능하도록)
window.T = T;
window.langT = T;
window.getLang = getLang;
window.applyLanguage = applyLanguage;

// UI 전체 언어 적용 (정적 요소 + Navbar)
function applyLanguage() {
    const lang = getLang();
    $('[data-i18n]').each(function() {
        const key = $(this).data('i18n');
        if (TRANSLATIONS[lang] && TRANSLATIONS[lang][key]) {
            $(this).text(TRANSLATIONS[lang][key]);
        }
    });
}