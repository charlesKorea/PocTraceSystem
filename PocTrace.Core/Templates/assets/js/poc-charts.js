'use strict';
window.PocTrace = window.PocTrace || {};
(function (PT) {
  const _ = window._;
  const dayjs = window.dayjs;
  const T = window.T;

PT.isDark = function () {
  return document.documentElement.getAttribute('data-theme') === 'dark';
};

const toNum = function (v) {
  if (v == null || v === '') return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
};

const clampPct = function (v) {
  return v == null ? null : Math.max(0, Math.min(100, v));
};

/** 일별 History 한 행에서 POC / H/U / Quality Point(%) 추출. 없으면 null. */
PT.historyRates = function (row) {
  const poc = clampPct(toNum(row && row.RatePoc));
  let qp = clampPct(toNum(row && row.AvgPassRate));
  if (qp == null) qp = poc;

  let hu = null;
  const huSaved = toNum(row && row.HookupPassRate);
  if (huSaved != null && toNum(row && row.HookupTotal) !== 0) {
    hu = clampPct(huSaved);
  } else if (toNum(row && row.HookupTotal) === 0) {
    hu = null;
  } else if (poc != null && qp != null) {
    // 구버전 History: Quality Point = (POC + H/U) / 2 에서 역산
    hu = clampPct(qp * 2 - poc);
  }
  return { poc: poc, hu: hu, qp: qp };
};

/** ISO 8601 주차 키 (예: 2026-W45). dayjs 주차 플러그인에 의존하지 않는다. */
PT.weekKey = function (dateStr) {
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(dateStr || ''));
  if (!m) return '';
  const t = new Date(Date.UTC(+m[1], +m[2] - 1, +m[3]));
  const day = t.getUTCDay() || 7;
  t.setUTCDate(t.getUTCDate() + 4 - day);
  const yearStart = Date.UTC(t.getUTCFullYear(), 0, 1);
  const week = Math.ceil(((t.getTime() - yearStart) / 86400000 + 1) / 7);
  return t.getUTCFullYear() + '-W' + String(week).padStart(2, '0');
};

PT.monthKey = function (dateStr) {
  const m = /^(\d{4})-(\d{2})/.exec(String(dateStr || ''));
  return m ? m[1] + '-' + m[2] : '';
};

/**
 * 일별 History → day / week / month 단위. 구간 안의 일별 비율은 평균내고 정수(%)로 반올림한다.
 * 반환: [{ Date, poc, hu, qp }]  (값 없으면 null)
 */
PT.aggregateHistory = function (rows, mode) {
  const sorted = (rows || []).filter(function (r) { return r && r.Date; })
    .slice().sort(function (a, b) { return String(a.Date).localeCompare(String(b.Date)); });
  const keyOf = mode === 'week' ? PT.weekKey : (mode === 'month' ? PT.monthKey : function (d) { return String(d).slice(0, 10); });
  const order = [];
  const groups = {};
  sorted.forEach(function (r) {
    const k = keyOf(r.Date);
    if (!k) return;
    if (!groups[k]) { groups[k] = []; order.push(k); }
    groups[k].push(PT.historyRates(r));
  });
  const avg = function (list, field) {
    const vals = list.map(function (x) { return x[field]; }).filter(function (v) { return v != null; });
    if (!vals.length) return null;
    return Math.round(vals.reduce(function (a, b) { return a + b; }, 0) / vals.length);
  };
  return order.map(function (k) {
    return { Date: k, poc: avg(groups[k], 'poc'), hu: avg(groups[k], 'hu'), qp: avg(groups[k], 'qp') };
  });
};

PT.trendPctAxis = function () {
  return {
    type: 'value', max: 100,
    min: function (v) { return Math.max(0, Math.floor((v.min - 5) / 5) * 5); },
    axisLabel: { formatter: '{value}%' }
  };
};

PT.updateSummaryTrend = function (mode) {
  mode = mode || 'day';
  $('#comp-trend-buttons .btn').removeClass('active');
  $('#comp-trend-buttons .btn[data-mode="' + mode + '"]').addClass('active');

  const historyMap = PT.state.summaryHistory || {};
  const dom = document.getElementById('compLineChart');
  if (!dom) return;
  if (PT.state.compChart) { try { PT.state.compChart.dispose(); } catch (e) { /* ignore */ } }
  const compChart = echarts.init(dom);
  PT.state.compChart = compChart;
  PT.state.charts.push(compChart);

  const dbNames = Object.keys(historyMap);
  const perLine = {};
  let allDates = [];
  dbNames.forEach(function (db) {
    perLine[db] = PT.aggregateHistory(historyMap[db], mode);
    allDates = _.union(allDates, perLine[db].map(function (d) { return d.Date; }));
  });
  allDates.sort();

  const seriesList = dbNames.map(function (db) {
    const byDate = {};
    perLine[db].forEach(function (d) { byDate[d.Date] = d.qp; });
    return {
      name: PT.getLineName(db),
      type: 'line', smooth: true, symbolSize: 8,
      connectNulls: true,
      data: allDates.map(function (date) { return byDate[date] != null ? byDate[date] : null; }),
      label: { show: true, position: 'top', formatter: '{c}%' },
      labelLayout: { hideOverlap: true }
    };
  });

  compChart.setOption({
    tooltip: {
      trigger: 'axis',
      valueFormatter: function (v) { return v == null ? '-' : v + '%'; }
    },
    legend: { bottom: 0 },
    grid: { left: '3%', right: '4%', bottom: '10%', top: '10%', containLabel: true },
    xAxis: { type: 'category', data: allDates, boundaryGap: false },
    yAxis: PT.trendPctAxis(),
    series: seriesList.map(function (s) {
      return Object.assign({}, s, {
        areaStyle: {
          color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
            { offset: 0, color: s.color || '#0071e3' },
            { offset: 1, color: (s.color || '#0071e3') + '20' }
          ])
        }
      });
    })
  });
};

