/* DIQS Core: Module 3 - Chart & Analysis (Label Fix) */

// --- [1. KPI] ---
function renderKPI(kpi, historyData, selector) {
    var el = document.querySelector(selector || "#kpi-gauge"); 
    if(!el) return;

    // 1. 텍스트 바인딩 (대시보드 호환성을 위해 ID 체크)
    var errCntEl = document.getElementById('kpi-error-count');
    if(errCntEl) errCntEl.innerText = (kpi.ErrorCount || 0).toLocaleString();

    var dod = kpi.DoD || 0;
    var diffCount = kpi.DiffCount || 0;
    // History 기반 재계산 (옵션)
    if (diffCount === 0 && historyData && historyData.length > 1) {
        var last = historyData[historyData.length - 1];
        var prev = historyData[historyData.length - 2];
        diffCount = (last.Error || 0) - (prev.Error || 0);
    }

    var icon = dod > 0 ? "▲" : (dod < 0 ? "▼" : "-");
    var color = dod > 0 ? "text-success" : (dod < 0 ? "text-danger" : "text-muted"); // 점수는 오르면 좋음(Green)
    var countSign = diffCount > 0 ? "+" : "";
    var countColor = diffCount > 0 ? "text-danger" : (diffCount < 0 ? "text-success" : "text-muted"); // 오류는 늘면 나쁨(Red)

    var dodEl = document.getElementById('kpi-dod-val');
    if(dodEl) dodEl.innerHTML = `<span class="${color}">${icon} ${Math.abs(dod).toFixed(2)}%</span> <small class="${countColor}">(${countSign}${diffCount}${T('ui_unit_count')||''})</small>`;

    var target = kpi.TargetScore || 98.0;
    var score = kpi.Score;
    var gap = (target - score).toFixed(1);
    
    $('#kpi-current-val').text(score + "%");
    $('#kpi-target-val').text(target + "%");
    
    var gapEl = document.getElementById('kpi-target-gap');
    if(gapEl) {
        var gapText = gap > 0 ? `(${T('kpi_target')||'Goal'} -${gap}%)` : `(Goal Reached!)`;
        gapEl.innerText = gapText;
        gapEl.className = gap > 0 ? "fw-bold text-danger fs-6" : "fw-bold text-success fs-6";
    }

    var pct = Math.min(100, (score / target) * 100).toFixed(1);
    $('#kpi-progress-bar').css('width', pct + '%');
    
    var chartColor = score >= 95 ? '#00E396' : (score >= 85 ? '#FEB019' : '#FF4560');
    var options = {
        series: [score],
        chart: { type: 'radialBar', height: 280, fontFamily: 'Pretendard, sans-serif', toolbar: { show: true } },
        colors: [chartColor], fill: { type: 'solid' },
        plotOptions: { radialBar: { hollow: { size: '65%' }, dataLabels: { value: { fontSize: '30px', fontWeight: 'bold', formatter: v => v + '%' } } } },
        labels: [T('trend_rate') || 'Score']
    };
    if (kpiChart) kpiChart.destroy();
    kpiChart = new ApexCharts(el, options);
    kpiChart.render();
}

// --- [2. Trend] ---
function renderTrend(days, historyData, selector) {
    var el = document.querySelector(selector || "#trend-chart");
    if (!historyData || !Array.isArray(historyData) || !el) return;
    
    var data = historyData.slice(Math.max(historyData.length - days, 0));
    var targetScore = 98.0;

    // 샘플링 로직
    var step = Math.ceil(data.length / 12);
    var sampled = [];
    for (var i = 0; i < data.length; i += step) sampled.push(data[i]);
    if (sampled.length > 0 && sampled[sampled.length - 1] !== data[data.length - 1]) sampled.push(data[data.length - 1]);

    var dates = sampled.map(d => d.Date);
    var rates = sampled.map(d => d.Rate);
    var errors = sampled.map(d => d.Error || 0);

    if (trendChart) trendChart.destroy();

    var options = {
        series: [
            { name: T('trend_rate')||"Rate", type: 'area', data: rates },
            { name: T('trend_error')||"Errors", type: 'column', data: errors }
        ],
        chart: {
            type: 'line', height: 250, toolbar: { show: true },
            events: {
                // [대시보드 호환] 클릭 시 브리핑 표시
                click: function(e, c, cfg) {
                    if (cfg.dataPointIndex >= 0) {
                        var idx = cfg.dataPointIndex;
                        $('#brief-date').text(dates[idx]);
                        $('#brief-rate').text(rates[idx] + "%");
                        $('#brief-error').text(errors[idx].toLocaleString());
                        $('#daily-briefing').hide().fadeIn();
                    }
                }
            }
        },
        stroke: { width: [3, 0], curve: 'smooth' },
        xaxis: { categories: dates, type: 'datetime' },
        yaxis: [ { title: { text: 'Rate' }, min: 0, max: 100 }, { opposite: true, title: { text: 'Errors' } } ],
        colors: ['#008FFB', '#FF4560'], fill: { type: 'solid', opacity: [0.2, 1] }, plotOptions: { bar: { columnWidth: '40%' } },
        annotations: { yaxis: [{ y: targetScore, borderColor: '#775DD0', borderWidth: 3, label: { style: { color: '#fff', background: '#775DD0' }, text: T('trend_goal')||'Goal' } }] },
        tooltip: { x: { format: 'yyyy-MM-dd' } }
    };
    trendChart = new ApexCharts(el, options);
    trendChart.render();
}

