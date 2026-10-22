/* DIQS Core: Logic Hub (Grid, Chart, KPI) - v3.3 Final */

// Global Variables
var table, drillTable, kpiChart, trendChart, dynChart;
var chartObj = null; 
const PALETTE = ['#5470c6', '#91cc75', '#fac858', '#ee6666', '#73c0de', '#3ba272', '#fc8452', '#9a60b4', '#ea7ccc'];
const SOLID = PALETTE;

// --- [1. Grid System] ---

function initGrid(tableSelector, data, toolbarSelector) {
    if (!data || data.length === 0) {
        if ($.fn.DataTable.isDataTable(tableSelector)) $(tableSelector).DataTable().destroy();
        $(tableSelector).html('<thead><tr><th>No Data</th></tr></thead><tbody><tr><td>데이터가 없습니다.</td></tr></tbody>');
        return;
    }

    var cols = [];
    var colNames = [];

    // Metadata
    ['_ProjectId', '_Process', '_ErrorType', '_ErrorDesc', '_UpdateDate'].forEach(k => {
        if (data[0].hasOwnProperty(k)) {
            var t = k.replace('_', '');
            cols.push({
                title: t, data: k,
                render: k === '_ErrorType' ? d => `<span class='badge bg-danger'>${d}</span>` : null
            });
            colNames.push({ id: k, text: t });
        }
    });

    // Business Data
    Object.keys(data[0]).forEach(k => {
        if (!k.startsWith('_')) {
            cols.push({ title: k, data: k });
            colNames.push({ id: k, text: k });
        }
    });

    // Init DataTable
    if ($.fn.DataTable.isDataTable(tableSelector)) $(tableSelector).DataTable().destroy();
    $(tableSelector).empty();

    var dt = $(tableSelector).DataTable({
        data: data, columns: cols, dom: 'rtip',
        scrollX: true, pageLength: 15, deferRender: true, searching: true,
        createdRow: function(row, d) {
            $(row).on('click', function(e) { if (e.altKey) showLineage(d); });
        }
    });

    // Build Toolbar
    if (toolbarSelector) {
        var $container = $(toolbarSelector);
        if ($container.length === 0) {
            // Self-Healing: Create container if missing
            $container = $(`<div id="${toolbarSelector.replace('#','')}" class="d-flex align-items-center gap-2"></div>`);
            $(tableSelector).parent().before($container);
        }
        $container.empty();

        new $.fn.dataTable.Buttons(dt, {
            buttons: [ { extend: 'copy', className: 'btn btn-sm btn-outline-secondary' }, { extend: 'excel', className: 'btn btn-sm btn-success', text: '<i class="bi bi-file-excel"></i> Excel' } ]
        }).container().appendTo($container);

        var uid = Date.now() + Math.floor(Math.random() * 1000);
        var searchHtml = `
            <div class="input-group input-group-sm ms-2" style="width: 450px;">
                <select class="form-select bg-light search-col" id="sc-${uid}" style="max-width: 120px;"><option value="all">All Columns</option></select>
                <input type="text" class="form-control border-start-0 search-input" id="si-${uid}" placeholder="검색어 (즉시 반응)">
                <div class="input-group-text bg-white"><input class="form-check-input mt-0 search-exact" id="se-${uid}" type="checkbox"><label class="form-check-label small ms-1">일치</label></div>
            </div><span class="badge bg-secondary ms-2 search-count" id="cnt-${uid}">${data.length.toLocaleString()}건</span>`;
        $container.append(searchHtml);

        var $sel = $(`#sc-${uid}`); colNames.forEach(c => $sel.append(new Option(c.text, c.id)));
        var $input = $(`#si-${uid}`); var $exact = $(`#se-${uid}`); var $count = $(`#cnt-${uid}`);

        function triggerSearch() { dt.draw(); $count.text(dt.rows({ filter: 'applied' }).count().toLocaleString() + "건"); }
        $input.on('input', triggerSearch); $sel.on('change', triggerSearch); $exact.on('change', triggerSearch);

        $.fn.dataTable.ext.search.push(function(settings, searchData, index, rowData) {
            if (settings.nTable.id !== $(tableSelector).attr('id')) return true;
            var raw = settings.aoData[index]._aData; if (!raw) return true;
            var col = $sel.val(); var term = ($input.val() || '').toLowerCase(); var isExact = $exact.is(':checked');
            if (!term) return true;
            if (col === 'all') return Object.values(raw).some(val => { var s = String(val||'').toLowerCase(); return isExact ? s === term : s.includes(term); });
            else { var val = String(raw[col]||'').toLowerCase(); return isExact ? val === term : val.includes(term); }
        });
    }
    
    if (tableSelector === '#univTable') table = dt; else drillTable = dt;
}

// --- [2. Chart: KPI] ---

