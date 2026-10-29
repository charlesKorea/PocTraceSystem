'use strict';
window.PocTrace = window.PocTrace || {};
(function (PT) {
  const _ = window._;
  const dayjs = window.dayjs;
  const T = window.T;

PT.initSummaryCharts = function(historyMap) {
        // 1. Comparative Trend (Line) - [수정] AvgPassRate 사용
    const compChart = echarts.init(document.getElementById('compLineChart'));
    PT.state.charts.push(compChart);

    const dbNames = Object.keys(historyMap);
    // 날짜 집합구하기 (X축)
    let allDates = [];
    dbNames.forEach(db => allDates = _.union(allDates, historyMap[db].map(d => d.Date)));
    allDates.sort();

    // [수정] AvgPassRate (POC% + Hookup% 평균) 사용
    const seriesList = dbNames.map(db => ({
        name: PT.getLineName(db),
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
                    { offset: 0, color: s.color || '#0071e3' },
                    { offset: 1, color: (s.color || '#0071e3') + '20' }
                ])
            }
        }))
    });

        // 2. Throughput Chart (Bar) - [수정] POC + H/U No, 색상 변경
    const tpChart = echarts.init(document.getElementById('throughputChart'));
    PT.state.charts.push(tpChart);

    // [수정] POC 수량과 H/U No 수량 함께 표시
    // SUMMARY_DATA에서 Hookup 정보 가져오기
    const lastDayData = dbNames.map(dbName => {
        const summary = SUMMARY_DATA.find(s => s.DbName === dbName);
        const h = historyMap[dbName];
        const last = h[h.length - 1];
        return {
            name: PT.getLineName(dbName),
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
                name: 'POC Pass', type: 'bar', stack: 'poc', color: '#34c759',
                data: lastDayData.map(d => d.pocPass),
                label: { show: true, formatter: (p) => p.value > 0 ? PT.formatNum(p.value) : '', color: '#ffffff', fontWeight: 'bold' }
            },
            // POC Fail (노랑색 텍스트)
            {
                name: 'POC Fail', type: 'bar', stack: 'poc', color: '#e63946',
                data: lastDayData.map(d => d.pocFail),
                label: { show: true, formatter: (p) => p.value > 0 ? PT.formatNum(p.value) : '', color: '#fbbf24', fontWeight: 'bold' }
            },
            // H/U No Pass (흰색 텍스트)
            {
                name: 'H/U No Pass', type: 'bar', stack: 'hookup', color: '#4895ef',
                data: lastDayData.map(d => d.hookupPass),
                label: { show: true, formatter: (p) => p.value > 0 ? PT.formatNum(p.value) : '', color: '#ffffff', fontWeight: 'bold' }
            },
            // H/U No Fail (노랑색 텍스트)
            {
                name: 'H/U No Fail', type: 'bar', stack: 'hookup', color: '#d62828',
                data: lastDayData.map(d => d.hookupFail),
                label: { show: true, formatter: (p) => p.value > 0 ? PT.formatNum(p.value) : '', color: '#fbbf24', fontWeight: 'bold' }
            }
        ]
    });
}

