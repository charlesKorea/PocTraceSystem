/**
 * PocTrace Interactive Report App (V2.3 - UX Final)
 */

'use strict';

// lodash, dayjs는 window 객체에서 직접 접근 (스크립트 로드 순서 보장됨)
const _ = window._;
const dayjs = window.dayjs;

const LINE_ALIAS = { 'x1': 'X1', 'x2': 'X2-PH1', 'x2_2': 'X2-PH2', 'x2_m': 'X2-M' };
const getLineName = (dbName) => (window.PocTrace && typeof PocTrace.getLineName === 'function')
    ? PocTrace.getLineName(dbName)
    : (LINE_ALIAS[dbName] || String(dbName || '').toUpperCase());
const formatNum = (n) => n ? n.toLocaleString() : '0';

// T 함수는 lang.js가 window.T로 제공하므로 그대로 사용
// (lang.js가 먼저 로드되므로 window.T가 이미 정의됨)

let charts = [];
let dataTableInstance = null;

$(document).ready(function () {
    console.log("PocTrace App V2.3 Started.");
    // 1. Language Init
    const savedLang = localStorage.getItem('poc_lang') || 'ko';
    $('#lang-selector').val(savedLang);
    // lang.js가 window에 등록한 함수 사용
    if (typeof window.applyLanguage === 'function') {
        window.applyLanguage();
    }

    const themeDark = document.documentElement.getAttribute('data-theme') === 'dark';
    $('#theme-toggle').attr('aria-pressed', themeDark ? 'true' : 'false');
    $('#theme-toggle').on('click', function () {
        const next = document.documentElement.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        localStorage.setItem('poc_theme', next);
        location.reload();
    });

    $('#lang-selector').on('change', function() {
        localStorage.setItem('poc_lang', $(this).val());
        location.reload();
    });

    const PT = window.PocTrace || {};
    if (typeof IS_RULES_PAGE !== 'undefined' && IS_RULES_PAGE === true && typeof PT.renderRulesPage === 'function') {
        PT.renderRulesPage(
            typeof RULES_CATALOG !== 'undefined' ? RULES_CATALOG : [],
            typeof RULES_HISTORY !== 'undefined' ? RULES_HISTORY : [],
            typeof RULES_META !== 'undefined' ? RULES_META : {}
        );
    } else if (typeof IS_PROGRESS_PAGE !== 'undefined' && IS_PROGRESS_PAGE === true && typeof PT.renderProgressPage === 'function') {
        PT.renderProgressPage(
            typeof PROGRESS_DATA !== 'undefined' ? PROGRESS_DATA : [],
            typeof PROGRESS_HISTORY !== 'undefined' ? PROGRESS_HISTORY : [],
            typeof PROGRESS_META !== 'undefined' ? PROGRESS_META : {}
        );
    } else if (typeof IS_SUMMARY !== 'undefined' && IS_SUMMARY === true) {
        renderSummaryPage(SUMMARY_DATA, typeof HISTORY_DATA_MAP !== 'undefined' ? HISTORY_DATA_MAP : {});
    } else if (typeof REPORT_DATA !== 'undefined' && typeof REPORT_META !== 'undefined') {
        renderDetailPage(REPORT_DATA, REPORT_META, typeof HISTORY_DATA !== 'undefined' ? HISTORY_DATA : []);
    }

    if (typeof PT.rebuildLineNav === 'function') PT.rebuildLineNav();
    if (typeof PT.mountCommandBar === 'function') PT.mountCommandBar();
    applyNavAlias();

    window.addEventListener('resize', () => charts.forEach(c => c && c.resize()));
    
    // [수정] Trend Chart 버튼 이벤트 리스너
    $(document).on('click', '#trend-buttons .btn', function() {
        const mode = $(this).data('mode');
        window.updateTrend(mode);
    });
});


function applyNavAlias() {
    $('.nav-link').each(function() {
        const text = $(this).text().trim();
        // ?�스?�에 x1, x2 ?�이 ?�함?�어 ?�으�?Alias�?교체
        for (const [key, value] of Object.entries(LINE_ALIAS)) {
            if (text.toLowerCase().includes(key.toLowerCase() + ' line')) {
                // "x1 Line" -> "X1 Line"
                $(this).html($(this).html().replace(new RegExp(key, 'i'), value.split(' ')[0])); 
                // ?��? ?�순?�게: $(this).text(value); �???��?�도 ??(?�이�??��? 주의)
            }
        }
        // report_x1.html ?�의 링크?�서??Alias ?�용???�해, 
        // href ?�성??보고 ?�스?��? 강제�?맞추??로직?????�실??
        const href = $(this).attr('href');
        if (href && href.includes('report_')) {
            const dbKey = href.replace('report_', '').replace('.html', '');
            if (LINE_ALIAS[dbKey]) {
                const iconHtml = $(this).find('i').prop('outerHTML') || ''; // ?�이�??��?
                $(this).html(`${iconHtml} ${LINE_ALIAS[dbKey]}`);
            }
        }
    });
}


// ==========================================
// 1. Data Aggregation
// ==========================================
function aggregateData(rawData) {
    // [Fix 1] Group by 'StartEquipmentTag' (not Guid)
    const grouped = _.groupBy(rawData, 'StartEquipmentTag');
    
    const eqpStats = Object.keys(grouped).map(tag => {
        const pocs = grouped[tag];
        const first = pocs[0];
        
        // [Fix 2] ?�체 ?�비 ?�량 계산
        // pocs 배열?�는 ?�당 Tag???�한 *모든* 결과가 ?�어?�어???�니??
        // _.uniqBy???�정 ??Guid)�?기�??�로 중복???�거?�니??
        // ?�라???�슈가 ?�든 ?�든, StartEquipmentGuid가 ?�르�?카운?�됩?�다.
        const uniqueGuids = _.uniqBy(pocs, 'StartEquipmentGuid'); 
        const eqpQty = uniqueGuids.length;
        let proc = first.SourceSub2 || 'Common'; 
        const constCode = first.ConstructionCode || first.constructionCode || '-';
        const failPocCount = pocs.filter(p => p.FailCount > 0).length;
        const totalPoc = pocs.length;

        return {
            Guid: first.StartEquipmentGuid, 
            Tag: tag,
            ConstCode: constCode, // [Fix 1] New Field
            Name: first.StartEquipmentName,
            Process: proc,
            Result: failPocCount > 0 ? 'FAIL' : 'PASS',
            TotalPoc: totalPoc,
            PassPoc: totalPoc - failPocCount,
            FailPoc: failPocCount,
            
            EqpQty: eqpQty, // [Fix 2] ?�확??물리???�비 ?�량
            
            IssueCount: pocs.reduce((sum, p) => sum + (p.IssueList ? p.IssueList.length : 0), 0),
            // BreakdownCount ?�거??
            RawPocs: pocs, 
            SearchKeys: _.flatMap(pocs, 'IssueList').join(' ') 
        };
    });

    return { raw: rawData, eqpList: eqpStats };
}