function renderKPI(kpi) {
    var el = document.querySelector("#kpi-gauge"); if(!el) return;

    $('#kpi-error-count').text(kpi.ErrorCount.toLocaleString());
    var dod = kpi.DoD || 0;
    var icon = dod > 0 ? "▲" : (dod < 0 ? "▼" : "-");
    var color = dod > 0 ? "text-success" : (dod < 0 ? "text-danger" : "text-muted");
    
    var diffCount = 0;
    if (typeof SERVER_DATA !== 'undefined' && SERVER_DATA.History && SERVER_DATA.History.length > 1) {
        var last = SERVER_DATA.History[SERVER_DATA.History.length - 1];
        var prev = SERVER_DATA.History[SERVER_DATA.History.length - 2];
        diffCount = (last.Error || 0) - (prev.Error || 0);
    }
    var countSign = diffCount > 0 ? "+" : "";
    var countColor = diffCount > 0 ? "text-danger" : (diffCount < 0 ? "text-success" : "text-muted");

    $('#kpi-dod-val').html(`<span class="${color}">${icon} ${Math.abs(dod).toFixed(2)}%</span> <small class="${countColor}">(${countSign}${diffCount}건)</small>`);

    var target = kpi.TargetScore || 98.0; var score = kpi.Score;
    var gap = (target - score).toFixed(1);
    var gapText = gap > 0 ? `(목표 -${gap}%)` : `(목표 달성!)`;
    var gapColor = gap > 0 ? "text-danger" : "text-success";

    $('#kpi-target-gap').html(`${gapText}`).removeClass('text-danger text-success').addClass(gapColor);
    $('#kpi-current-val').text(score + "%"); $('#kpi-target-val').text(target + "%");

    var pct = Math.min(100, (score / target) * 100).toFixed(1);
    $('#kpi-progress-bar').css('width', pct + '%');
    if (pct >= 100) $('#kpi-progress-bar').addClass('bg-success').removeClass('bg-warning');
    else $('#kpi-progress-bar').addClass('bg-warning').removeClass('bg-success');

    var chartColor = score >= 95 ? '#00E396' : (score >= 85 ? '#FEB019' : '#FF4560');
    var options = {
        series: [score], chart: { type: 'radialBar', height: 280, fontFamily: 'Pretendard, sans-serif', toolbar: { show: true } },
        colors: [chartColor], fill: { type: 'solid' },
        plotOptions: { radialBar: { hollow: { size: '65%' }, dataLabels: { value: { fontSize: '30px', fontWeight: 'bold', formatter: v => v + '%' } } } },
        labels: ['정합률']
    };
    if (kpiChart) kpiChart.destroy(); kpiChart = new ApexCharts(el, options); kpiChart.render();
}

// --- [3. Chart: Trend] ---

function renderTrend(days, historyData) {
    if (!historyData || !Array.isArray(historyData)) return;
    
    var data = historyData.slice(Math.max(historyData.length - days, 0));
    var targetScore = 98.0;
    if(typeof SERVER_DATA !== 'undefined' && SERVER_DATA.KPI) targetScore = SERVER_DATA.KPI.TargetScore || 98.0;

    var step = Math.ceil(data.length / 12); var sampled = [];
    for (var i = 0; i < data.length; i += step) sampled.push(data[i]);
    if (sampled.length > 0 && sampled[sampled.length - 1] !== data[data.length - 1]) sampled.push(data[data.length - 1]);

    var dates = sampled.map(d => d.Date); var rates = sampled.map(d => d.Rate); var errors = sampled.map(d => d.Error);

    if (trendChart) trendChart.destroy();
    var el = document.querySelector("#trend-chart"); if(!el) return;

    var options = {
        series: [ { name: "정합률(%)", type: 'area', data: rates }, { name: "오류건수", type: 'column', data: errors } ],
        chart: { type: 'line', height: 250, toolbar: { show: true }, 
            events: {
                click: function(e, c, cfg) {
                    if (cfg.dataPointIndex >= 0) {
                        var idx = cfg.dataPointIndex;
                        $('#brief-date').text(dates[idx]); $('#brief-rate').text(rates[idx] + "%"); $('#brief-error').text(errors[idx].toLocaleString());
                        $('#daily-briefing').hide().fadeIn();
                    }
                }
            }
        },
        stroke: { width: [3, 0], curve: 'smooth' }, xaxis: { categories: dates, type: 'datetime' },
        yaxis: [ { title: { text: 'Rate' }, min: 0, max: 100 }, { opposite: true, title: { text: 'Errors' } } ],
        colors: ['#008FFB', '#FF4560'], fill: { type: 'solid', opacity: [0.2, 1] }, plotOptions: { bar: { columnWidth: '40%' } },
        annotations: { yaxis: [{ y: targetScore, borderColor: '#775DD0', strokeDashArray: 0, borderWidth: 4, label: { borderColor: '#775DD0', style: { color: '#fff', background: '#775DD0' }, text: 'Goal' } }] },
        tooltip: { x: { format: 'yyyy-MM-dd' } }
    };
    trendChart = new ApexCharts(el, options); trendChart.render();
}