PT.initDetailCharts = function(dto, history) {
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
            PT.state.charts.push(hookupChart);
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
        PT.state.charts.push(hookupChart);
        hookupChart.setOption({
            title: { text: 'No Hookup Data', left: 'center', top: 'center', textStyle: { color: '#999' } },
            series: []
        });
    }

    // ----------------------------
    // C. Trend Chart (Initial: Daily)
    // ----------------------------
    PT.updateTrend('day'); // 초기 ?�더�?
    PT.showErrorTrendForType('ALL', history || []);
    
    /*
    const errorMap = dto.ErrorStats || {};
    const pieData = Object.keys(errorMap).map(k => ({ value: errorMap[k], name: k }));
    
    const pieChart = echarts.init(document.getElementById('pieChart'));
    PT.state.charts.push(pieChart);
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
        if (PT.state.dataTableInstance) {
            PT.state.dataTableInstance.search(params.name).draw();
            if (typeof PT.scrollToNode === 'function') PT.scrollToNode('#mainTable');
        }
    });
  */
            // ----------------------------
    // B. Bar Chart (Process Stats — ProcessStats DTO 직접 사용, 0도 숫자 표기)
    // ----------------------------
    const procMap = dto.ProcessStats || {};
    const num = (v) => {
        const n = Number(v);
        return Number.isFinite(n) ? n : 0;
    };
    const pct = (pass, fail) => {
        const t = pass + fail;
        return t > 0 ? +(100 * pass / t).toFixed(1) : 0;
    };

    const procStats = {};
    Object.keys(procMap).forEach(k => {
        const s = procMap[k] || {};
        let pocPass = num(s.PassPoc);
        let pocFail = num(s.FailPoc);
        let hookupPass = num(s.HookupPass);
        let hookupFail = num(s.HookupFail);
        let eqpCount = num(s.EqpCount);
        if (eqpCount <= 0) eqpCount = num(s.Pass) + num(s.Fail);

        // 하위 호환: PassPoc 미포함 구 data_*.js → Equipments에서 집계
        if (pocPass === 0 && pocFail === 0 && Array.isArray(dto.Equipments)) {
            dto.Equipments.filter(e => (e.Process || '-') === k).forEach(e => {
                pocPass += num(e.PassPoc);
                pocFail += num(e.FailPoc);
            });
        }
        if (hookupPass === 0 && hookupFail === 0) {
            const hookupTotal = num(dto.HookupStats && dto.HookupStats.TotalObjects);
            const hookupOK = num(dto.HookupStats && dto.HookupStats.OK);
            const rate = hookupTotal > 0 ? hookupOK / hookupTotal : 0;
            const totalPoc = pocPass + pocFail;
            hookupPass = Math.round(totalPoc * rate);
            hookupFail = Math.max(0, totalPoc - hookupPass);
        }

        procStats[k] = {
            pocPass, pocFail, hookupPass, hookupFail, eqpCount,
            pocPassPct: pct(pocPass, pocFail),
            hookupPassPct: pct(hookupPass, hookupFail)
        };
    });

    // Equipments에만 있고 ProcessStats에 없는 공정 보정
    if (Array.isArray(dto.Equipments)) {
        dto.Equipments.forEach(e => {
            const k = e.Process || '-';
            if (procStats[k]) return;
            procStats[k] = {
                pocPass: num(e.PassPoc), pocFail: num(e.FailPoc),
                hookupPass: 0, hookupFail: 0, eqpCount: 1,
                pocPassPct: 0, hookupPassPct: 0
            };
            procStats[k].pocPassPct = pct(procStats[k].pocPass, procStats[k].pocFail);
        });
    }

    const categories = Object.keys(procStats).sort((a, b) => {
        const d1 = procStats[a], d2 = procStats[b];
        const fr1 = (() => {
            const t = d1.pocPass + d1.pocFail + d1.hookupPass + d1.hookupFail;
            return t > 0 ? (d1.pocFail + d1.hookupFail) / t : 0;
        })();
        const fr2 = (() => {
            const t = d2.pocPass + d2.pocFail + d2.hookupPass + d2.hookupFail;
            return t > 0 ? (d2.pocFail + d2.hookupFail) / t : 0;
        })();
        return fr2 - fr1;
    });

    const categoryLabels = categories.map(k => {
        const n = procStats[k] ? procStats[k].eqpCount : 0;
        return k + ' (n=' + n + ')';
    });

    const barChart = echarts.init(document.getElementById('barChart'));
    PT.state.charts.push(barChart);
    barChart.setOption({
        tooltip: {
            trigger: 'axis', axisPointer: { type: 'shadow' },
            formatter: function(params) {
                const idx = params[0].dataIndex;
                const key = categories[idx];
                const d = procStats[key] || {};
                let html = `<strong>${params[0].name}</strong><br/>`;
                html += `검수 수량: <b>${PT.formatNum(d.eqpCount || 0)}</b><br/>`;
                params.forEach(p => {
                    if (String(p.seriesName).indexOf('%') >= 0) {
                        html += `${p.marker} ${p.seriesName}: <b>${p.value}%</b><br/>`;
                    } else {
                        html += `${p.marker} ${p.seriesName}: <b>${PT.formatNum(p.value)}</b><br/>`;
                    }
                });
                return html;
            }
        },
        legend: { bottom: 0, type: 'scroll' },
        grid: { left: '3%', right: '4%', bottom: '18%', top: '12%', containLabel: true },
        xAxis: {
            type: 'category',
            data: categoryLabels,
            axisLabel: { rotate: 30, interval: 0, fontSize: 10 }
        },
        yAxis: [
            { type: 'value', name: 'Count' },
            { type: 'value', name: '%', min: 0, max: 100, splitLine: { show: false } }
        ],
        series: [
            {
                name: 'POC Pass', type: 'bar', stack: 'poc', color: '#34c759',
                data: categories.map(k => procStats[k].pocPass),
                label: {
                    show: true, position: 'inside',
                    formatter: (p) => PT.formatNum(p.value),
                    color: '#ffffff', fontWeight: 'bold', fontSize: 10
                }
            },
            {
                name: 'POC Fail', type: 'bar', stack: 'poc', color: '#e63946',
                data: categories.map(k => procStats[k].pocFail),
                label: {
                    show: true, position: 'inside',
                    formatter: (p) => PT.formatNum(p.value),
                    color: '#ffffff', fontWeight: 'bold', fontSize: 10
                }
            },
            {
                name: 'H/U No Pass', type: 'bar', stack: 'hookup', color: '#4895ef',
                data: categories.map(k => procStats[k].hookupPass),
                label: {
                    show: true, position: 'inside',
                    formatter: (p) => PT.formatNum(p.value),
                    color: '#ffffff', fontWeight: 'bold', fontSize: 10
                }
            },
            {
                name: 'H/U No Fail', type: 'bar', stack: 'hookup', color: '#d62828',
                data: categories.map(k => procStats[k].hookupFail),
                label: {
                    show: true, position: 'inside',
                    formatter: (p) => PT.formatNum(p.value),
                    color: '#ffffff', fontWeight: 'bold', fontSize: 10
                }
            },
            {
                name: '검수 수량', type: 'bar', color: '#6c757d',
                data: categories.map(k => procStats[k].eqpCount),
                label: {
                    show: true, position: 'top',
                    formatter: (p) => PT.formatNum(p.value),
                    color: '#343a40', fontWeight: 'bold', fontSize: 10
                }
            },
            {
                name: 'POC Pass %', type: 'line', yAxisIndex: 1, color: '#1d7874',
                data: categories.map(k => procStats[k].pocPassPct),
                label: { show: true, formatter: '{c}%', color: '#1d7874', position: 'top', fontSize: 10 }
            },
            {
                name: 'H/U Pass %', type: 'line', yAxisIndex: 1, color: '#3a86ff',
                data: categories.map(k => procStats[k].hookupPassPct),
                label: { show: true, formatter: '{c}%', color: '#3a86ff', position: 'top', fontSize: 10 }
            }
        ]
    });
    barChart.on('click', (params) => {
        const key = categories[params.dataIndex] || params.name;
        const searchKey = String(key).replace(/\s*\(n=\d+\)\s*$/, '');
        if (PT.state.dataTableInstance) {
            PT.state.dataTableInstance.search(searchKey).draw();
            if (typeof PT.scrollToNode === 'function') PT.scrollToNode('#mainTable');
        }
    });
  /*
    // ----------------------------
    // C. Line Chart (Trend - [Fix 2] Issue Count Added)
    // ----------------------------
    const lineChart = echarts.init(document.getElementById('lineChart'));
    PT.state.charts.push(lineChart);
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
                name: T('card_poc_pass_rate'), type: 'line', color: '#0071e3',
                data: history.map(d => d.RatePoc),
                label: { show: true, formatter: '{c}%' } // Label
            },
            {
                name: T('lbl_issue_poc'), type: 'line', yAxisIndex: 1, color: '#f72585',
                data: history.map(d => d.IssuePocCount || 0),
                label: { show: true, formatter: (p) => PT.formatNum(p.value) }, // Label + Comma
                lineStyle: { type: 'dashed' }
            }
        ]
    });
  */
}

