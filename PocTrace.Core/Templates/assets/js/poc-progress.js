'use strict';
/**
 * Design Progress 페이지
 * - VOC: 저신뢰/BOM/N/최근설계일 설명, 추이 숫자, DataTables, Tag→UT 펼침
 */
window.PocTrace = window.PocTrace || {};
(function (PT) {
  function t(key) {
    return (typeof window.T === 'function') ? window.T(key) : key;
  }
  function esc(s) {
    return $('<div>').text(s == null ? '' : String(s)).html();
  }
  function pctCell(row, field) {
    const baselineField = field === 'BomPct' ? 'BaselineBomMedian' : 'BaselineLengthMedian';
    const currentField = field === 'BomPct' ? 'CurrentBomCount' : 'CurrentLength';
    const base = Number(row[baselineField]) || 0;
    const cur = Number(row[currentField]) || 0;
    if (base <= 0) {
      return '<span class="text-muted" title="' + esc(t('progress_pct_no_baseline')) + '">—</span>';
    }
    const pct = Number(row[field]) || 0;
    return pct.toFixed(1);
  }

  function designPeriodCell(row) {
    const first = row.FirstDesignAt || '';
    const last = row.LastDesignAt || '';
    const days = row.DesignDays;
    if (!first && !last) {
      return '<span class="text-muted" title="' + esc(t('progress_last_dash_hint')) + '">-</span>';
    }
    let html = '<div class="design-period">';
    if (first && last) {
      html += '<span class="design-period-dates">' + esc(first) + ' → ' + esc(last) + '</span>';
    } else {
      html += '<span class="design-period-dates">' + esc(first || last) + '</span>';
    }
    if (days != null && days !== '') {
      html += '<span class="design-period-days">' + esc(String(days)) + t('progress_days_unit') + '</span>';
    }
    html += '</div>';
    return html;
  }

  function progressMiniBar(pct, overrun) {
    const v = Math.max(0, Math.min(100, Number(pct) || 0));
    const fillCls = overrun ? ' progress-mini-fill overrun' : ' progress-mini-fill';
    return '<div class="progress-mini-cell" role="img" aria-label="' + esc(v.toFixed(1) + '%') + '">' +
      '<div class="progress-mini-track"><div class="' + fillCls.trim() + '" style="width:' + v + '%"></div></div>' +
      '<span class="progress-mini-label">' + v.toFixed(1) + '%</span></div>';
  }

  function fmtNum1(n) {
    const v = Number(n) || 0;
    if (Math.abs(v) >= 100) return PT.formatNum(Math.round(v));
    return v.toFixed(1);
  }

  function metricDetail(cur, base, pct, unit) {
    const c = Number(cur) || 0;
    const b = Number(base) || 0;
    const p = Number(pct) || 0;
    if (b <= 0) {
      return '<span class="text-muted" title="' + esc(t('progress_pct_no_baseline')) + '">—</span>';
    }
    const unitHtml = unit ? (' <span class="text-muted progress-ut-unit">' + esc(unit) + '</span>') : '';
    return '<span class="progress-ut-metric">' +
      '<span class="progress-ut-cur">' + esc(fmtNum1(c)) + '</span>' +
      '<span class="text-muted"> / </span>' +
      '<span class="progress-ut-base">' + esc(fmtNum1(b)) + '</span>' +
      unitHtml +
      '<span class="progress-ut-pct text-primary"> (' + p.toFixed(1) + '%)</span></span>';
  }

  function renderUtilityTable(utils) {
    if (!utils || !utils.length) {
      return '<p class="text-muted small mb-0">' + esc(t('progress_ut_empty')) + '</p>';
    }
    let html = '<div class="table-responsive progress-ut-table-wrap">' +
      '<table class="table table-sm table-hover mb-0 progress-ut-table">' +
      '<thead><tr>' +
      '<th>' + esc(t('progress_ut_col_utility')) + '</th>' +
      '<th class="text-end">' + esc(t('progress_ut_col_length')) + '</th>' +
      '<th class="text-end">' + esc(t('progress_ut_col_bom')) + '</th>' +
      '<th class="text-end">' + esc(t('progress_col_pct')) + '</th>' +
      '</tr></thead><tbody>';
    utils.forEach(function (u) {
      html += '<tr' + (u.Overrun ? ' class="table-warning"' : '') + '>' +
        '<td class="fw-semibold">' + esc(u.UtilityName || '-') + '</td>' +
        '<td class="text-end text-nowrap">' + metricDetail(u.CurrentLength, u.BaselineLengthMedian, u.LengthPct, 'mm') + '</td>' +
        '<td class="text-end text-nowrap">' + metricDetail(u.CurrentBomCount, u.BaselineBomMedian, u.BomPct, '') + '</td>' +
        '<td class="text-end">' + progressMiniBar(u.ProgressPct, u.Overrun) + '</td>' +
        '</tr>';
    });
    html += '</tbody></table></div>';
    return html;
  }

  function statusBadges(row) {
    if (typeof PT.progressStatusBadges === 'function') {
      return PT.progressStatusBadges(row, esc, t);
    }
    let html = '<span class="status-stack">';
    html += row.IsInProgress
      ? '<span class="status-badge active">' + t('progress_fresh_yes') + '</span>'
      : '<span class="status-badge idle">' + t('progress_fresh_no') + '</span>';
    if (row.Overrun) html += '<span class="status-badge over">Over</span>';
    if (row.LowConfidence) {
      html += '<span class="status-badge lowconf" title="' + esc(t('progress_low_conf_hint')) + '">?</span>';
    }
    html += '</span>';
    return html;
  }

  PT.renderProgressPage = function (rows, history, meta) {
    const data = (rows || []).slice();
    const hist = (history || []).slice().sort(function (a, b) {
      return String(a.Date || '').localeCompare(String(b.Date || ''));
    });
    const m = meta || {};
    const dbName = m.dbName || 'ALL';
    const genDate = m.genDate || '';
    const totalEq = m.totalEquip != null ? m.totalEquip : data.length;
    const inProg = m.inProgressCount != null ? m.inProgressCount : data.filter(function (r) { return r.IsInProgress; }).length;
    const lowConf = m.lowConfidenceCount != null ? m.lowConfidenceCount : data.filter(function (r) { return r.LowConfidence; }).length;
    const avgPct = m.avgProgressPct != null ? m.avgProgressPct : 0;

    if (typeof PT.setHeaderMeta === 'function') {
      PT.setHeaderMeta(genDate, t('nav_progress') + ': ' + dbName);
    }
    if (typeof PT.rebuildLineNav === 'function') PT.rebuildLineNav();
    document.title = 'M-Link : Design Progress' + (dbName !== 'ALL' ? ' [' + dbName + ']' : '');

    const showDb = data.some(function (r) { return r.DbName; });

    // 최근설계일 내림차순 기본 정렬 (빈 날짜는 맨 아래)
    data.sort(function (a, b) {
      const da = a.LastDesignAt || '';
      const db2 = b.LastDesignAt || '';
      if (!da && !db2) return 0;
      if (!da) return 1;
      if (!db2) return -1;
      return String(db2).localeCompare(String(da));
    });
    data.forEach(function (r, i) { r._rowNo = i + 1; });

    const html =
      '<div class="row mb-3"><div class="col-12">' +
      '<h3 class="fw-bold">' + t('progress_page_title') + '</h3>' +
      '<p class="text-muted small mb-2">' + t('progress_page_hint') + '</p>' +
      '<details class="progress-voc-panel mb-2"><summary class="fw-semibold text-primary">' + t('progress_voc_title') + '</summary>' +
      '<div class="progress-voc-body small mt-2">' +
      '<h6 class="fw-bold">' + t('progress_voc_lowconf_h') + '</h6><p>' + t('progress_voc_lowconf_b') + '</p>' +
      '<h6 class="fw-bold">' + t('progress_voc_formula_h') + '</h6><p>' + t('progress_voc_formula_b') + '</p>' +
      '<h6 class="fw-bold">' + t('progress_voc_bom_h') + '</h6><p>' + t('progress_voc_bom_b') + '</p>' +
      '<h6 class="fw-bold">' + t('progress_voc_n_h') + '</h6><p>' + t('progress_voc_n_b') + '</p>' +
      '<h6 class="fw-bold">' + t('progress_voc_last_h') + '</h6><p>' + t('progress_voc_last_b') + '</p>' +
      '<h6 class="fw-bold">' + t('progress_voc_first_h') + '</h6><p>' + t('progress_voc_first_b') + '</p>' +
      '</div></details></div></div>' +
      '<div class="row g-3 mb-3">' +
      '<div class="col-6 col-md-3"><div class="kpi-card total"><div class="kpi-label">' + t('kpi_total_eq') +
      '</div><div class="kpi-value text-primary">' + PT.formatNum(totalEq) + '</div></div></div>' +
      '<div class="col-6 col-md-3"><div class="kpi-card pass"><div class="kpi-label">' + t('progress_in_progress') +
      '</div><div class="kpi-value text-warning">' + PT.formatNum(inProg) + '</div></div></div>' +
      '<div class="col-6 col-md-3"><div class="kpi-card hookup"><div class="kpi-label">' + t('progress_avg_pct') +
      '</div><div class="kpi-value text-primary">' + Number(avgPct).toFixed(1) + '%</div></div></div>' +
      '<div class="col-6 col-md-3"><div class="kpi-card fail" title="' + esc(t('progress_voc_lowconf_h')) + '"><div class="kpi-label">' + t('progress_low_conf') +
      '</div><div class="kpi-value text-muted">' + PT.formatNum(lowConf) + '</div>' +
      '<div class="kpi-sub-text">' + t('progress_low_conf_hint') + '</div></div></div>' +
      '</div>' +
      '<details class="ui-collapse-panel mb-3" id="progress-chart-panel">' +
      '<summary><i class="bi bi-graph-up"></i> ' + t('progress_chart_title') + '</summary>' +
      '<div class="ui-collapse-body"><div class="chart-container-wrapper mb-0">' +
      '<div id="progressTrendChart" class="chart-body"></div></div></div></details>' +
      '<div class="row"><div class="col-12"><div class="card shadow-sm border"><div class="card-body p-2 p-md-3">' +
      '<div class="d-flex justify-content-between align-items-center mb-3 flex-wrap gap-2">' +
      '<h5 class="fw-bold mb-0">' + t('progress_table_title') + '</h5>' +
      '<div class="form-check form-switch">' +
      '<input class="form-check-input" type="checkbox" id="progressOnlyInProg">' +
      '<label class="form-check-label" for="progressOnlyInProg">' + t('progress_filter_inprog') + '</label></div></div>' +
      '<div class="table-sticky-wrap"><table class="table table-hover table-sm align-middle w-100 mb-0" id="progressTable"></table></div>' +
      '<div id="progressUtilityPanel" class="mt-4 d-none">' +
      '<div class="d-flex justify-content-between align-items-center mb-2">' +
      '<h6 class="fw-bold mb-0" id="progressUtilityTitle"></h6>' +
      '<button type="button" class="btn btn-sm btn-outline-secondary" id="progressUtBackBtn" title="' + esc(t('progress_btn_back_table')) + '">' +
      '<i class="bi bi-arrow-up-circle"></i> ' + t('progress_btn_back_table') + '</button></div>' +
      '<div id="progressUtilityChart" style="height:260px;"></div>' +
      '<div id="progressUtilityTable" class="mt-3"></div></div>' +
      '</div></div></div></div>';

    $('#main-container').html(html);

    if (PT.state.progressTable) {
      try { PT.state.progressTable.destroy(); } catch (e) { /* ignore */ }
      PT.state.progressTable = null;
    }

    const columns = [];
    columns.push({
      name: 'No', title: 'No', data: '_rowNo', className: 'text-center text-muted', width: '48px',
      orderable: true
    });
    if (showDb) {
      columns.push({ name: 'DbName', title: 'DB', data: 'DbName', className: 'small' });
    }
    columns.push({
      name: 'Tag', title: 'Tag', data: 'Tag',
      render: function (d, type, row) {
        if (type !== 'display') return d || '';
        const tip = row.MainName ? (' title="' + esc(row.MainName) + '"') : '';
        return '<a href="javascript:void(0)" class="fw-semibold progress-tag-link"' + tip + ' data-tag="' + esc(d) + '">' + esc(d) + '</a>';
      }
    });
    columns.push({ name: 'Process', title: t('header_proc'), data: 'Process', defaultContent: '-' });
    columns.push({
      name: 'DesignPeriod', title: t('progress_col_period'), data: null, className: 'design-date-group text-nowrap',
      render: function (d, type, row) {
        if (type === 'sort' || type === 'type') return row.LastDesignAt || row.FirstDesignAt || '';
        return designPeriodCell(row);
      }
    });
    columns.push({
      name: 'FirstDesignAt', title: t('progress_col_first'), data: 'FirstDesignAt', visible: false
    });
    columns.push({
      name: 'LastDesignAt', title: t('progress_col_last'), data: 'LastDesignAt', visible: false
    });
    columns.push({
      name: 'DesignDays', title: t('progress_col_days'), data: 'DesignDays', visible: false
    });
    columns.push({ name: 'MainName', title: 'MainName', data: 'MainName', visible: false });
    columns.push({
      name: 'Freshness', title: t('progress_col_fresh'), data: 'IsInProgress',
      render: function (d, type, row) {
        if (type !== 'display') return d ? 1 : 0;
        return statusBadges(row);
      }
    });
    columns.push({
      name: 'LengthPct', title: 'Length%', data: 'LengthPct', className: 'text-end',
      render: function (d, type, row) {
        if (type !== 'display') return Number(d) || 0;
        return pctCell(row, 'LengthPct');
      }
    });
    columns.push({
      name: 'BomPct', title: 'BOM%', data: 'BomPct', className: 'text-end',
      render: function (d, type, row) {
        if (type !== 'display') return Number(d) || 0;
        return pctCell(row, 'BomPct');
      }
    });
    columns.push({
      name: 'ProgressPct', title: t('progress_col_pct'), data: 'ProgressPct', className: 'text-end',
      render: function (d, type, row) {
        if (type !== 'display') return Number(d) || 0;
        return progressMiniBar(d, row.Overrun);
      }
    });
    columns.push({
      name: 'CohortN', title: 'N', data: 'CohortN', className: 'text-end text-muted',
      render: function (d, type) {
        if (type !== 'display') return Number(d) || 0;
        return '<span title="' + esc(t('progress_voc_n_h')) + '">' + (Number(d) || 0) + '</span>';
      }
    });

    const lastDesignColIdx = showDb ? 4 : 3;
    const colKey = 'poc_cols_progress_' + String(dbName || 'ALL');

    PT.state.progressTable = $('#progressTable').DataTable({
      data: data,
      columns: columns,
      pageLength: 20,
      lengthMenu: [10, 20, 50, 100],
      order: [[lastDesignColIdx, 'desc']],
      deferRender: true,
      scrollX: true,
      dom: '<"dt-top-container" <"dt-left-group" <"dt-custom-title"> l > <"dt-right-group" f <"dt-col-picker"> B > >rtip',
      buttons: [
        {
          extend: 'excelHtml5',
          text: '<i class="bi bi-file-earmark-excel"></i> ' + t('btn_export'),
          className: 'btn btn-success btn-sm fw-bold',
          title: 'DesignProgress_' + dbName
        }
      ],
      initComplete: function () {
        const api = this.api();
        PT.state.progressTable = api;
        if (typeof PT.applyColumnPrefs === 'function') {
          PT.applyColumnPrefs(api, colKey);
        }
        $('div.dt-custom-title').html(
          '<h5 class="m-0 fw-bold text-dark"><i class="bi bi-table" aria-hidden="true"></i> ' + t('progress_table_title') + '</h5>'
        );
        if (typeof PT.mountColumnPicker === 'function') {
          PT.mountColumnPicker({
            api: api,
            storageKey: colKey,
            containerSelector: 'div.dt-col-picker',
            lockedNames: ['Tag', 'No']
          });
        }
      }
    });

    let lastScrollY = 0;
    let activeUtTag = null;

    function showUtilityForTag(tag) {
      const row = data.find(function (r) { return r.Tag === tag; });
      if (!row) return;
      lastScrollY = window.scrollY || window.pageYOffset || 0;
      activeUtTag = tag;
      const $panel = $('#progressUtilityPanel');
      $panel.removeClass('d-none');
      $('#progressUtilityTitle').text((row.Tag || '') + ' — ' + t('progress_utility_title'));
      const utils = row.Utilities || [];
      $('#progressUtilityTable').html(renderUtilityTable(utils));
      const dom = document.getElementById('progressUtilityChart');
      if (!dom || typeof echarts === 'undefined') return;
      const chart = echarts.init(dom);
      if (PT.state && PT.state.charts) PT.state.charts.push(chart);
      chart.setOption({
        tooltip: {
          trigger: 'axis',
          formatter: function (params) {
            if (!params || !params.length) return '';
            const idx = params[0].dataIndex;
            const u = utils[idx];
            if (!u) return params[0].name;
            let tip = '<strong>' + esc(u.UtilityName || '') + '</strong><br/>';
            tip += t('progress_ut_col_length') + ': ' +
              fmtNum1(u.CurrentLength) + ' / ' + fmtNum1(u.BaselineLengthMedian) +
              ' mm (' + Number(u.LengthPct || 0).toFixed(1) + '%)<br/>';
            tip += t('progress_ut_col_bom') + ': ' +
              fmtNum1(u.CurrentBomCount) + ' / ' + fmtNum1(u.BaselineBomMedian) +
              ' (' + Number(u.BomPct || 0).toFixed(1) + '%)<br/>';
            tip += t('progress_col_pct') + ': ' + Number(u.ProgressPct || 0).toFixed(1) + '%';
            return tip;
          }
        },
        legend: { bottom: 0 },
        grid: { left: '3%', right: '4%', bottom: '14%', top: '12%', containLabel: true },
        xAxis: { type: 'category', data: utils.map(function (u) { return u.UtilityName; }) },
        yAxis: { type: 'value', max: 100, name: '%' },
        series: [
          {
            name: 'Length%', type: 'bar',
            data: utils.map(function (u) { return u.LengthPct; }),
            color: '#0071e3',
            label: {
              show: true, position: 'top',
              formatter: function (p) {
                const u = utils[p.dataIndex];
                if (!u || !(Number(u.BaselineLengthMedian) > 0)) return '—';
                return Number(u.LengthPct || 0).toFixed(0);
              }
            }
          },
          {
            name: 'BOM%', type: 'bar',
            data: utils.map(function (u) { return u.BomPct; }),
            color: '#34c759',
            label: {
              show: true, position: 'top',
              formatter: function (p) {
                const u = utils[p.dataIndex];
                if (!u || !(Number(u.BaselineBomMedian) > 0)) return '—';
                return Number(u.BomPct || 0).toFixed(0);
              }
            }
          },
          {
            name: 'Progress%', type: 'line',
            data: utils.map(function (u) { return u.ProgressPct; }),
            color: '#ff9f0a',
            label: { show: true, formatter: '{c}' }
          }
        ]
      });
      // 펼친 위치로 스크롤
      const top = $panel.offset().top - 72;
      window.scrollTo({ top: Math.max(0, top), behavior: 'smooth' });
    }

    $('#progressTable tbody').off('click.progressTag').on('click.progressTag', '.progress-tag-link', function (e) {
      e.preventDefault();
      showUtilityForTag($(this).data('tag'));
    });

    $('#progressUtBackBtn').off('click').on('click', function () {
      $('#progressUtilityPanel').addClass('d-none');
      window.scrollTo({ top: lastScrollY, behavior: 'smooth' });
      if (activeUtTag) {
        // 테이블 행 하이라이트 복귀
        $('#progressTable tbody tr').removeClass('table-active');
        $('#progressTable tbody .progress-tag-link').each(function () {
          if ($(this).data('tag') === activeUtTag) {
            $(this).closest('tr').addClass('table-active');
          }
        });
      }
    });

    $('#progressOnlyInProg').off('change').on('change', function () {
      const only = $(this).is(':checked');
      $.fn.dataTable.ext.search = $.fn.dataTable.ext.search.filter(function (fn) {
        return !fn._progressInProgFilter;
      });
      if (only) {
        const filterFn = function (settings, data, dataIndex) {
          if (settings.nTable.id !== 'progressTable') return true;
          const row = PT.state.progressTable.row(dataIndex).data();
          return !!(row && row.IsInProgress);
        };
        filterFn._progressInProgFilter = true;
        $.fn.dataTable.ext.search.push(filterFn);
      }
      PT.state.progressTable.draw();
    });

    const domTrend = document.getElementById('progressTrendChart');
    function initProgressTrendChart() {
      if (!domTrend || typeof echarts === 'undefined' || !hist.length) return;
      if (domTrend.getAttribute('data-chart-ready') === '1') {
        (PT.state.charts || []).forEach(function(c) { if (c && c.resize) c.resize(); });
        return;
      }
      domTrend.setAttribute('data-chart-ready', '1');
      const chart = echarts.init(domTrend);
      if (PT.state && PT.state.charts) PT.state.charts.push(chart);
      const dates = hist.map(function (h) { return h.Date + (h.DbName ? (' ' + h.DbName) : ''); });
      chart.setOption({
        tooltip: { trigger: 'axis' },
        legend: { bottom: 0 },
        grid: { left: '3%', right: '4%', bottom: '12%', top: '12%', containLabel: true },
        xAxis: { type: 'category', data: dates },
        yAxis: [
          { type: 'value', name: t('progress_in_progress'), minInterval: 1 },
          { type: 'value', name: '%', max: 100, splitLine: { show: false } }
        ],
        series: [
          {
            name: t('progress_in_progress'), type: 'bar',
            data: hist.map(function (h) { return Number(h.InProgressCount) || 0; }),
            color: '#ff9f0a', barMaxWidth: 36,
            label: { show: true, position: 'top', formatter: '{c}' }
          },
          {
            name: t('progress_avg_pct'), type: 'line', yAxisIndex: 1,
            data: hist.map(function (h) { return Number(h.AvgProgressPct) || 0; }),
            color: '#0071e3', smooth: true,
            label: { show: true, formatter: '{c}' }
          }
        ]
      });
    }
    if (typeof PT.bindLazyCharts === 'function') {
      PT.bindLazyCharts('#progress-chart-panel', initProgressTrendChart);
    }
  };

  window.renderProgressPage = PT.renderProgressPage;
})(window.PocTrace);
