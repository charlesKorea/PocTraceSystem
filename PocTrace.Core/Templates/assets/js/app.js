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

$(document).ready(function () {
    console.log("PocTrace App V2.3 Started.");
    // 1. Language Init
    const savedLang = (window.PocTracePrefs && typeof PocTracePrefs.getLang === 'function')
        ? PocTracePrefs.getLang()
        : 'ko';
    $('#lang-selector').val(savedLang);
    // lang.js가 window에 등록한 함수 사용
    if (typeof window.applyLanguage === 'function') {
        window.applyLanguage();
    }

    const themeDark = document.documentElement.getAttribute('data-theme') === 'dark';
    $('#theme-toggle').attr('aria-pressed', themeDark ? 'true' : 'false');
    $('#theme-toggle').on('click', function () {
        const next = document.documentElement.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        if (window.PocTracePrefs) {
            PocTracePrefs.setTheme(next);
            PocTracePrefs.commit();
            return;
        }
        try { localStorage.setItem('poc_theme', next); } catch (e) {}
        location.reload();
    });

    $('#lang-selector').on('change', function() {
        const next = $(this).val();
        if (window.PocTracePrefs) {
            PocTracePrefs.setLang(next);
            PocTracePrefs.commit();
            return;
        }
        try { localStorage.setItem('poc_lang', next); } catch (e) {}
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
    if (window.PocTracePrefs && !PocTracePrefs.storageOk && typeof PocTracePrefs.href === 'function') {
        $('a[href]').each(function () {
            const href = $(this).attr('href');
            const next = PocTracePrefs.href(href);
            if (next && next !== href) $(this).attr('href', next);
        });
    }
    if (typeof PT.mountCommandBar === 'function') PT.mountCommandBar();
    applyNavAlias();

    window.addEventListener('resize', () => PT.state.charts.forEach(c => c && !c.isDisposed() && c.resize()));

    $(document).on('click', '#trend-buttons .btn', function() {
        window.updateTrend($(this).data('mode'));
    });
    $(document).on('click', '#comp-trend-buttons .btn', function() {
        PT.updateSummaryTrend($(this).data('mode'));
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
                    <div class="d-flex justify-content-between align-items-start mb-2 flex-wrap gap-2">
                        <h5 class="fw-bold text-dark mb-0"><i class="bi bi-graph-up-arrow"></i> ${T('chart_trend_comparative')}</h5>
                        <div class="btn-group" role="group" id="comp-trend-buttons">
                            <button type="button" class="btn btn-outline-secondary btn-sm active" data-mode="day">Daily</button>
                            <button type="button" class="btn btn-outline-secondary btn-sm" data-mode="week">Weekly</button>
                            <button type="button" class="btn btn-outline-secondary btn-sm" data-mode="month">Monthly</button>
                        </div>
                    </div>
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