// Pie Chart Helper (Reusable)
PT.initPieChart = function(domId, dataMap, emptyLabel) {
    const map = dataMap || {};
    const data = Object.keys(map).map(k => ({ value: map[k], name: k }));
    const chart = echarts.init(document.getElementById(domId));
    PT.state.charts.push(chart);
    chart.setOption({
        tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
        legend: { top: '5%', left: 'center', type: 'scroll' },
        series: [{
            type: 'pie', radius: ['40%', '65%'], center: ['50%', '60%'],
            data: data.length ? data : [{value:0, name:emptyLabel}],
            label: { show: true, formatter: '{b}\n{c} ({d}%)', fontSize: 10 }
        }]
    });

    // Drill-down: Error pie filters grid via action module
    chart.off('click');
    chart.on('click', function (params) {
        if (!params || !params.name) return;
        if (domId === 'pieChartError') {
            if (typeof PT.filterGridByIssue === 'function') PT.filterGridByIssue(params.name);
            if (typeof PT.showErrorTrendForType === 'function') {
                PT.showErrorTrendForType(params.name, window.currentHistory || []);
            }
            return;
        }
        if (PT.state.dataTableInstance) {
            PT.state.dataTableInstance.search(params.name).draw();
            if (typeof PT.scrollToNode === 'function') PT.scrollToNode('#mainTable');
        }
    });
}