// --- [3. Analysis] ---
function initAnalysisControls() { /* 기존 동일 - 생략 */ 
    var data = window.GLOBAL_DATA || []; if(data.length === 0) return;
    var keys = Object.keys(data[0]);
    var exclude = ['_SysId', '_Lineage', '_UpdateDate', '_ReportTitle'];
    keys.forEach(k => { if(!exclude.includes(k)) { var l = k.startsWith('_')?k.substring(1):k; $('#anlGroup').append(new Option(l, k)); } });
    if(keys.includes('_ErrorType')) $('#anlGroup').val('_ErrorType');
    var projs = [...new Set(data.map(d => d._ProjectId))].sort();
    projs.forEach(p => $('#anlProject').append(new Option(p, p)));
}

function resetAnalysisOptions() { 
    $('#anlProject').val(''); 
    $('#chkEmpty').prop('checked', false); 
    $('#chkShowLabel').prop('checked', true); 
    renderAnalysis(); 
}

function renderAnalysis(filteredData) {
    var targetData = filteredData || window.GLOBAL_DATA || [];
    var groupBy = $('#anlGroup').val();
    var chartType = $('#anlType').val();
    var targetProj = $('#anlProject').val();
    var excludeEmpty = $('#chkEmpty').is(':checked');
    var showLabel = $('#chkShowLabel').is(':checked'); // [Check] 체크 상태 읽기
    
    if (targetProj && !filteredData) targetData = targetData.filter(d => d._ProjectId === targetProj);

    var counts = {}; var validCount = 0;
    targetData.forEach(item => {
        var val = item[groupBy];
        if (val === null || val === undefined || val === "") { if (excludeEmpty) return; val = "(Empty)"; }
        counts[val] = (counts[val] || 0) + 1; validCount++;
    });

    var sortedKeys = Object.keys(counts).sort((a, b) => counts[b] - counts[a]);
    var topKeys = sortedKeys.slice(0, 30);
    var chartData = topKeys.map(k => counts[k]);

    if (dynChart) dynChart.dispose();
    var el = document.getElementById('dynamic-chart'); if(!el) return;
    dynChart = echarts.init(el);

    var labelFormatter = function(p) { var pct = validCount > 0 ? ((p.value / validCount) * 100).toFixed(1) : 0; return p.name + '\n' + p.value.toLocaleString() + ' (' + pct + '%)'; };
    // [Fix] showLabel 값 적용
    var labelOpt = { show: showLabel, position: (chartType=='bar'||chartType=='line')?'top':'inside', formatter: labelFormatter };

    var option = {
        title: { text: `${groupBy} Analysis`, subtext: `Total: ${validCount}`, left: 'center' },
        tooltip: { trigger: 'item' }, legend: { bottom: 0, type: 'scroll' }, color: PALETTE, series: []
    };

    if (chartType === 'bar' || chartType === 'line') {
        option.xAxis = { type: 'category', data: topKeys, axisLabel: { rotate: 45 } }; option.yAxis = { type: 'value' };
        option.series = [{ type: chartType, data: chartData, label: labelOpt, itemStyle: { color: p => PALETTE[p.dataIndex % PALETTE.length] }, showBackground: true }];
    } else if (chartType.includes('pie') || chartType === 'donut') {
        var pData = topKeys.map((k, i) => ({ name: k, value: chartData[i] }));
        var radius = chartType === 'donut' ? ['40%', '70%'] : '60%';
        option.series = [{ type: 'pie', radius: radius, data: pData, label: { show: showLabel, formatter: '{b}: {c} ({d}%)' }, itemStyle: { borderRadius: 5, borderColor: '#fff', borderWidth: 2 } }];
    } else if (chartType === 'radar') {
         var maxVal = Math.max(...chartData) || 100; option.radar = { indicator: topKeys.slice(0,8).map(k => ({ name: k, max: maxVal })), radius: '65%' };
         option.series = [{ type: 'radar', data: [{ value: topKeys.slice(0,8).map(k => counts[k]), name: 'Data' }], areaStyle: {opacity:0.3}, label: { show: showLabel } }]; // Radar Label
    } else if (chartType === 'treemap') {
         option.series = [{ type: 'treemap', data: topKeys.map((k, i) => ({ name: k, value: chartData[i], itemStyle: { color: PALETTE[i%PALETTE.length] } })), label: { show: showLabel, formatter: '{b}: {c}' } }];
    }

    dynChart.setOption(option);
    dynChart.off('click');
    dynChart.on('click', function(p) { if(window.showDrillDown) window.showDrillDown(groupBy, p.name, targetProj); });
}

function showDrillDown(key, val, targetProj) {
    // (기존과 동일)
    $('#drill-category').text(`${key} : ${val}`); $('#drilldown-section').fadeIn();
    var data = window.GLOBAL_DATA || [];
    var filtered = data.filter(function(item) {
        var matchProj = targetProj === '' || item._ProjectId === targetProj;
        var v = item[key]; if (val === "(Empty)") v = (v===null||v===undefined||v==="") ? "(Empty)" : v;
        return matchProj && (v == val);
    });
    if(typeof initGrid === 'function') initGrid('#drillTable', filtered.slice(0, 100), '#grid-toolbar-drill');
    $('html, body').animate({ scrollTop: $("#drilldown-section").offset().top }, 500);
}