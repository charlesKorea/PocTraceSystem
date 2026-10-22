'use strict';
/**
 * Check Rules 페이지: 일별 Rule 수 추이 + 규칙 설명/수행(이슈) 횟수
 */
window.PocTrace = window.PocTrace || {};
(function (PT) {
  function t(key) {
    return (typeof window.T === 'function') ? window.T(key) : key;
  }

  PT.renderRulesPage = function (catalog, history, meta) {
    const rules = catalog || [];
    const hist = (history || []).slice().sort(function (a, b) {
      return String(a.Date).localeCompare(String(b.Date));
    });
    const ruleCount = (meta && meta.ruleCount) || rules.length;
    const totalHits = (meta && meta.totalHits) != null
      ? meta.totalHits
      : rules.reduce(function (s, r) { return s + (Number(r.HitCount) || 0); }, 0);
    const genDate = (meta && meta.genDate) || '';

    if (typeof PT.setHeaderMeta === 'function') {
      PT.setHeaderMeta(genDate, t('nav_rules') + ': ' + ruleCount);
    }
    if (typeof PT.rebuildLineNav === 'function') PT.rebuildLineNav();
    document.title = 'M-Link : Check Rules';

    const html =
      '<div class="row mb-3">' +
      '<div class="col-12"><h3 class="fw-bold">' + t('rules_page_title') + '</h3>' +
      '<p class="text-muted small mb-0">' + t('rules_page_hint') + '</p></div></div>' +
      '<div class="row mb-4">' +
      '<div class="col-md-4"><div class="kpi-card total"><div class="kpi-label">' + t('kpi_check_rules') +
      '</div><div class="kpi-value text-primary">' + PT.formatNum(ruleCount) +
      '</div><div class="kpi-sub-text">' + t('rules_registered') + '</div></div></div>' +
      '<div class="col-md-4"><div class="kpi-card pass"><div class="kpi-label">' + t('rules_total_hits') +
      '</div><div class="kpi-value" style="background:linear-gradient(45deg,#4361ee,#2a9d8f);-webkit-background-clip:text;-webkit-text-fill-color:transparent;">' +
      PT.formatNum(totalHits) + '</div><div class="kpi-sub-text">' + t('rules_hits_hint') + '</div></div></div>' +
      '<div class="col-md-4"><div class="kpi-card hookup"><div class="kpi-label">' + t('lbl_generated') +
      '</div><div class="kpi-value fs-4 text-dark">' + (genDate || '-') +
      '</div><div class="kpi-sub-text">data/Rules_History.json</div></div></div>' +
      '</div>' +
      '<div class="row mb-4"><div class="col-12"><div class="chart-container-wrapper">' +
      '<h5 class="fw-bold mb-3"><i class="bi bi-graph-up"></i> ' + t('rules_chart_title') + '</h5>' +
      '<div id="rulesTrendChart" class="chart-body-lg"></div></div></div></div>' +
      '<div class="row"><div class="col-12"><div class="card shadow-sm"><div class="card-body">' +
      '<h5 class="fw-bold mb-3">' + t('rules_list_title') + '</h5>' +
      '<div class="table-responsive"><table class="table table-hover align-middle" id="rulesTable">' +
      '<thead class="table-light"><tr>' +
      '<th style="width:4rem">#</th>' +
      '<th>' + t('rules_col_id') + '</th>' +
      '<th>' + t('rules_col_desc') + '</th>' +
      '<th class="text-end" style="width:8rem">' + t('rules_col_hits') + '</th>' +
      '</tr></thead><tbody></tbody></table></div></div></div></div></div>';

    $('#main-container').html(html);

    const $tb = $('#rulesTable tbody');
    rules.forEach(function (r, i) {
      $tb.append(
        '<tr><td class="text-muted">' + (r.Order || (i + 1)) + '</td>' +
        '<td class="fw-semibold text-nowrap">' + $('<div>').text(r.RuleId || '').html() + '</td>' +
        '<td>' + $('<div>').text(r.Description || '').html() + '</td>' +
        '<td class="text-end fw-bold">' + PT.formatNum(r.HitCount || 0) + '</td></tr>'
      );
    });

    const dom = document.getElementById('rulesTrendChart');
    if (!dom || typeof echarts === 'undefined') return;
    const chart = echarts.init(dom);
    if (PT.state && PT.state.charts) PT.state.charts.push(chart);

    const dates = hist.map(function (h) { return h.Date; });
    const counts = hist.map(function (h) { return Number(h.RuleCount) || 0; });
    const hits = hist.map(function (h) { return Number(h.TotalHits) || 0; });

    const palette = ['#4361ee', '#2a9d8f', '#e76f51', '#f4a261', '#e9c46a', '#264653', '#9b5de5', '#00bbf9', '#00f5d4', '#fee440'];
    const hitKeys = [];
    const hitKeySet = {};
    hist.forEach(function (h) {
      const map = h.Hits || {};
      Object.keys(map).forEach(function (k) {
        if (!hitKeySet[k]) {
          hitKeySet[k] = true;
          hitKeys.push(k);
        }
      });
    });
    // Top contributors first for legend readability
    hitKeys.sort(function (a, b) {
      const sumA = hist.reduce(function (s, h) { return s + (Number((h.Hits || {})[a]) || 0); }, 0);
      const sumB = hist.reduce(function (s, h) { return s + (Number((h.Hits || {})[b]) || 0); }, 0);
      return sumB - sumA;
    });
    const topHitKeys = hitKeys.slice(0, 8);

    const series = [
      {
        name: t('kpi_check_rules'), type: 'line', data: counts,
        smooth: true, color: '#1d3557', symbolSize: 9, z: 10,
        label: { show: true, formatter: '{c}' },
        lineStyle: { width: 3 }
      },
      {
        name: t('rules_total_hits'), type: 'line', yAxisIndex: 1, data: hits,
        smooth: true, color: '#457b9d', symbol: 'diamond', symbolSize: 8, z: 9,
        areaStyle: { opacity: 0.08 },
        lineStyle: { width: 2, type: 'dashed' }
      }
    ];
    topHitKeys.forEach(function (key, idx) {
      series.push({
        name: key,
        type: 'bar',
        stack: 'hits',
        yAxisIndex: 1,
        barMaxWidth: 28,
        emphasis: { focus: 'series' },
        itemStyle: { color: palette[idx % palette.length] },
        data: hist.map(function (h) { return Number((h.Hits || {})[key]) || 0; })
      });
    });

    chart.setOption({
      tooltip: {
        trigger: 'axis',
        axisPointer: { type: 'cross' }
      },
      legend: { type: 'scroll', bottom: 0 },
      grid: { left: '3%', right: '4%', bottom: '18%', top: '12%', containLabel: true },
      xAxis: { type: 'category', data: dates, boundaryGap: true },
      yAxis: [
        { type: 'value', name: t('kpi_check_rules'), minInterval: 1 },
        { type: 'value', name: t('rules_total_hits'), splitLine: { show: false } }
      ],
      series: series
    });
  };

  window.renderRulesPage = PT.renderRulesPage;
})(window.PocTrace);