PT.initSummaryCharts = function(historyMap) {
    PT.state.summaryHistory = historyMap || {};
    PT.updateSummaryTrend('day');
    const dbNames = Object.keys(historyMap || {});

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

// 파이 차트: 상단 범례와 도넛/바깥 라벨이 겹치지 않도록 범례는 맨 위, 도넛은 아래로 내리고 작게 둔다.
PT.PIE_RADIUS = ['30%', '50%'];
PT.PIE_CENTER = ['50%', '60%'];
PT.pieLegend = function () {
  return { top: 0, left: 'center', type: 'scroll', itemWidth: 12, itemHeight: 10, textStyle: { fontSize: 11 } };
};

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
                legend: PT.pieLegend(),
                series: [{
                    type: 'pie', radius: PT.PIE_RADIUS, center: PT.PIE_CENTER, avoidLabelOverlap: true,
                    data: hookupData,
                    label: { show: true, formatter: '{b}\n{c} ({d}%)', fontSize: 10 },
                    labelLine: { length: 8, length2: 8 },
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
    // 공정별 검수 현황: POC 기준 검수 설비 총 수량 / Fail 설비 수량 / Fail Rate
    const dark = PT.isDark();
    const procStats = {};
    (dto.Equipments || []).forEach(function (e) {
        const k = e.Process || '-';
        const a = procStats[k] || (procStats[k] = { total: 0, fail: 0 });
        a.total++;
        if (e.Result === 'FAIL') a.fail++;
    });
    const failRateOf = function (k) {
        const d = procStats[k];
        return d && d.total > 0 ? d.fail / d.total * 100 : 0;
    };
    const categories = Object.keys(procStats).sort(function (a, b) {
        return (failRateOf(b) - failRateOf(a)) || (procStats[b].total - procStats[a].total);
    });

    const nameTotal = T('proc_eqp_total');
    const nameFail = T('proc_eqp_fail');
    const nameRate = T('proc_fail_rate');
    const rateColor = dark ? '#ffb703' : '#c1121f';
    const countLabelColor = dark ? '#e5e5ea' : '#343a40';

    const barChart = echarts.init(document.getElementById('barChart'));
    PT.state.charts.push(barChart);
    barChart.setOption({
        tooltip: {
            trigger: 'axis', axisPointer: { type: 'shadow' },
            formatter: function(params) {
                const key = categories[params[0].dataIndex];
                const d = procStats[key] || { total: 0, fail: 0 };
                return '<strong>' + key + '</strong><br/>' +
                    nameTotal + ': <b>' + PT.formatNum(d.total) + '</b><br/>' +
                    nameFail + ': <b>' + PT.formatNum(d.fail) + '</b><br/>' +
                    nameRate + ': <b>' + Math.round(failRateOf(key)) + '%</b>';
            }
        },
        legend: { bottom: 0 },
        grid: { left: '3%', right: '4%', bottom: '14%', top: '12%', containLabel: true },
        xAxis: { type: 'category', data: categories, axisLabel: { interval: 0, rotate: categories.length > 6 ? 30 : 0 } },
        yAxis: [
            { type: 'value', name: 'Count', minInterval: 1 },
            { type: 'value', name: '%', min: 0, max: 100, splitLine: { show: false }, axisLabel: { formatter: '{value}%' } }
        ],
        series: [
            {
                name: nameTotal, type: 'bar', color: dark ? '#5b8def' : '#4895ef', barMaxWidth: 36,
                data: categories.map(function (k) { return procStats[k].total; }),
                label: { show: true, position: 'top', color: countLabelColor, fontWeight: 'bold', fontSize: 11, formatter: function (p) { return PT.formatNum(p.value); } }
            },
            {
                name: nameFail, type: 'bar', color: '#e63946', barMaxWidth: 36,
                data: categories.map(function (k) { return procStats[k].fail; }),
                label: { show: true, position: 'top', color: countLabelColor, fontWeight: 'bold', fontSize: 11, formatter: function (p) { return PT.formatNum(p.value); } }
            },
            {
                name: nameRate, type: 'line', yAxisIndex: 1, color: rateColor,
                symbol: 'circle', symbolSize: 9, z: 10,
                lineStyle: { width: 2, type: 'dashed' },
                itemStyle: { color: rateColor, borderColor: dark ? '#1c1c1e' : '#ffffff', borderWidth: 2 },
                data: categories.map(function (k) { return Math.round(failRateOf(k)); }),
                label: {
                    show: true, position: 'top', offset: [0, -6], formatter: '{c}%', fontWeight: 'bold', fontSize: 12,
                    color: dark ? '#1c1c1e' : rateColor,
                    backgroundColor: dark ? '#ffb703' : 'rgba(193,18,31,0.08)',
                    borderRadius: 4, padding: [2, 6]
                }
            }
        ]
    });
    barChart.on('click', (params) => {
        const searchKey = categories[params.dataIndex] || params.name;
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
        legend: PT.pieLegend(),
        series: [{
            type: 'pie', radius: PT.PIE_RADIUS, center: PT.PIE_CENTER, avoidLabelOverlap: true,
            data: data.length ? data : [{value:0, name:emptyLabel}],
            label: { show: true, formatter: '{b}\n{c} ({d}%)', fontSize: 10 },
            labelLine: { length: 8, length2: 8 }
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

// Trend Chart Update Logic (Daily / Weekly / Monthly)
PT.updateTrend = function(mode) {
    mode = (mode === 'week' || mode === 'month') ? mode : 'day';
    $('#trend-buttons .btn').removeClass('active');
    $(`#trend-buttons .btn[data-mode="${mode}"]`).addClass('active');

    const rawHistory = window.currentHistory || [];
    const currentYear = new Date().getFullYear().toString();
    const yearData = rawHistory.filter(d => String(d.Date || '').startsWith(currentYear));
    const source = yearData.length ? yearData : rawHistory;

    PT.renderTrendChart(PT.aggregateHistory(source, mode));
};

PT.groupByTime = function(data, unit) {
    return PT.aggregateHistory(data, unit);
};

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

    const fmt = function (v) { return v == null ? '-' : v + '%'; };
    PT.state.lineChartInstance.setOption({
        tooltip: { trigger: 'axis', valueFormatter: fmt },
        legend: { bottom: 0 },
        grid: { left: '3%', right: '4%', bottom: '15%', top: '10%', containLabel: true },
        xAxis: { type: 'category', data: data.map(d => d.Date) },
        yAxis: PT.trendPctAxis(),
        series: [
            {
                name: 'POC Pass(%)', type: 'line', data: data.map(d => d.poc),
                smooth: true, connectNulls: true, color: '#0071e3', symbol: 'circle', symbolSize: 7,
                label: { show: true, formatter: '{c}%', position: 'top' },
                labelLayout: { hideOverlap: true }
            },
            {
                name: 'H/U Pass(%)', type: 'line', data: data.map(d => d.hu),
                smooth: true, connectNulls: true, color: '#2a9d8f', symbol: 'diamond', symbolSize: 7,
                label: { show: true, formatter: '{c}%', position: 'bottom' },
                labelLayout: { hideOverlap: true }
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