// [Change 3] Trend Chart Update Logic
PT.updateTrend = function(mode) {
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
        processedData = PT.groupByTime(yearData, 'week');
    } else if (mode === 'month') {
        processedData = PT.groupByTime(yearData, 'month');
    }

    // 3. Render Chart
    PT.renderTrendChart(processedData);
};

// Helper: Group By Week/Month
PT.groupByTime = function(data, unit) {
    const toWeekKey = function (dateStr) {
        const d = new Date(dateStr + 'T00:00:00');
        if (!Number.isFinite(d.getTime())) return '';
        const day = d.getDay(); // 0:Sun ~ 6:Sat
        const diffToMonday = (day + 6) % 7;
        const monday = new Date(d);
        monday.setDate(d.getDate() - diffToMonday);
        const yearStart = new Date(monday.getFullYear(), 0, 1);
        const yearStartDay = yearStart.getDay();
        const yearStartDiff = (yearStartDay + 6) % 7;
        const firstMonday = new Date(yearStart);
        firstMonday.setDate(yearStart.getDate() - yearStartDiff);
        const weekNo = Math.floor((monday - firstMonday) / 604800000) + 1;
        return monday.getFullYear() + '-W' + String(weekNo).padStart(2, '0');
    };

    const grouped = _.groupBy(data, function (d) {
        if (unit === 'week') return toWeekKey(d.Date);
        const dt = new Date(String(d.Date) + 'T00:00:00');
        if (!Number.isFinite(dt.getTime())) return '';
        return dt.getFullYear() + '-' + String(dt.getMonth() + 1).padStart(2, '0');
    });

    return Object.keys(grouped).filter(function (k) { return !!k; }).sort().map(key => {
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
            RatePoc: Number(rate),
            AvgPassRate: parseFloat(avgPassRate.toFixed(1)),
            IssuePocCount: issueCnt,
            HookupErrorCount: hookupErrorCnt
        };
    });
}