// --- [4. Chart: Analysis] ---

function initAnalysisControls() {
    var data = window.GLOBAL_DATA || []; if(data.length === 0) return;
    var keys = Object.keys(data[0]);
    var exclude = ['_SysId', '_Lineage', '_UpdateDate', '_ReportTitle'];
    keys.forEach(k => { if(!exclude.includes(k)) { var l = k.startsWith('_')?k.substring(1):k; $('#anlGroup').append(new Option(l, k)); } });
    if(keys.includes('_ErrorType')) $('#anlGroup').val('_ErrorType');
    var projs = [...new Set(data.map(d => d._ProjectId))].sort();
    projs.forEach(p => $('#anlProject').append(new Option(p, p)));
}

function resetAnalysisOptions() { $('#anlProject').val(''); $('#chkEmpty').prop('checked', false); $('#chkShowLabel').prop('checked', true); renderAnalysis(); }

function renderAnalysis(filteredData) {
    var targetData = filteredData || window.GLOBAL_DATA || [];
    var groupBy = $('#anlGroup').val(); var chartType = $('#anlType').val(); var targetProj = $('#anlProject').val();
    var excludeEmpty = $('#chkEmpty').is(':checked'); var showLabel = $('#chkShowLabel').is(':checked');
    
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
    var labelOpt = { show: showLabel, position: (chartType=='bar'||chartType=='line')?'top':'inside', formatter: labelFormatter };
    var option = { title: { text: `${groupBy} 분석`, subtext: `Total: ${validCount}`, left: 'center' }, tooltip: { trigger: 'item' }, legend: { bottom: 0, type: 'scroll' }, color: PALETTE, series: [] };

    if (chartType === 'bar' || chartType === 'line') {
        option.xAxis = { type: 'category', data: topKeys, axisLabel: { rotate: 45 } }; option.yAxis = { type: 'value' };
        option.series = [{ type: chartType, data: chartData, label: labelOpt, itemStyle: { color: p => PALETTE[p.dataIndex % PALETTE.length] } }];
    } else if (chartType.includes('pie') || chartType === 'donut') {
        var pData = topKeys.map((k, i) => ({ name: k, value: chartData[i] }));
        var radius = chartType === 'donut' ? ['40%', '70%'] : '60%';
        option.series = [{ type: 'pie', radius: radius, data: pData, label: { show: showLabel, formatter: '{b}: {c} ({d}%)' } }];
    } else if (chartType === 'radar') {
         var maxVal = Math.max(...chartData) || 100; option.radar = { indicator: topKeys.slice(0,8).map(k => ({ name: k, max: maxVal })), radius: '65%' };
         option.series = [{ type: 'radar', data: [{ value: topKeys.slice(0,8).map(k => counts[k]), name: 'Data' }], areaStyle: {opacity:0.3} }];
    } else if (chartType === 'treemap') {
         option.series = [{ type: 'treemap', data: topKeys.map((k, i) => ({ name: k, value: chartData[i], itemStyle: { color: PALETTE[i%PALETTE.length] } })), label: { show: showLabel, formatter: '{b}: {c}' } }];
    }

    dynChart.setOption(option);
    dynChart.off('click');
    dynChart.on('click', function(p) { if(window.showDrillDown) window.showDrillDown(groupBy, p.name, targetProj); });
    $('#drilldown-section').hide();
}

// [Drill-down]
function showDrillDown(key, val, targetProj) {
    $('#drill-category').text(`${key} : ${val}`); $('#drilldown-section').fadeIn();
    var data = window.GLOBAL_DATA || [];
    var filtered = data.filter(function(item) {
        var matchProj = targetProj === '' || item._ProjectId === targetProj;
        var v = item[key]; if (val === "(Empty)") v = (v===null||v===undefined||v==="") ? "(Empty)" : v;
        return matchProj && (v == val);
    });
    var sample = filtered.slice(0, 100);
    initGrid('#drillTable', sample, '#grid-toolbar-drill'); // [핵심] 드릴다운 그리드 생성
    $('html, body').animate({ scrollTop: $("#drilldown-section").offset().top }, 500);
}

// [Helpers]
function fillSelect(el, data, key) { if (!el) return; var set = new Set(); data.forEach(d => { if (d[key]) set.add(d[key]); }); [...set].sort().forEach(v => el.append(new Option(v, v))); }
function safeStr(val) { return (val === null || val === undefined) ? '' : String(val); }
function showLineage(data) { var text = data._Lineage || "계보 정보 없음"; if (typeof Swal !== 'undefined') Swal.fire({ title: '🧬 데이터 계보', text: text, icon: 'info' }); else alert(text); }