// ==========================================
// 2. Summary Page (Executive Dashboard)
// ==========================================
function renderSummaryPage(data, historyMap) {
    $('#meta-date').text('Integrated Dashboard');
    
    // [Fix 2] Header Date Display
    const today = new Date().toLocaleDateString();
    // [Point 2] Layout with Executive Charts
    const html = `
        <div class="row" id="summary-cards"></div>
        
        <div class="row mt-4">
            <div class="col-md-8">
                <div class="chart-container-wrapper">
                    <h5 class="fw-bold text-dark mb-3"><i class="bi bi-graph-up-arrow"></i> ${T('chart_trend_comparative')}</h5>
                    <div id="compLineChart" class="chart-body-lg"></div>
                </div>
            </div>
            <div class="col-md-4">
                <div class="chart-container-wrapper">
                    <div class="d-flex justify-content-between">
                        <h5 class="fw-bold text-dark mb-3"><i class="bi bi-bar-chart-fill"></i> ${T('chart_throughput')}</h5>
                        <span class="badge bg-light text-dark border align-self-start">${today}</span>
                    </div>
                    <div id="throughputChart" class="chart-body-lg"></div>
                </div>
            </div>
        </div>
    `;
    $('#main-container').html(html);

    // Cards Generation
    data.forEach(db => {
        if (db.Failed) {
            const lineName = getLineName(db.DbName);
            const err = (db.Error || 'DB query failed').toString();
            $('#summary-cards').append(`
            <div class="col-md-6 col-lg-4 mb-4">
                <div class="card shadow-sm h-100 border-top border-4 border-danger">
                    <div class="card-header bg-white">
                        <h5 class="fw-bold text-danger mb-0">${lineName}</h5>
                    </div>
                    <div class="card-body">
                        <div class="small text-danger fw-bold mb-2">이 라인은 이번 배치에서 실패했습니다. 이전 리포트는 갱신되지 않았습니다.</div>
                        <div class="small text-muted">${err}</div>
                    </div>
                </div>
            </div>`);
            return;
        }
        const lineName = getLineName(db.DbName);
        const totalEq = db.TotalEquip || 0;
        const passEq = db.PassEquipCount || 0;
        const eqRate = totalEq > 0 ? ((passEq / totalEq) * 100).toFixed(1) : 0;

        const totalPoc = db.TotalPoc || 0;
        const passPoc = db.PassPocCount || 0;
        const pocPassRate = totalPoc > 0 ? ((passPoc / totalPoc) * 100).toFixed(1) : 0;
        const failPoc = db.FailPocCount || 0;
        const pocFailRate = totalPoc > 0 ? ((failPoc / totalPoc) * 100).toFixed(1) : 0;

                // [수정] Calculate Trend (vs Yesterday) - AvgPassRate 기준
        let trendHtml = `<span class="trend-badge trend-neutral">-</span>`;
        const history = historyMap[db.DbName];
        if (history && history.length >= 2) {
            const yesterday = history[history.length - 2];
            // AvgPassRate가 있으면 사용, 없으면 RatePoc
            const prevRate = yesterday.AvgPassRate || yesterday.RatePoc;
            const currentRate = db.AvgPassRate || pocPassRate;
            const diff = (currentRate - prevRate).toFixed(1);
            
            if (diff > 0) trendHtml = `<span class="trend-badge trend-up">↑${diff}%</span>`;
            else if (diff < 0) trendHtml = `<span class="trend-badge trend-down">↓${Math.abs(diff)}%</span>`;
            else trendHtml = `<span class="trend-badge trend-neutral">- 0.0%</span>`;
        }
        
                        // [수정] Line 카드: POC PASS% -> Quality Point(%), Avg Pass 기준 trend
        // AvgPassRate = (POC Pass% + Hookup Pass%) / 2
        const avgPassRate = db.AvgPassRate || pocPassRate;
        const hookupPassRate = db.HookupPassRate || 0;
        const hookupTotal = db.HookupTotal || 0;
        const hookupPass = db.HookupPass || 0;
        
        // POC Pass% (기존 값)
        const pocPassPercent = totalPoc > 0 ? ((db.PassPocCount / totalPoc) * 100).toFixed(1) : 0;
        
        let cardHtml = `
            <div class="col-md-6 col-lg-4 mb-4">
                <div class="card shadow-sm h-100 border-top border-4 border-primary">
                    <div class="card-header bg-white d-flex justify-content-between align-items-center">
                        <h5 class="fw-bold text-primary mb-0">${lineName}</h5>
                        <a href="report_${db.DbName}.html" class="btn btn-sm btn-outline-dark">${T('btn_view')} <i class="bi bi-arrow-right"></i></a>
                    </div>
                    <div class="card-body">
                        <div class="mb-3 text-center">
                            <label class="small text-muted fw-bold">${T('label_quality_point')}</label>
                            <div class="d-flex justify-content-center align-items-center">
                                <span class="kpi-highlight text-primary">${avgPassRate}%</span>
                                ${trendHtml}
                            </div>
                            <div class="small text-muted">${T('lbl_diff')}</div>
                        </div>
                        <hr>
                                                <div class="row text-center">
                            <div class="col-6 border-end">
                                <label class="small text-muted fw-bold">${T('label_hu_no')} Pass(%)</label>
                                <div class="metric-num">${hookupPassRate}%</div>
                                <div class="small text-muted">${formatNum(hookupPass)}/${formatNum(hookupTotal)}</div>
                            </div>
                            <div class="col-6">
                                <label class="small text-muted fw-bold">POC Pass(%)</label>
                                <div class="metric-num">${pocPassPercent}%</div>
                                <div class="small text-muted">${formatNum(db.PassPocCount)}/${formatNum(totalPoc)}</div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;
        $('#summary-cards').append(cardHtml);
    });

    // Charts Generation
    initSummaryCharts(historyMap);
}

function initSummaryCharts(historyMap) {
        // 1. Comparative Trend (Line) - [수정] AvgPassRate 사용
    const compChart = echarts.init(document.getElementById('compLineChart'));
    charts.push(compChart);

    const dbNames = Object.keys(historyMap);
    // 날짜 집합구하기 (X축)
    let allDates = [];
    dbNames.forEach(db => allDates = _.union(allDates, historyMap[db].map(d => d.Date)));
    allDates.sort();

    // [수정] AvgPassRate (POC% + Hookup% 평균) 사용
    const seriesList = dbNames.map(db => ({
        name: getLineName(db),
        type: 'line', smooth: true, symbolSize: 8,
        data: allDates.map(date => {
            const day = historyMap[db].find(d => d.Date === date);
            // AvgPassRate가 있으면 사용, 없으면 기존 RatePoc
            return day ? (day.AvgPassRate || day.RatePoc) : null;
        }),
        label: { show: true, position: 'top', formatter: '{c}%' }
    }));

        compChart.setOption({
        tooltip: { trigger: 'axis' },
        legend: { bottom: 0 },
        grid: { left: '3%', right: '4%', bottom: '10%', top: '10%', containLabel: true },
        xAxis: { type: 'category', data: allDates, boundaryGap: false },
        yAxis: { type: 'value', max: 100, name: 'Avg Pass Rate (%)' },
        series: seriesList.map(s => ({
            ...s,
            areaStyle: {
                color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
                    { offset: 0, color: s.color || '#4361ee' },
                    { offset: 1, color: (s.color || '#4361ee') + '20' }
                ])
            }
        }))
    });

        // 2. Throughput Chart (Bar) - [수정] POC + H/U No, 색상 변경
    const tpChart = echarts.init(document.getElementById('throughputChart'));
    charts.push(tpChart);

    // [수정] POC 수량과 H/U No 수량 함께 표시
    // SUMMARY_DATA에서 Hookup 정보 가져오기
    const lastDayData = dbNames.map(dbName => {
        const summary = SUMMARY_DATA.find(s => s.DbName === dbName);
        const h = historyMap[dbName];
        const last = h[h.length - 1];
        return {
            name: getLineName(dbName),
            pocPass: last ? last.PassPoc : 0,
            pocFail: last ? last.FailPoc : 0,
            hookupPass: summary ? (summary.HookupPass || 0) : 0,
            hookupFail: summary ? ((summary.HookupTotal || 0) - (summary.HookupPass || 0)) : 0
        };
    });

        tpChart.setOption({
        tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
        legend: { bottom: 0 },
        grid: { left: '3%', right: '4%', bottom: '15%', top: '10%', containLabel: true },
        xAxis: { type: 'category', data: lastDayData.map(d => d.name) },
        yAxis: { type: 'value', name: 'Count' },
        series: [
            // POC Pass (흰색 텍스트)
            {
                name: 'POC Pass', type: 'bar', stack: 'poc', color: '#2a9d8f',
                data: lastDayData.map(d => d.pocPass),
                label: { show: true, formatter: (p) => p.value > 0 ? formatNum(p.value) : '', color: '#ffffff', fontWeight: 'bold' }
            },
            // POC Fail (노랑색 텍스트)
            {
                name: 'POC Fail', type: 'bar', stack: 'poc', color: '#e63946',
                data: lastDayData.map(d => d.pocFail),
                label: { show: true, formatter: (p) => p.value > 0 ? formatNum(p.value) : '', color: '#fbbf24', fontWeight: 'bold' }
            },
            // H/U No Pass (흰색 텍스트)
            {
                name: 'H/U No Pass', type: 'bar', stack: 'hookup', color: '#4895ef',
                data: lastDayData.map(d => d.hookupPass),
                label: { show: true, formatter: (p) => p.value > 0 ? formatNum(p.value) : '', color: '#ffffff', fontWeight: 'bold' }
            },
            // H/U No Fail (노랑색 텍스트)
            {
                name: 'H/U No Fail', type: 'bar', stack: 'hookup', color: '#d62828',
                data: lastDayData.map(d => d.hookupFail),
                label: { show: true, formatter: (p) => p.value > 0 ? formatNum(p.value) : '', color: '#fbbf24', fontWeight: 'bold' }
            }
        ]
    });
}

// ==========================================
// 3. Detail Page (Report)
// ==========================================
function renderDetailPage(dto, meta, history) {
    const lineName = getLineName(meta.dbName);
    const dbName = meta.dbName; // ?��? 링크??
    $('#meta-date').text(`Generated: ${meta.genDate}`);
    document.title = `Report [${lineName}]`;

    const eqList = dto.Equipments;
    
    // Stats Calculation (?��? C#?�서 계산??값이지�?JS?�서 ?�확??가??
    const totalEq = eqList.length;
    const passEq = eqList.filter(e => e.Result === 'PASS').length;
    const eqRate = totalEq > 0 ? ((passEq / totalEq) * 100).toFixed(1) : 0;
    
    // POC Stats (DTO???�함??
    const totalPoc = eqList.reduce((sum, e) => sum + e.TotalPoc, 0);
    const passPoc = eqList.reduce((sum, e) => sum + e.PassPoc, 0);
    const pocRate = totalPoc > 0 ? ((passPoc / totalPoc) * 100).toFixed(1) : 0;
    const totalIssues = eqList.reduce((sum, e) => sum + e.IssueCount, 0);
    const issueRate = totalPoc > 0 ? ((totalIssues / totalPoc) * 100).toFixed(1) : 0;


            // [신규] Hookup Stats 계산
    const hookupStats = dto.HookupStats || {};
    const totalHookup = hookupStats.TotalObjects || 0;
    const okHookup = hookupStats.OK || 0;
    const hookupPassRate = totalHookup > 0 ? ((okHookup / totalHookup) * 100).toFixed(1) : 0;
    const hookupErrorCount = (hookupStats.MISSING || 0) + (hookupStats.MISMATCH || 0);

    // [수정] KPI 카드 4개: Total Eq, POC Pass, Hookup Pass, Issues
    const html = `
        <div class="row mb-4">
             <div class="col-12 mb-3"><h3 class="fw-bold">${lineName} Report</h3></div>
             <div class="col-md-3"><div class="kpi-card total" id="card-1"></div></div>
             <div class="col-md-3"><div class="kpi-card pass" id="card-2"></div></div>
             <div class="col-md-3"><div class="kpi-card hookup" id="card-3"></div></div>
             <div class="col-md-3"><div class="kpi-card fail" id="card-4"></div></div>
        </div>

        <div class="row mb-4">
            <div class="col-12">
                <div class="chart-container-wrapper position-relative">
                    <div class="d-flex justify-content-between mb-2">
                         <h5 class="fw-bold">${T('chart_trend_dual')}</h5>
                                             <div class="btn-group" role="group" id="trend-buttons">
                            <button type="button" class="btn btn-outline-secondary btn-sm active" data-mode="day">Daily</button>
                            <button type="button" class="btn btn-outline-secondary btn-sm" data-mode="week">Weekly</button>
                            <button type="button" class="btn btn-outline-secondary btn-sm" data-mode="month">Monthly</button>
                         </div>
                    </div>
                    <div id="lineChart" class="chart-trend-lg"></div>
                </div>
            </div>
        </div>

                        <div class="row mb-4">
            <div class="col-md-6">
                <div class="chart-container-wrapper">
                    <h5 class="fw-bold">${T('chart_error')}</h5>
                    <div id="pieChartError" class="chart-body"></div>
                </div>
            </div>
            <div class="col-md-3">
                <div class="chart-container-wrapper">
                    <h5 class="fw-bold">${T('label_issue_belong')}</h5>
                    <div id="pieChartBelong" class="chart-body"></div>
                </div>
            </div>
                        <div class="col-md-3">
                <div class="chart-container-wrapper">
                    <h5 class="fw-bold"><i class="bi bi-check-circle"></i> ${T('label_hu_validation')}</h5>
                    <div id="pieChartHookup" class="chart-body"></div>
                </div>
            </div>
        </div>
        
        <div class="row mb-4">
            <div class="col-12">
                <div class="chart-container-wrapper">
                    <h5 class="fw-bold">${T('chart_process')}</h5>
                    <div id="barChart" class="chart-body-lg"></div>
                </div>
            </div>
        </div>
        
        <div class="row"><div class="col-12"><div class="card shadow-sm"><div class="card-body"><table id="mainTable" class="table table-hover w-100"></table></div></div></div></div>
    `;
    $('#main-container').html(html);

                // KPI Cards Injection (Applying T() and Layout)
        // [수정] 4개 카드: Total Eq, POC Pass, Hookup Pass, Issues + Error Badge + 그라데이션
    const eqErrorCount = totalEq - passEq;
    const pocErrorCount = totalPoc - passPoc;

    $('#card-1').html(`
        <div class="kpi-label">${T('kpi_total_eq')}</div>
        <div class="kpi-value">${formatNum(totalEq)}</div>
        <div class="kpi-sub-text">${T('kpi_target')}</div>
        ${eqErrorCount > 0 ? `<div class="mt-1"><span class="status-badge fail">${formatNum(eqErrorCount)} Err</span></div>` : ''}
    `);
    $('#card-2').html(`
        <div class="kpi-label">${T('card_poc_pass_rate')}</div>
        <div class="kpi-value">${pocRate}%</div>
        <div class="kpi-sub-text">${formatNum(passPoc)} / ${formatNum(totalPoc)}</div>
        ${pocErrorCount > 0 ? `<div class="mt-1"><span class="status-badge fail">${formatNum(pocErrorCount)} Err</span></div>` : ''}
    `);
    const hookupCardContent = totalHookup > 0
        ? `<div class="kpi-label">H/U Number Pass(%)</div><div class="kpi-value">${hookupPassRate}%</div><div class="kpi-sub-text">${formatNum(okHookup)} / ${formatNum(totalHookup)}</div>${hookupErrorCount > 0 ? `<div class="mt-1"><span class="status-badge fail">${formatNum(hookupErrorCount)} Err</span></div>` : ''}`
        : `<div class="kpi-label">H/U Number Pass(%)</div><div class="kpi-value text-muted">-</div><div class="kpi-sub-text">No Data</div>`;
    $('#card-3').html(hookupCardContent);
    $('#card-4').html(`
        <div class="kpi-label">${T('card_issue_fail')}</div>
        <div class="kpi-value text-danger">${formatNum(totalIssues)}</div>
        <div class="kpi-sub-text">${issueRate}% Rate</div>
    `);

    // ?�역 변?�에 ?�스?�리 ?�??(?�터링용)
    window.currentHistory = history; 

    initDetailCharts(dto, history); // dto ?�째�??��?
    initGrid(dto.Equipments, meta.dbName); // [Fix 3] Remove Length from Grid

}

// ==========================================
// 4. Detail Charts (Pre-calculated Data)
// ==========================================
let lineChartInstance = null; // 차트 ?�스?�스 ?�역 관�?
function initDetailCharts(dto, history) {
    // ----------------------------
    // A. Error Type Pie (Left)
    // ----------------------------
    initPieChart('pieChartError', dto.ErrorStats, 'No Error');

        // ----------------------------
    // B. [New] Issue Belong Pie (Right)
    // ----------------------------
    initPieChart('pieChartBelong', dto.BelongStats, 'No Data');

    // ----------------------------
    // C. [New] Hookup Validation Pie Chart
    // ----------------------------
    if (dto.HookupStats && dto.HookupStats.TotalObjects > 0) {
        const hookupData = [
            { value: dto.HookupStats.OK || 0, name: 'OK' },
            { value: dto.HookupStats.MISSING || 0, name: 'MISSING' },
            { value: dto.HookupStats.MISMATCH || 0, name: 'MISMATCH' },
            { value: dto.HookupStats.TAG_ERROR || 0, name: 'TAG_ERROR' },
            { value: dto.HookupStats.DATA_ERROR || 0, name: 'DATA_ERROR' }
        ].filter(d => d.value > 0);
        
        if (hookupData.length > 0) {
            const hookupChart = echarts.init(document.getElementById('pieChartHookup'));
            charts.push(hookupChart);
            hookupChart.setOption({
                tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
                legend: { top: '5%', left: 'center', type: 'scroll' },
                series: [{
                    type: 'pie', radius: ['40%', '65%'], center: ['50%', '60%'],
                    data: hookupData,
                    label: { show: true, formatter: '{b}\n{c} ({d}%)', fontSize: 10 },
                    itemStyle: {
                        color: function(params) {
                            const colors = {
                                'OK': '#22c55e',
                                'MISSING': '#ef4444',
                                'MISMATCH': '#f97316',
                                'TAG_ERROR': '#eab308',
                                'DATA_ERROR': '#a855f7'
                            };
                            return colors[params.name] || '#6366f1';
                        }
                    }
                }]
            });
        }
    } else {
        // Hookup 데이터가 없으면 빈 차트 또는 메시지
        const hookupChart = echarts.init(document.getElementById('pieChartHookup'));
        charts.push(hookupChart);
        hookupChart.setOption({
            title: { text: 'No Hookup Data', left: 'center', top: 'center', textStyle: { color: '#999' } },
            series: []
        });
    }

    // ----------------------------
    // C. Trend Chart (Initial: Daily)
    // ----------------------------
    updateTrend('day'); // 초기 ?�더�?
    
    /*
    const errorMap = dto.ErrorStats || {};
    const pieData = Object.keys(errorMap).map(k => ({ value: errorMap[k], name: k }));
    
    const pieChart = echarts.init(document.getElementById('pieChart'));
    charts.push(pieChart);
    pieChart.setOption({
        tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
        legend: { top: '5%', left: 'center', type: 'scroll' },
        series: [{
            type: 'pie', radius: ['40%', '65%'], center: ['50%', '60%'],
            data: pieData.length ? pieData : [{value:0, name:'No Error'}],
            label: { show: true, formatter: '{b}\n{c} ({d}%)', fontSize: 10 }
        }]
    });
    // Drill-down: Search text in Table (MainIssues 컬럼 검??
    pieChart.on('click', (params) => {
        if(dataTableInstance) { dataTableInstance.search(params.name).draw(); $('html, body').animate({ scrollTop: $("#mainTable").offset().top - 100 }, 500); }
    });
  */
            // ----------------------------
    // B. Bar Chart (Process Stats - [수정] POC + H/U No 분리 막대, Fail Rate 내림차순)
    // ----------------------------
    const procMap = dto.ProcessStats || {};
    
    // [수정] POC + Hookup 합계 계산
    const procStats = {};
    dto.Equipments.forEach(e => {
        if (!procStats[e.Process]) procStats[e.Process] = { pocPass: 0, pocFail: 0, hookupPass: 0, hookupFail: 0 };
        procStats[e.Process].pocPass += e.PassPoc;
        procStats[e.Process].pocFail += e.FailPoc;
        // Hookup은 전체에서 동일하게 분배 (비율 적용)
        const hookupTotal = dto.HookupStats?.TotalObjects || 0;
        const hookupOK = dto.HookupStats?.OK || 0;
        const hookupPassRate = hookupTotal > 0 ? hookupOK / hookupTotal : 0;
        const processTotalPoc = e.TotalPoc;
        const estimatedHookupPass = Math.round(processTotalPoc * hookupPassRate);
        const estimatedHookupFail = processTotalPoc - estimatedHookupPass;
        procStats[e.Process].hookupPass += estimatedHookupPass;
        procStats[e.Process].hookupFail += estimatedHookupFail;
    });

    // [수정] Fail Rate 기준 내림차순 정렬
    const categories = Object.keys(procMap).sort((a, b) => {
        const d1 = procStats[a];
        const d2 = procStats[b];
        const failRate1 = d1 ? ((d1.pocFail + d1.hookupFail) / (d1.pocPass + d1.pocFail + d1.hookupPass + d1.hookupFail) * 100) : 0;
        const failRate2 = d2 ? ((d2.pocFail + d2.hookupFail) / (d2.pocPass + d2.pocFail + d2.hookupPass + d2.hookupFail) * 100) : 0;
        return failRate2 - failRate1;
    });

        const barChart = echarts.init(document.getElementById('barChart'));
    charts.push(barChart);
    barChart.setOption({
        tooltip: { 
            trigger: 'axis', axisPointer: { type: 'shadow' },
            formatter: function(params) {
                let html = `<strong>${params[0].name}</strong><br/>`;
                params.forEach(p => {
                    if (p.seriesName === 'Fail Rate') {
                        html += `${p.marker} ${p.seriesName}: <b>${p.value}%</b><br/>`;
                    } else {
                        const color = p.seriesName.includes('Fail') ? '#fbbf24' : '#ffffff';
                        html += `${p.marker} ${p.seriesName}: <b style="color:${color}">${formatNum(p.value)}</b><br/>`;
                    }
                });
                return html;
            }
        },
        legend: { bottom: 0 },
        grid: { left: '3%', right: '4%', bottom: '15%', top: '10%', containLabel: true },
        xAxis: { type: 'category', data: categories, axisLabel: { rotate: 30 } },
        yAxis: [
            { type: 'value', name: 'Count' },
            { type: 'value', name: 'Fail Rate (%)', min: 0, max: 100, splitLine: { show: false } }
        ],
        series: [
            // POC (Stacked: Pass 아래, Fail 위)
            { 
                name: 'POC Pass', type: 'bar', stack: 'poc', color: '#2a9d8f',
                data: categories.map(k => procStats[k] ? procStats[k].pocPass : 0),
                label: { show: true, formatter: (p) => p.value>0?formatNum(p.value):'', color: '#ffffff', fontWeight: 'bold', position: 'top' }
            },
            { 
                name: 'POC Fail', type: 'bar', stack: 'poc', color: '#e63946',
                data: categories.map(k => procStats[k] ? procStats[k].pocFail : 0),
                label: { show: true, formatter: (p) => p.value>0?formatNum(p.value):'', color: '#fbbf24', fontWeight: 'bold', position: 'top' }
            },
            // H/U No (Stacked: Pass 아래, Fail 위)
            { 
                name: 'H/U No Pass', type: 'bar', stack: 'hookup', color: '#4895ef',
                data: categories.map(k => procStats[k] ? procStats[k].hookupPass : 0),
                label: { show: true, formatter: (p) => p.value>0?formatNum(p.value):'', color: '#ffffff', fontWeight: 'bold', position: 'top' }
            },
            { 
                name: 'H/U No Fail', type: 'bar', stack: 'hookup', color: '#d62828',
                data: categories.map(k => procStats[k] ? procStats[k].hookupFail : 0),
                label: { show: true, formatter: (p) => p.value>0?formatNum(p.value):'', color: '#fbbf24', fontWeight: 'bold', position: 'top' }
            },
            // Fail Rate (합계 기준)
            {
                name: 'Fail Rate', type: 'line', yAxisIndex: 1, color: '#6c757d',
                data: categories.map(k => {
                    const d = procStats[k];
                    if (!d) return 0;
                    const total = d.pocPass + d.pocFail + d.hookupPass + d.hookupFail;
                    const failTotal = d.pocFail + d.hookupFail;
                    return total > 0 ? ((failTotal / total) * 100).toFixed(1) : 0;
                }),
                label: { show: true, formatter: '{c}%', color: '#6c757d', position: 'top', fontWeight: 'bold' }
            }
        ]
    });
    barChart.on('click', (params) => {
        if (dataTableInstance) {
            dataTableInstance.search(params.name).draw();
            if (window.PocTrace && typeof PocTrace.scrollToNode === 'function') PocTrace.scrollToNode('#mainTable');
        }
    });
  /*
    // ----------------------------
    // C. Line Chart (Trend - [Fix 2] Issue Count Added)
    // ----------------------------
    const lineChart = echarts.init(document.getElementById('lineChart'));
    charts.push(lineChart);
    const dates = history.map(d => d.Date);
    const ratePocs = history.map(d => d.RatePoc);
    const issueCounts = history.map(d => d.IssuePocCount || 0); // [New]

    lineChart.setOption({
        tooltip: { trigger: 'axis' },
        legend: { bottom: 0 },
        xAxis: { type: 'category', data: history.map(d => d.Date) },
        yAxis: [{ type: 'value', name: 'Pass Rate (%)', max: 100 }, { type: 'value', name: 'Error Count' }],
        series: [
            { 
                name: T('card_poc_pass_rate'), type: 'line', color: '#4361ee',
                data: history.map(d => d.RatePoc),
                label: { show: true, formatter: '{c}%' } // Label
            },
            {
                name: T('lbl_issue_poc'), type: 'line', yAxisIndex: 1, color: '#f72585',
                data: history.map(d => d.IssuePocCount || 0),
                label: { show: true, formatter: (p) => formatNum(p.value) }, // Label + Comma
                lineStyle: { type: 'dashed' }
            }
        ]
    });
  */
}

// Pie Chart Helper (Reusable)
function initPieChart(domId, dataMap, emptyLabel) {
    const map = dataMap || {};
    const data = Object.keys(map).map(k => ({ value: map[k], name: k }));
    const chart = echarts.init(document.getElementById(domId));
    charts.push(chart);
    chart.setOption({
        tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
        legend: { top: '5%', left: 'center', type: 'scroll' },
        series: [{
            type: 'pie', radius: ['40%', '65%'], center: ['50%', '60%'],
            data: data.length ? data : [{value:0, name:emptyLabel}],
            label: { show: true, formatter: '{b}\n{c} ({d}%)', fontSize: 10 }
        }]
    });
}

// [Change 3] Trend Chart Update Logic
window.updateTrend = function(mode) {
    // Button Active State Toggle
    $('#trend-buttons .btn').removeClass('active');
    $(`#trend-buttons .btn[data-mode="${mode}"]`).addClass('active');

    const rawHistory = window.currentHistory;
    // 1. Filter: Year to Date (Assuming format yyyy-MM-dd)
    const currentYear = new Date().getFullYear().toString();
    const yearData = rawHistory.filter(d => d.Date.startsWith(currentYear));

    let processedData = [];

    // 2. Aggregate Data based on Mode
    if (mode === 'day') {
        processedData = yearData;
    } else if (mode === 'week') {
        processedData = groupByTime(yearData, 'week');
    } else if (mode === 'month') {
        processedData = groupByTime(yearData, 'month');
    }

    // 3. Render Chart
    renderTrendChart(processedData);
};

// Helper: Group By Week/Month
function groupByTime(data, unit) {
    const grouped = _.groupBy(data, d => {
        const date = dayjs(d.Date);
        if (unit === 'week') return date.format('YYYY-W') + 'w'; // 2026-05w
        return date.format('YYYY-MM'); // 2026-02
    });

    return Object.keys(grouped).map(key => {
        const group = grouped[key];
        // 합산: Total, Pass, IssueCount, HookupErrorCount
        const totalPoc = _.sumBy(group, 'TotalPoc');
        const passPoc = _.sumBy(group, 'PassPoc');
        const issueCnt = _.sumBy(group, 'IssuePocCount');
        const hookupErrorCnt = _.sumBy(group, 'HookupErrorCount') || 0;
        // 비율 계산
        const rate = totalPoc > 0 ? ((passPoc / totalPoc) * 100).toFixed(1) : 0;
        // AvgPassRate 계산 (History에 있으면 사용, 없으면 RatePoc)
        const avgPassRate = _.meanBy(group, d => d.AvgPassRate || d.RatePoc) || rate;
        
        return {
            Date: key,
            RatePoc: rate,
            AvgPassRate: parseFloat(avgPassRate.toFixed(1)),
            IssuePocCount: issueCnt,
            HookupErrorCount: hookupErrorCnt
        };
    });
}

function renderTrendChart(data) {
    const dom = document.getElementById('lineChart');
    if (lineChartInstance) lineChartInstance.dispose();
    lineChartInstance = echarts.init(dom);
    charts.push(lineChartInstance);

    // [수정] Hookup 데이터도 함께 표시 (RatePoc + AvgPassRate + IssuePocCount)
    // AvgPassRate가 있으면 사용, 없으면 RatePoc
        lineChartInstance.setOption({
        tooltip: { trigger: 'axis' },
        legend: { bottom: 0 },
        grid: { left: '3%', right: '4%', bottom: '15%', top: '10%', containLabel: true },
        xAxis: { type: 'category', data: data.map(d => d.Date) },
        yAxis: [
            { type: 'value', name: 'Pass Rate (%)', max: 100 },
            { type: 'value', name: 'Count', splitLine: { show: false } }
        ],
        series: [
            // POC Pass Rate (숫자 표시)
            { 
                name: 'POC Pass(%)', type: 'line', data: data.map(d => d.RatePoc), 
                smooth: true, color: '#4361ee', symbol: 'circle', symbolSize: 8,
                label: { show: true, formatter: '{c}%', position: 'top' }
            },
            // H/U Pass Rate (숫자 표시)
            {
                name: 'H/U Pass(%)', type: 'line', data: data.map(d => {
                    if (d.AvgPassRate && d.RatePoc) {
                        let huRate = d.AvgPassRate * 2 - d.RatePoc;
                        return Math.max(0, Math.min(100, huRate)).toFixed(1);
                    }
                    return null;
                }), 
                smooth: true, color: '#4895ef', symbol: 'diamond', symbolSize: 8,
                label: { show: true, formatter: '{c}%', position: 'top' }
            },
            // Issue POC Count
            {
                name: T('lbl_issue_poc'), type: 'line', yAxisIndex: 1, data: data.map(d => d.IssuePocCount || 0), 
                smooth: true, color: '#f72585', symbol: 'triangle', symbolSize: 8,
                lineStyle: { type: 'dashed' }
            },
            // H/U Error Count
            {
                name: T('label_hookup_error'), type: 'line', yAxisIndex: 1, data: data.map(d => d.HookupErrorCount || 0), 
                smooth: true, color: '#d62828', symbol: 'rect', symbolSize: 8,
                lineStyle: { type: 'dashed' }
            }
        ]
    });
}

// ==========================================
// 5. DataTables (Custom Layout)
// ==========================================
function initGrid(eqList, dbName) {
    if (dataTableInstance) { dataTableInstance.destroy(); $('#mainTable').empty(); }

    // ?��? ?�일 경로 (?��? 경로)
    const excelLink = `./Excel/${dbName}_Error_Detail.xlsx`;

    dataTableInstance = $('#mainTable').DataTable({
        data: eqList,
        dom: '<"dt-top-container" <"dt-left-group" <"dt-custom-title"> <"dt-result-filter"> l > <"dt-right-group" f <"btn-excel-wrapper"> > >rtip',

        initComplete: function() {
            const api = this.api();
            dataTableInstance = api;
            if (window.PocTrace && PocTrace.state) PocTrace.state.dataTableInstance = api;

            $("div.dt-custom-title").html(`<h5 class="m-0 fw-bold text-dark"><i class="bi bi-table"></i> ${T('header_detail')}</h5>`);

            const rows = eqList || [];
            const failCount = rows.filter(function (e) { return e.Result === 'FAIL'; }).length;
            const passCount = rows.filter(function (e) { return e.Result === 'PASS'; }).length;
            $("div.dt-result-filter").html(
                '<div class="filter-chips" id="result-filter-group" role="group" aria-label="' + T('header_res') + '">' +
                '<button type="button" class="filter-chip fail' + (failCount > 0 ? ' active' : '') + '" data-filter="FAIL">' + T('filter_fail') + ' ' + failCount + '</button>' +
                '<button type="button" class="filter-chip' + (failCount > 0 ? '' : ' active') + '" data-filter="ALL">' + T('filter_all') + ' ' + rows.length + '</button>' +
                '<button type="button" class="filter-chip pass" data-filter="PASS">' + T('filter_pass') + ' ' + passCount + '</button>' +
                '</div>'
            );
            if (failCount > 0) api.column(3).search('FAIL').draw();

            $('#result-filter-group').on('click', 'button', function() {
                const mode = $(this).data('filter');
                $('#result-filter-group button').removeClass('active');
                $(this).addClass('active');
                api.search('');
                api.column(3).search(mode === 'ALL' ? '' : String(mode)).draw();
            });

            $("div.btn-excel-wrapper").html(`
                <a href="${excelLink}" class="btn btn-success btn-sm fw-bold">
                    <i class="bi bi-file-earmark-excel"></i> ${T('btn_export')}
                </a>
            `);
        },
        
        columns: [
            { title: T('header_eqp'), data: "Tag", render: d => `<span class="fw-bold">${d}</span>` },
            
            // [Fix 1] 2. Construction Code (New)
            { title: T('header_cc'), data: "ConstCode", className: "text-center" },

            // 3. Process
            { title: T('header_proc'), data: "Process" },
            
            // 4. Result
            { title: T('header_res'), data: "Result", render: function (d, type) {
                if (type && type !== 'display') return d || '';
                return d === 'FAIL'
                    ? '<span class="status-badge fail">' + T('label_fail') + '</span>'
                    : '<span class="status-badge pass">' + T('label_pass') + '</span>';
            } },

                        
            // 5. EQP QTY
            { title: T('th_eq_qty'), data: "EqpQty", className: "text-center", render: formatNum },
            
            // [Fix 1] Length Column Added
            //{ title: "Length", data: "Length", className: "text-center", defaultContent: "-" },
            
             // [변�? 5. POC QTY (?�서 ?�동)
            { title: T('header_poc'), data: "TotalPoc", className: "text-center", render: formatNum },
            
            // [변�? 6. ISSUE POC (?�서 ?�동)
            { title: T('header_issue'), data: "IssueCount", className: "text-center", render: (d, t, r) => d > 0 ? `<span class="text-danger fw-bold">${formatNum(d)}</span>` : formatNum(d) },

            // [?�규] 7. POC Pass Rate (New Column)
            { 
                title: T('header_poc_rate') || 'POC Rate', 
                data: null, 
                className: "text-center",
                render: (data, type, row) => {
                    const rate = row.TotalPoc > 0 ? ((row.PassPoc / row.TotalPoc) * 100).toFixed(1) : 0;
                    // 100%�??��??? ?�니�?기본??(?��? 90% 미만 빨간????조건부 ?�식 가??
                    const colorClass = rate < 100 ? 'text-danger' : 'text-primary';
                    return `<span class="${colorClass} fw-bold">${rate}%</span>`;
                }
            },
                        
            // [중요] ?�세 ?�이??Raw Pocs)가 ?�으므�?MainIssues(?�스??�??��? 컬럼?�로 추�??�여 검??지??
            { data: "MainIssues", visible: false }, 

            // [Fix 1] Two Buttons: Detail(Lite) & Analyze
            {
                title: T('header_detail'), data: null, className: "text-center", width: "140px",
                render: (data, type, row) => {
                    let html = `<div class="btn-group btn-group-sm">`;
                    
                    // Detail Button (Error View)
                    if (row.Result === 'FAIL' || row.IssueCount > 0) {
                        html += `<button class="btn btn-detail-view view-error-btn" data-tag="${row.Tag}" title="View Errors"><i class="bi bi-search"></i></button>`;
                    } else {
                        html += `<button class="btn btn-light disabled"><i class="bi bi-search"></i></button>`;
                    }

                    // [Fix 1] Analyze Button (Similar Model)
                    html += `<button class="btn btn-info text-white view-sim-btn" data-tag="${row.Tag}" title="Compare Similar Models"><i class="bi bi-intersect"></i></button>`;
                    
                    html += `</div>`;
                    return html;
                }
            }
        ],
        // 기본 ?�렬: Result가 FAIL??�??�선 ?�시
        order: [[3, 'asc']] 
    });

    // Event Delegation
    $('#mainTable tbody').off('click');
    $('#mainTable tbody').on('click', '.view-error-btn', function() {
        const tag = $(this).data('tag');
        const rowData = dataTableInstance.rows().data().toArray().find(r => r.Tag === tag);
        openLiteModal(rowData, excelLink);
    });
    // [Fix 1] Similar Model Event
    $('#mainTable tbody').on('click', '.view-sim-btn', function() {
        const tag = $(this).data('tag');
        analyzeSimilarModels(tag);
    });
}

// ==========================================
// 6. Lite Modal (Raw Data ?�음)
// ==========================================
function openLiteModal(rowData, excelLink) {
    if (!rowData) return;
    // [Fix 1] Title: Tag (ConstCode)
    const title = `${rowData.Tag} <span class="text-muted fs-6">(${rowData.ConstCode || 'No Code'})</span>`;
    const bodyHtml = `
        <div class="text-center mb-4">
            <h4 class="fw-bold text-dark">${title}</h4>
            <div class="text-primary fw-bold mb-2">${rowData.MainName || ''}</div>
            <div class="badge bg-danger fs-6 mb-2">${rowData.IssueCount} Issues Found</div>
            <p class="text-muted small">
                ?�세 ?�이?�는 ?�?�량 처리�??�해 ?��? ?�일�??�공?�니??<br>
                Detailed data is provided in the Excel file for performance.<br>
                为了处理大用?�数??���?��??��?�据?��?请参?�Excel?�件
            </p>
        </div>

        <div class="card bg-light mb-3">
            <div class="card-body">
                <h6 class="fw-bold border-bottom pb-2 mb-2">?�� 주요 ?�슈 主要??��类型</h6>
                <p class="text-danger fw-bold" style="white-space: pre-wrap;">${rowData.MainIssues}</p>
            </div>
        </div>

        <div class="d-grid gap-2">
            <a href="${excelLink}" class="btn btn-success btn-lg shadow-sm">
                <i class="bi bi-file-earmark-excel-fill"></i> Download Full Equipments Excel Report
            </a>
            <button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">Close</button>
        </div>
    `;

    $('#detailModal .modal-body').html(bodyHtml);
    new bootstrap.Modal(document.getElementById('detailModal')).show();
}


// ==========================================
// 6. Modal (Expanded Columns)
// ==========================================
let currentModalPocs = [];

function openErrorModal(guid) {
    // guid??First Poc??StartEquipmentGuid ?��?�? ?�본 ?�이?�에???�당 GUID�?가�?모든 POC�?찾아????
    // aggregateData?�서 'RawPocs'�??�?�해?�?��?�?그것???�용
    const rowData = dataTableInstance.rows().data().toArray().find(r => r.Guid === guid);
    if (!rowData) return;

    // FailCount > 0 ??것만
    const targetPocs = rowData.RawPocs.filter(p => p.FailCount > 0); 
    currentModalPocs = targetPocs;

    // [Fix 3] Modal Header & Table Structure
    const bodyHtml = `
        <div class="d-flex justify-content-between mb-3 align-items-center">
            <div>
                <h6 class="text-dark fw-bold mb-0">${rowData.Tag} (${rowData.Name})</h6>
                <small class="text-muted">Found ${formatNum(targetPocs.length)} Issues</small>
            </div>
            <button class="btn btn-success btn-sm" id="btn-modal-export">
                <i class="bi bi-file-earmark-excel"></i> Download Excel
            </button>
        </div>
        <div class="table-responsive" style="max-height:500px;overflow:auto;">
            <table class="table table-sm table-bordered modal-table" style="font-size:0.75rem; white-space:nowrap;">
                <thead class="sticky-top">
                    <tr>
                        <th>Check Result</th>
                        <th>Fail Count</th>
                        <th>Issues</th>
                        <th>Feedback</th>
                        <th>Status</th>
                        <th>S: Name</th>
                        <th>S: Sub1</th>
                        <th>S: Sub2</th>
                        <th>S: Tag</th>
                        <th>S: GUID</th>
                        <th>S: Conn GUID</th>
                        <th>S: Conn Name</th>
                        <th>S: Util Group</th>
                        <th>S: Utility</th>
                        <th>T: Tag</th>
                        <th>T: Conn Tag</th>
                        <th>T: Name</th>
                        <th>T: Sub1</th>
                        <th>T: Sub2</th>
                    </tr>
                </thead>
                <tbody>
                    ${targetPocs.map(p => `
                        <tr>
                            <td class="text-center fw-bold ${p.FailCount>0?'text-danger':'text-success'}">${p.FailCount>0 ? 'FAIL' : 'PASS'}</td>
                            <td class="text-center">${p.FailCount}</td>
                            <td class="text-danger fw-bold">${(p.IssueList||[]).join(', ')}</td>
                            <td style="max-width:150px;overflow:hidden;text-overflow:ellipsis;" title="${p.Feedback||''}">${p.feedback||''}</td>
                            <td class="text-center">${p.STATUS||'-'}</td>
                            
                            <td>${p.StartEquipmentName||'-'}</td>
                            <td>${p.SourceSubdivision1||'-'}</td>
                            <td>${p.SourceSubdivision2||'-'}</td>
                            <td>${p.StartEquipmentTag||'-'}</td>
                            <td>${p.StartEquipmentGuid||'-'}</td>
                            <td>${p.StartConnectorGuid||'-'}</td>
                            <td>${p.StartConnectorName||'-'}</td>
                            <td>${p.SourceUtilityGroup||'-'}</td>
                            <td>${p.SourceConnectedUT||'-'}</td>
                            
                            <td>${p.TargetTag||'-'}</td>
                            <td>${p.TargetConnectionTag||'-'}</td>
                            <td>${p.TargetNodeName||'-'}</td>
                            <td>${p.TargetSubdivision1||'-'}</td>
                            <td>${p.TargetSubdivision2||'-'}</td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
        </div>
    `;

    $('#detailModal .modal-body').html(bodyHtml);
    $('#btn-modal-export').off('click').on('click', () => exportModalData(rowData.Tag));
    new bootstrap.Modal(document.getElementById('detailModal')).show();
}

function exportModalData(tagName) {
    if(!currentModalPocs.length) return;
    
    // CSV Header (Requested Order)
    let csv = "\uFEFFCheck Result,Fail Count,Issues,Feedback,Status,S: Name,S: Sub1,S: Sub2,S: Tag,S: GUID,S: Conn GUID,S: Conn Name,S: Util Group,S: Utility,T: Tag,T: Conn Tag,T: Name,T: Sub1,T: Sub2\n"; 
    
    currentModalPocs.forEach(p => {
        const result = p.FailCount > 0 ? 'FAIL' : 'PASS';
        const fb = (p.Feedback||'').replace(/"/g, '""').replace(/\n/g, ' ');
        const issues = (p.IssueList||[]).join('; ');
        
        // CSV Row Construction
        const row = [
            result,
            p.FailCount,
            `"${issues}"`,
            `"${fb}"`,
            p.STATUS||'',
            `"${p.StartEquipmentName||''}"`,
            p.SourceSubdivision1||'',
            p.SourceSubdivision2||'',
            p.StartEquipmentTag||'',
            p.StartEquipmentGuid||'',
            p.StartConnectorGuid||'',
            `"${p.StartConnectorName||''}"`,
            p.SourceUtilityGroup||'',
            p.SourceConnectedUT||'',
            p.TargetTag||'',
            p.TargetConnectionTag||'',
            `"${p.TargetNodeName||''}"`,
            p.TargetSubdivision1||'',
            p.TargetSubdivision2||''
        ];
        
        csv += row.join(",") + "\n";
    });
    
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.setAttribute("download", `Detail_Error_${tagName}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}

// ==========================================
// 7. Similar Model Analysis (Advanced V2)
// ==========================================
function analyzeSimilarModels(targetTag) {
    console.log(`[SimAnalysis] Starting analysis for: ${targetTag}`);
    const allData = dataTableInstance.rows().data().toArray();
    const targetData = allData.find(d => d.Tag === targetTag);
    if (!targetData) {
        console.error("[SimAnalysis] Target data not found.");
        return;
    }

    // 1. [Fix 1] ?�중 ?�턴 매칭 로직 (Tag Prefix + ConstCode Prefix)
    // Tag Prefix 추출 (EQP-101 -> EQP-)
    const tagMatch = targetTag.match(/^(.*?)(\d+)$/);
    const tagPrefix = tagMatch ? tagMatch[1] : targetTag;

    // ConstCode Prefix 추출 (ABC-TYPE-01 -> ABC)
    // ConstCode가 ?�거??'-'가 ?�으�??�체�??�용
    const targetCC = targetData.ConstCode || '';
    const ccPrefix = targetCC.includes('-') ? targetCC.split('-')[0] : targetCC;

    // ?�터�? Tag Prefix ?�치 AND ConstCode Prefix ?�치 AND ?�기 ?�신 ?�외
    const similarGroup = allData.filter(d => {
        if (d.Tag === targetTag) return false; // ?�기 ?�신 ?�외
        
        const dCC = d.ConstCode || '';
        const dCCPrefix = dCC.includes('-') ? dCC.split('-')[0] : dCC;

        return d.Tag.startsWith(tagPrefix) && dCCPrefix === ccPrefix;
    });

    if (similarGroup.length === 0) {
        alert(`${T('lbl_similar_model')}: No matching models found.\n(Tag: ${tagPrefix}*, CC: ${ccPrefix}*)`);
        return;
    }

    // 2. [Fix 4] ?�균�?계산 (기�? ?�비 ?�함? 보통?� 비교�??�사?�비)???�균??�?
    const calcAvg = (arr, key) => {
        const sum = arr.reduce((acc, cur) => acc + (cur[key] || 0), 0);
        return (sum / arr.length);
    };
    
    // 비율 ?�균?� (�?Pass / �?Total)�?계산?�야 ?�확??
    const totalSimPoc = similarGroup.reduce((a,c) => a + c.TotalPoc, 0);
    const totalSimPass = similarGroup.reduce((a,c) => a + c.PassPoc, 0);
    
    const avgStats = {
        Tag: '-',
        ConstCode: '-',
        MainName: '-',
        EqpQty: calcAvg(similarGroup, 'EqpQty'),
        TotalPoc: calcAvg(similarGroup, 'TotalPoc'),
        IssueCount: calcAvg(similarGroup, 'IssueCount'),
        PassRate: totalSimPoc > 0 ? ((totalSimPass/totalSimPoc)*100) : 0
    };

    // 3. Table Construction
    // [Fix 1] Check Diff: Red Text Only (No Background)
    const checkDiff = (val, baseVal) => val !== baseVal ? 'text-danger fw-bold' : '';
    
    let rowsHtml = '';

    // (A) Target Row (기�?)
    const targetRate = targetData.TotalPoc > 0 ? ((targetData.PassPoc/targetData.TotalPoc)*100).toFixed(1) : 0;
    rowsHtml += `
        <tr class="table-primary border-primary fw-bold">
            <td>${T('lbl_target')}</td>
            <td>${targetData.Tag}</td>
            <td>${targetData.ConstCode}</td>
            <td class="text-start">${targetData.MainName}</td>
            <td>${formatNum(targetData.EqpQty)}</td>
            <td>${formatNum(targetData.TotalPoc)}</td>
            <td>${formatNum(targetData.IssueCount)}</td>
            <td>${targetRate}%</td>
        </tr>
    `;

    // (B) Average Row (?�균)
    rowsHtml += `
        <tr class="table-secondary fw-bold" style="border-bottom: 2px solid #aaa;">
            <td>${T('lbl_avg')} (${similarGroup.length})</td>
            <td>-</td>
            <td>-</td>
            <td>-</td>
            <td>${formatNum(avgStats.EqpQty.toFixed(1))}</td>
            <td>${formatNum(avgStats.TotalPoc.toFixed(1))}</td>
            <td>${formatNum(avgStats.IssueCount.toFixed(1))}</td>
            <td>${avgStats.PassRate.toFixed(1)}%</td>
        </tr>
    `;

    // (C) Similar Rows (비교)
    similarGroup.forEach(d => {
        const rate = d.TotalPoc > 0 ? ((d.PassPoc/d.TotalPoc)*100).toFixed(1) : 0;
        
        // [Fix 3] �?불일�???빨간???�시 (기�? ?�비?� 비교)
        const clsName = checkDiff(d.MainName, targetData.MainName);
        const clsQty = checkDiff(d.EqpQty, targetData.EqpQty);
        const clsPoc = checkDiff(d.TotalPoc, targetData.TotalPoc);

        rowsHtml += `
            <tr>
                <td class="text-muted small">${T('lbl_similar')}</td>
                <td class="fw-bold">${d.Tag}</td>
                <td>${d.ConstCode}</td>
                <td class="text-start ${clsName}">${d.MainName}</td>
                <td class="${clsQty}">${formatNum(d.EqpQty)}</td>
                <td class="${clsPoc}">${formatNum(d.TotalPoc)}</td>
                <td class="${d.IssueCount > 0 ? 'text-danger fw-bold' : ''}">${formatNum(d.IssueCount)}</td>
                <td>${rate}%</td>
            </tr>
        `;
    });

    const bodyHtml = `
        <div class="d-flex justify-content-between align-items-center mb-3">
             <div>
                <span class="badge bg-info text-dark me-2">Tag Pattern: ${tagPrefix}*</span>
                <span class="badge bg-secondary">Const Code: ${ccPrefix}*</span>
             </div>
             <button class="btn btn-success btn-sm fw-bold" id="btn-sim-export">
                <i class="bi bi-file-earmark-excel"></i> ${T('btn_excel_sim')}
             </button>
        </div>
        <div class="table-responsive">
            <table class="table table-bordered table-hover text-center align-middle" style="font-size: 0.9rem;">
                <thead class="table-light">
                    <tr>
                        <th width="10%">${T('lbl_type')}</th>
                        <th width="12%">${T('header_eqp')}</th>
                        <th width="12%">${T('header_cc')}</th>
                        <th width="20%">${T('lbl_main_name')}</th>
                        <th width="10%">${T('th_eq_qty')}</th>
                        <th width="10%">${T('header_poc')}</th>
                        <th width="10%">${T('lbl_issue_poc')}</th>
                        <th width="10%">${T('lbl_pass_rate')}</th>
                    </tr>
                </thead>
                <tbody>
                    ${rowsHtml}
                </tbody>
            </table>
        </div>
    `;

    $('#detailModal .modal-title').html(`<i class="bi bi-intersect"></i> ${T('lbl_similar_model')}`);
    $('#detailModal .modal-body').html(bodyHtml);
    
    // [Fix 2] Explicit Event Binding (Safe way)
    // ?�이?��? ?�역 변?�에 ?�??
    window.simExportData = { 
        target: targetData, 
        avg: avgStats, 
        similar: similarGroup,
        prefix: `${tagPrefix}_${ccPrefix}`
    };
    
    // 버튼 ?�벤???�결 (기존 ?�벤???�거 ???�연�?
    $('#btn-sim-export').off('click').on('click', function() {
        console.log("[SimAnalysis] Export button clicked");
        exportSimExcel();
    });

    new bootstrap.Modal(document.getElementById('detailModal')).show();
    
    // [Fix 5] Store data globally for export function
    window.simExportData = { target: targetData, avg: avgStats, similar: similarGroup };
    window.simPrefix = `${tagPrefix}_${ccPrefix}`;
}

// [Fix 2] Export Function with Debugging
function exportSimExcel() {
    console.log("[SimAnalysis] Export function started");
    
    try {
        const data = window.simExportData;
        if (!data) {
            console.error("[SimAnalysis] No export data found in window.simExportData");
            alert("No data to export. Please reopen the modal.");
            return;
        }

        const target = data.target;
        const avg = data.avg;
        const sim = data.similar;
        const prefix = data.prefix || "Analysis";

        console.log(`[SimAnalysis] Exporting ${sim.length + 2} rows...`);

        // CSV Header
        let csv = `\uFEFFType,Tag,ConstCode,MainName,EqpQty,TotalPoc,IssuePoc,PassRate\n`;

        // 1. Target
        const tRate = target.TotalPoc > 0 ? ((target.PassPoc/target.TotalPoc)*100).toFixed(1) : 0;
        csv += `TARGET,${target.Tag},${target.ConstCode},"${target.MainName}",${target.EqpQty},${target.TotalPoc},${target.IssueCount},${tRate}%\n`;

        // 2. Average
        csv += `AVERAGE,-,-,-,${avg.EqpQty.toFixed(1)},${avg.TotalPoc.toFixed(1)},${avg.IssueCount.toFixed(1)},${avg.PassRate.toFixed(1)}%\n`;

        // 3. Similar
        sim.forEach(d => {
            const rate = d.TotalPoc > 0 ? ((d.PassPoc/d.TotalPoc)*100).toFixed(1) : 0;
            csv += `SIMILAR,${d.Tag},${d.ConstCode},"${d.MainName}",${d.EqpQty},${d.TotalPoc},${d.IssueCount},${rate}%\n`;
        });

        const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
        const link = document.createElement("a");
        link.href = URL.createObjectURL(blob);
        link.setAttribute("download", `SimAnalysis_${prefix}.csv`);
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        
        console.log("[SimAnalysis] Export successful.");

    } catch (e) {
        console.error("[SimAnalysis] Export failed:", e);
        alert("Export failed. Check console for details.");
    }
}


function renderBadge(res) {
    return res === 'FAIL' ? '<span class="badge bg-danger">FAIL</span>' : '<span class="badge bg-success">PASS</span>';
}