PT.showErrorTrendForType = function(issueType, history) {
    const dom = document.getElementById('errorTrendChart');
    const table = document.getElementById('errorTrendTable');
    const hint = document.getElementById('errorTrendHint');
    if (!dom || !table) return;

    const rows = (history || []).slice().sort(function (a, b) {
        return String(a.Date || '').localeCompare(String(b.Date || ''));
    });
    const key = issueType || 'ALL';
    const hasTypedSeries = rows.some(function (r) {
        return r && r.ErrorTypeStats && typeof r.ErrorTypeStats === 'object';
    });
    const values = rows.map(function (r) {
        if (key === 'ALL') return Number(r.IssuePocCount) || 0;
        if (r && r.ErrorTypeStats && r.ErrorTypeStats[key] != null) {
            return Number(r.ErrorTypeStats[key]) || 0;
        }
        return 0;
    });

    if (hint) {
        if (key !== 'ALL' && !hasTypedSeries) hint.textContent = T('chart_error_trend_fallback');
        else hint.textContent = T('chart_error_trend_hint');
    }

    if (PT.state.errorTrendChart) {
        try { PT.state.errorTrendChart.dispose(); } catch (e) { /* ignore */ }
    }
    const chart = echarts.init(dom);
    PT.state.errorTrendChart = chart;
    PT.state.charts.push(chart);
    chart.setOption({
        tooltip: { trigger: 'axis' },
        grid: { left: '3%', right: '4%', bottom: '10%', top: '8%', containLabel: true },
        xAxis: { type: 'category', data: rows.map(function (r) { return r.Date; }) },
        yAxis: { type: 'value', name: 'Count' },
        series: [{
            name: key === 'ALL' ? T('lbl_issue_poc') : key,
            type: 'line',
            smooth: true,
            symbol: 'circle',
            symbolSize: 7,
            color: '#f72585',
            data: values,
            areaStyle: { opacity: 0.08 },
            label: { show: true, formatter: function (p) { return PT.formatNum(p.value); } }
        }]
    });

    const latest = values.length ? values[values.length - 1] : 0;
    const prev = values.length > 1 ? values[values.length - 2] : null;
    const delta = prev == null ? null : (latest - prev);
    const trendCls = delta == null ? 'text-muted' : (delta < 0 ? 'text-success' : (delta > 0 ? 'text-danger' : 'text-muted'));
    const trendText = delta == null
        ? '—'
        : ((delta > 0 ? '+' : '') + PT.formatNum(delta));

    const title = key === 'ALL' ? T('chart_error_trend_all') : key;
    table.innerHTML =
        '<thead class="table-light"><tr>' +
        '<th>' + T('chart_error_trend_case') + '</th>' +
        '<th class="text-end">' + T('chart_error_trend_prev') + '</th>' +
        '<th class="text-end">' + T('chart_error_trend_today') + '</th>' +
        '<th class="text-end">' + T('chart_error_trend_delta') + '</th>' +
        '</tr></thead>' +
        '<tbody><tr>' +
        '<td class="fw-semibold">' + $('<div>').text(title).html() + '</td>' +
        '<td class="text-end">' + (prev == null ? '—' : PT.formatNum(prev)) + '</td>' +
        '<td class="text-end">' + PT.formatNum(latest) + '</td>' +
        '<td class="text-end ' + trendCls + ' fw-semibold">' + trendText + '</td>' +
        '</tr></tbody>';
}

PT.renderTrendChart = function(data) {
    const dom = document.getElementById('lineChart');
    if (PT.state.lineChartInstance) PT.state.lineChartInstance.dispose();
    PT.state.lineChartInstance = echarts.init(dom);
    PT.state.charts.push(PT.state.lineChartInstance);

    // [수정] Hookup 데이터도 함께 표시 (RatePoc + AvgPassRate + IssuePocCount)
    // AvgPassRate가 있으면 사용, 없으면 RatePoc
        PT.state.lineChartInstance.setOption({
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
                smooth: true, color: '#0071e3', symbol: 'circle', symbolSize: 8,
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

  window.updateTrend = PT.updateTrend;
  window.initSummaryCharts = PT.initSummaryCharts;
  window.initDetailCharts = PT.initDetailCharts;
  window.initPieChart = PT.initPieChart;
  window.groupByTime = PT.groupByTime;
  window.renderTrendChart = PT.renderTrendChart;
})(window.PocTrace);