'use strict';
window.PocTrace = window.PocTrace || {};
(function (PT) {
  const _ = window._;

  PT.state = {
    charts: [],
    dataTableInstance: null,
    mainNameIndex: null,
    lineChartInstance: null
  };

  PT.LINE_ALIAS = { 'x1': 'X1', 'x2': 'X2-PH1', 'x2_2': 'X2-PH2', 'x2_m': 'X2-M' };
  PT.getLineName = (dbName) => PT.LINE_ALIAS[dbName] || String(dbName || '').toUpperCase();
  PT.formatNum = (n) => n ? n.toLocaleString() : '0';

PT.rebuildLineNav = function() {
    const $nav = $('#line-nav');
    if (!$nav.length) return;
    $nav.empty();

    let lines = [];
    if (typeof SUMMARY_DATA !== 'undefined' && Array.isArray(SUMMARY_DATA) && SUMMARY_DATA.length) {
        lines = SUMMARY_DATA.map(d => d.DbName).filter(Boolean);
    } else if (typeof AVAILABLE_LINES !== 'undefined' && Array.isArray(AVAILABLE_LINES) && AVAILABLE_LINES.length) {
        lines = AVAILABLE_LINES.slice();
    } else if (typeof REPORT_META !== 'undefined' && REPORT_META.dbName) {
        lines = [REPORT_META.dbName];
    } else {
        lines = Object.keys(PT.LINE_ALIAS);
    }

    const seen = {};
    lines = lines.filter(db => {
        const k = String(db).toLowerCase();
        if (seen[k]) return false;
        seen[k] = true;
        return true;
    });

    const page = (location.pathname.split('/').pop() || location.href.split('/').pop() || '').split('?')[0];
    const currentDb = (typeof REPORT_META !== 'undefined' && REPORT_META.dbName)
        ? String(REPORT_META.dbName).toLowerCase()
        : '';

    const failed = {};
    if (typeof SUMMARY_DATA !== 'undefined' && Array.isArray(SUMMARY_DATA)) {
        SUMMARY_DATA.forEach(function (row) {
            if (row && row.Failed && row.DbName)
                failed[String(row.DbName).toLowerCase()] = true;
        });
    }

    lines.forEach(db => {
        const isFailed = !!failed[String(db).toLowerCase()];
        const href = isFailed ? 'index.html' : ('report_' + db + '.html');
        const isActive = !isFailed && (page === href || currentDb === String(db).toLowerCase());
        const label = PT.getLineName(db) + (isFailed ? ' (실패)' : '');
        const cls = 'rail-link' + (isActive ? ' is-active' : '') + (isFailed ? ' is-failed' : '');
        $nav.append(
            $('<li>').append(
                $('<a>').attr('href', href).addClass(cls).text(label)
            )
        );
    });
}

PT.scrollToNode = function(selector) {
    const el = typeof selector === 'string' ? document.querySelector(selector) : selector;
    const main = document.querySelector('.report-main');
    if (!el) return;
    if (main) {
        const top = main.scrollTop + (el.getBoundingClientRect().top - main.getBoundingClientRect().top) - 12;
        main.scrollTo({ top: Math.max(0, top), behavior: 'smooth' });
        return;
    }
    el.scrollIntoView({ behavior: 'smooth', block: 'start' });
};

PT.mountCommandBar = function() {
    const $root = $('#cmdk');
    if (!$root.length || $root.data('ready')) return;
    $root.data('ready', 1);

    const $input = $('#cmdk-input');
    const $list = $('#cmdk-list');
    let items = [];
    let active = 0;

    function t(key) {
        return (typeof window.T === 'function') ? window.T(key) : key;
    }

    function baseItems() {
        const list = [
            { group: t('cmdk_pages'), label: t('nav_summary'), href: 'index.html' },
            { group: t('cmdk_pages'), label: t('nav_rules'), href: 'rules.html' },
            { group: t('cmdk_pages'), label: t('nav_progress'), href: 'progress.html' }
        ];
        $('#line-nav a').each(function() {
            list.push({
                group: t('cmdk_lines'),
                label: $(this).text().trim(),
                href: $(this).attr('href')
            });
        });
        if ($('#result-filter-group').length) {
            [
                ['FAIL', 'filter_fail'],
                ['ALL', 'filter_all'],
                ['PASS', 'filter_pass']
            ].forEach(function(pair) {
                list.push({
                    group: t('cmdk_actions'),
                    label: t(pair[1]),
                    run: function() {
                        $('#result-filter-group button[data-filter="' + pair[0] + '"]').trigger('click');
                    }
                });
            });
            const excel = $('.btn-excel-wrapper a').attr('href');
            if (excel) {
                list.push({ group: t('cmdk_actions'), label: t('btn_export'), href: excel });
            }
            list.push({
                group: t('cmdk_actions'),
                label: t('cmdk_focus_search'),
                run: function() {
                    const input = document.querySelector('.dataTables_filter input');
                    if (input) input.focus();
                }
            });
        }
        return list;
    }

    function tagItems(query) {
        if (!query || query.length < 2) return [];
        const api = PT.state && PT.state.dataTableInstance;
        if (!api || typeof api.rows !== 'function') return [];
        const needle = query.toLowerCase();
        const out = [];
        const rows = api.rows().data().toArray();
        for (let i = 0; i < rows.length && out.length < 12; i++) {
            const tag = String((rows[i] && rows[i].Tag) || '');
            if (tag.toLowerCase().indexOf(needle) < 0) continue;
            out.push({
                group: t('cmdk_tags'),
                label: tag,
                run: function() {
                    const $all = $('#result-filter-group button[data-filter="ALL"]');
                    $('#result-filter-group button').removeClass('active');
                    $all.addClass('active');
                    api.column(3).search('');
                    api.search(tag).draw();
                    PT.scrollToNode('#mainTable');
                }
            });
        }
        return out;
    }

    function render(raw) {
        const query = String(raw || '').trim().toLowerCase();
        items = baseItems().concat(tagItems(query)).filter(function(item) {
            if (!query) return true;
            return (item.label + ' ' + item.group).toLowerCase().indexOf(query) >= 0;
        });
        active = 0;
        if (!items.length) {
            $list.html('<li class="cmdk-empty">' + $('<div>').text(t('cmdk_empty')).html() + '</li>');
            return;
        }
        let html = '';
        let lastGroup = '';
        items.forEach(function(item, idx) {
            if (item.group !== lastGroup) {
                html += '<li class="cmdk-group">' + $('<div>').text(item.group).html() + '</li>';
                lastGroup = item.group;
            }
            html += '<li><button type="button" class="cmdk-item' + (idx === 0 ? ' is-active' : '') +
                '" data-idx="' + idx + '">' + $('<div>').text(item.label).html() + '</button></li>';
        });
        $list.html(html);
    }

    function setActive(next) {
        if (!items.length) return;
        active = (next + items.length) % items.length;
        const buttons = $list.find('.cmdk-item').removeClass('is-active');
        const btn = buttons.get(active);
        if (!btn) return;
        btn.classList.add('is-active');
        if (btn.scrollIntoView) btn.scrollIntoView({ block: 'nearest' });
    }

    function openBar() {
        $input.attr('placeholder', t('cmdk_placeholder'));
        $root.find('.cmdk-panel').attr('aria-label', t('cmdk_open'));
        $input.val('');
        render('');
        $root.removeAttr('hidden');
        setTimeout(function() { $input.trigger('focus'); }, 0);
    }

    function closeBar() {
        $root.attr('hidden', 'hidden');
        $input.val('');
    }

    function choose(idx) {
        const item = items[idx];
        if (!item) return;
        closeBar();
        if (item.href) window.location.href = item.href;
        else if (typeof item.run === 'function') item.run();
    }

    $('#cmdk-open').on('click', function() {
        if ($root.is('[hidden]')) openBar();
        else closeBar();
    });
    $root.on('click', '[data-cmdk-close]', closeBar);
    $input.on('input', function() { render(this.value); });
    $list.on('click', '.cmdk-item', function() {
        choose(Number($(this).data('idx')));
    });
    $input.on('keydown', function(e) {
        if (e.key === 'ArrowDown') { e.preventDefault(); setActive(active + 1); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); setActive(active - 1); }
        else if (e.key === 'Enter') { e.preventDefault(); choose(active); }
        else if (e.key === 'Escape') { e.preventDefault(); closeBar(); }
    });
    $(document).on('keydown.cmdk', function(e) {
        const key = String(e.key || '');
        if ((e.ctrlKey || e.metaKey) && key.toLowerCase() === 'k') {
            e.preventDefault();
            if ($root.is('[hidden]')) openBar();
            else closeBar();
            return;
        }
        if (key === 'Escape' && !$root.is('[hidden]')) closeBar();
    });
};

PT.setHeaderMeta = function(genDate, dodText) {
    const parts = [];
    if (genDate) parts.push(`${window.T('lbl_generated')}: ${genDate}`);
    if (dodText) parts.push(dodText);
    $('#meta-date').html(parts.join(' · ') || '');
}

/** Collapsed chart panel: init once on first open, resize on reopen */
PT.bindLazyCharts = function(detailsSelector, initFn) {
    const el = document.querySelector(detailsSelector);
    if (!el || typeof initFn !== 'function') return;
    let initialized = false;
    const run = function() {
        if (!initialized) {
            initialized = true;
            initFn();
        }
        (PT.state.charts || []).forEach(function(c) {
            if (c && typeof c.resize === 'function') c.resize();
        });
    };
    el.addEventListener('toggle', function() {
        if (el.open) run();
    });
};

/** localStorage 컬럼 표시 설정 */
PT.loadColumnPrefs = function(storageKey) {
  try {
    const raw = localStorage.getItem(storageKey);
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    return (parsed && typeof parsed === 'object') ? parsed : null;
  } catch (e) {
    return null;
  }
};

PT.saveColumnPrefs = function(storageKey, prefs) {
  try {
    localStorage.setItem(storageKey, JSON.stringify(prefs || {}));
  } catch (e) { /* ignore quota */ }
};

PT.applyColumnPrefs = function(api, storageKey) {
  if (!api) return;
  const prefs = PT.loadColumnPrefs(storageKey);
  if (!prefs) return;
  api.columns().every(function() {
    const col = this;
    const settings = col.settings()[0].aoColumns[col.index()];
    const name = settings.name || settings.data;
    if (!name || prefs[name] === undefined) return;
    col.visible(!!prefs[name], false);
  });
  api.columns.adjust();
};

/**
 * DataTables 컬럼 표시/숨김 드롭다운.
 * opts: { api, storageKey, containerSelector, lockedNames: string[] }
 */
PT.mountColumnPicker = function(opts) {
  opts = opts || {};
  const api = opts.api;
  const storageKey = opts.storageKey;
  const $host = $(opts.containerSelector);
  if (!api || !$host.length || !storageKey) return;

  const locked = {};
  (opts.lockedNames || []).forEach(function(n) { locked[n] = true; });

  const t = (typeof window.T === 'function') ? window.T : function(k) { return k; };
  const uid = 'colpick-' + String(storageKey).replace(/[^a-zA-Z0-9_-]/g, '_');

  let html = '<div class="col-picker dropdown">' +
    '<button class="btn btn-sm btn-outline-secondary dropdown-toggle" type="button" id="' + uid +
    '" data-bs-toggle="dropdown" data-bs-auto-close="outside" aria-expanded="false" aria-haspopup="true">' +
    '<i class="bi bi-layout-three-columns" aria-hidden="true"></i> ' + t('col_picker_btn') +
    '</button>' +
    '<div class="dropdown-menu dropdown-menu-end col-picker-menu p-2" aria-labelledby="' + uid + '">' +
    '<div class="small text-muted mb-2 px-1">' + t('col_picker_hint') + '</div>';

  api.columns().every(function() {
    const col = this;
    const idx = col.index();
    const settings = col.settings()[0].aoColumns[idx];
    const name = settings.name || settings.data;
    if (!name || name === 'MainIssues') return;
    const title = $('<div>').html(settings.sTitle || name).text() || String(name);
    const isLocked = !!locked[name];
    const checked = col.visible() ? ' checked' : '';
    const disabled = isLocked ? ' disabled' : '';
    html += '<label class="col-picker-item' + (isLocked ? ' is-locked' : '') + '">' +
      '<input type="checkbox" class="form-check-input me-2" data-col-name="' +
      String(name).replace(/"/g, '&quot;') + '"' + checked + disabled + '>' +
      '<span>' + $('<div>').text(title).html() +
      (isLocked ? ' <span class="text-muted">(' + t('col_picker_locked') + ')</span>' : '') +
      '</span></label>';
  });

  html += '<div class="border-top mt-2 pt-2 d-flex gap-2">' +
    '<button type="button" class="btn btn-sm btn-outline-secondary flex-fill col-picker-reset">' +
    t('col_picker_reset') + '</button></div></div></div>';

  $host.html(html);

  $host.off('change.colpick').on('change.colpick', 'input[data-col-name]', function() {
    const name = $(this).data('col-name');
    if (locked[name]) {
      $(this).prop('checked', true);
      return;
    }
    const show = $(this).is(':checked');
    api.columns().every(function() {
      const settings = this.settings()[0].aoColumns[this.index()];
      const n = settings.name || settings.data;
      if (n === name) this.visible(show);
    });
    const prefs = PT.loadColumnPrefs(storageKey) || {};
    prefs[name] = show;
    PT.saveColumnPrefs(storageKey, prefs);
  });

  $host.off('click.colpickReset').on('click.colpickReset', '.col-picker-reset', function(e) {
    e.preventDefault();
    e.stopPropagation();
    try { localStorage.removeItem(storageKey); } catch (err) { /* ignore */ }
    api.columns().every(function() {
      const settings = this.settings()[0].aoColumns[this.index()];
      const name = settings.name || settings.data;
      if (!name || name === 'MainIssues') {
        this.visible(false, false);
        return;
      }
      // 기본: 초기 columns.visible 값 복원 불가 → locked 제외 전부 표시, 숨김 기본 컬럼은 이름 규칙
      const defaultHidden = {
        FirstDesignAt: true, LastDesignAt: true, DesignDays: true, MainName: true, MainIssues: true
      };
      this.visible(!defaultHidden[name], false);
    });
    api.columns.adjust().draw(false);
    PT.mountColumnPicker(opts);
  });
};

/** 접근성: 색 + 아이콘 + 텍스트 상태 배지 */
PT.resultBadge = function(result) {
  const t = (typeof window.T === 'function') ? window.T : function(k) { return k; };
  const isFail = String(result || '').toUpperCase() === 'FAIL';
  if (isFail) {
    return '<span class="status-badge fail" role="status">' +
      '<i class="bi bi-x-circle-fill" aria-hidden="true"></i>' +
      '<span>' + t('label_fail') + '</span></span>';
  }
  return '<span class="status-badge pass" role="status">' +
    '<i class="bi bi-check-circle-fill" aria-hidden="true"></i>' +
    '<span>' + t('label_pass') + '</span></span>';
};

PT.progressStatusBadges = function(row, escFn, tFn) {
  const esc = escFn || function(s) { return String(s == null ? '' : s); };
  const t = tFn || ((typeof window.T === 'function') ? window.T : function(k) { return k; });
  let html = '<span class="status-stack">';
  if (row && row.IsInProgress) {
    html += '<span class="status-badge active" role="status">' +
      '<i class="bi bi-play-circle-fill" aria-hidden="true"></i>' +
      '<span>' + t('progress_fresh_yes') + '</span></span>';
  } else {
    html += '<span class="status-badge idle" role="status">' +
      '<i class="bi bi-pause-circle-fill" aria-hidden="true"></i>' +
      '<span>' + t('progress_fresh_no') + '</span></span>';
  }
  if (row && row.Overrun) {
    html += '<span class="status-badge over" role="status">' +
      '<i class="bi bi-exclamation-triangle-fill" aria-hidden="true"></i>' +
      '<span>Over</span></span>';
  }
  if (row && row.LowConfidence) {
    html += '<span class="status-badge lowconf" role="status" title="' + esc(t('progress_low_conf_hint')) + '">' +
      '<i class="bi bi-question-circle-fill" aria-hidden="true"></i>' +
      '<span>' + t('progress_low_conf_short') + '</span></span>';
  }
  html += '</span>';
  return html;
};

PT.computeDodBadge = function(history) {
    if (!history || history.length < 2) return '';
    const cur = history[history.length - 1];
    const prev = history[history.length - 2];
    const curQp = (cur.AvgPassRate != null && !isNaN(cur.AvgPassRate))
      ? PT.clampPercent(cur.AvgPassRate)
      : PT.clampPercent(cur.RatePoc);
    const prevQp = (prev.AvgPassRate != null && !isNaN(prev.AvgPassRate))
      ? PT.clampPercent(prev.AvgPassRate)
      : PT.clampPercent(prev.RatePoc);
    if (curQp == null || prevQp == null) return '';
    const delta = Number(curQp) - Number(prevQp);
    const cls = delta > 0 ? 'text-success' : (delta < 0 ? 'text-warning' : 'text-white-50');
    const sign = delta > 0 ? '+' : '';
    return `<span class="${cls}">${window.T('lbl_diff')}: ${sign}${delta.toFixed(1)}%p</span>`;
}

/** 0~100% 클램프 */
PT.clampPercent = function (v) {
  const n = Number(v);
  if (v == null || isNaN(n)) return null;
  return Math.max(0, Math.min(100, n));
};

/**
 * Quality Point = (POC Pass% + Hookup Pass%) / 2  (Hookup 없으면 POC Pass%만)
 * 절대 100을 넘지 않음.
 */
PT.computeQualityPoint = function (opts) {
  opts = opts || {};
  if (opts.avgPassRate != null && !isNaN(Number(opts.avgPassRate))) {
    return PT.clampPercent(opts.avgPassRate);
  }
  const poc = PT.clampPercent(opts.pocPassRate);
  if (poc == null) return 0;
  if (opts.hasHookup && opts.hookupPassRate != null && !isNaN(Number(opts.hookupPassRate))) {
    const hu = PT.clampPercent(opts.hookupPassRate);
    return PT.clampPercent((poc + hu) / 2);
  }
  return poc;
};

/**
 * 이슈 count건을 POC FAIL→PASS로 가정했을 때 QP 상승분(%p).
 */
PT.estimateQpGain = function (issueCount, totalPoc, passPoc, hookupPassRate, hasHookup) {
  const total = Number(totalPoc) || 0;
  const pass = Number(passPoc) || 0;
  if (total <= 0) return 0;
  const fail = Math.max(0, total - pass);
  const fixed = Math.min(Math.max(0, Number(issueCount) || 0), fail);
  const curQp = PT.computeQualityPoint({
    pocPassRate: (pass / total) * 100,
    hookupPassRate: hookupPassRate,
    hasHookup: !!hasHookup
  });
  const newQp = PT.computeQualityPoint({
    pocPassRate: ((pass + fixed) / total) * 100,
    hookupPassRate: hookupPassRate,
    hasHookup: !!hasHookup
  });
  return Math.max(0, +(newQp - curQp).toFixed(2));
};

/** Summary QP 카드 우측: 텍스트 없는 미니 추세 곡선 */
PT.renderQpSparkline = function (domId, values) {
  const el = document.getElementById(domId);
  if (!el) return;
  const vals = (values || []).map(Number).filter(function (v) { return !isNaN(v); });
  if (vals.length < 2) {
    el.innerHTML = '<span class="qp-spark-empty"></span>';
    return;
  }
  if (typeof echarts !== 'undefined') {
    const chart = echarts.init(el);
    if (PT.state && PT.state.charts) PT.state.charts.push(chart);
    chart.setOption({
      animation: false,
      grid: { left: 2, right: 2, top: 4, bottom: 4 },
      xAxis: { type: 'category', show: false, data: vals.map(function (_, i) { return i; }) },
      yAxis: { type: 'value', show: false, min: Math.min.apply(null, vals) - 1, max: Math.max.apply(null, vals) + 1 },
      series: [{
        type: 'line',
        data: vals,
        smooth: true,
        symbol: 'none',
        lineStyle: { width: 2, color: '#0071e3' },
        areaStyle: {
          color: {
            type: 'linear', x: 0, y: 0, x2: 0, y2: 1,
            colorStops: [
              { offset: 0, color: 'rgba(0,113,227,0.35)' },
              { offset: 1, color: 'rgba(0,113,227,0.02)' }
            ]
          }
        }
      }]
    });
    return;
  }
  // echarts 없을 때 SVG fallback
  const w = 88, h = 36, pad = 2;
  const min = Math.min.apply(null, vals), max = Math.max.apply(null, vals);
  const span = Math.max(0.01, max - min);
  const pts = vals.map(function (v, i) {
    const x = pad + (i / (vals.length - 1)) * (w - pad * 2);
    const y = h - pad - ((v - min) / span) * (h - pad * 2);
    return x.toFixed(1) + ',' + y.toFixed(1);
  }).join(' ');
  el.innerHTML = '<svg width="' + w + '" height="' + h + '" viewBox="0 0 ' + w + ' ' + h +
    '" aria-hidden="true"><polyline fill="none" stroke="#4361ee" stroke-width="2" points="' + pts + '"/></svg>';
};

PT.buildMainNameIndex = function(eqList) {
    const map = {};
    (eqList || []).forEach(row => {
        const key = (row.MainName || '').trim();
        if (!key) return;
        if (!map[key]) map[key] = [];
        map[key].push(row);
    });
    return map;
}


// ==========================================
// 1. Data Aggregation
// ==========================================
PT.aggregateData = function(rawData) {
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

  if (window.echarts && !echarts.__pocTheme) {
    const axis = {
      axisLine: { lineStyle: { color: 'rgba(255,255,255,0.28)' } },
      axisTick: { lineStyle: { color: 'rgba(255,255,255,0.28)' } },
      axisLabel: { color: '#c8c8cc' },
      splitLine: { lineStyle: { color: 'rgba(255,255,255,0.08)' } },
      nameTextStyle: { color: '#98989d' }
    };
    echarts.registerTheme('poc-dark', {
      textStyle: { color: '#c8c8cc' },
      title: { textStyle: { color: '#f5f5f7' }, subtextStyle: { color: '#98989d' } },
      legend: { textStyle: { color: '#c8c8cc' } },
      tooltip: {
        backgroundColor: '#1c1c1e',
        borderColor: 'rgba(255,255,255,0.12)',
        textStyle: { color: '#f5f5f7' }
      },
      categoryAxis: axis,
      valueAxis: axis,
      timeAxis: axis,
      logAxis: axis
    });
    const origInit = echarts.init.bind(echarts);
    echarts.init = function (dom, theme, opts) {
      const dark = document.documentElement.getAttribute('data-theme') === 'dark';
      return origInit(dom, theme || (dark ? 'poc-dark' : undefined), opts);
    };
    echarts.__pocTheme = true;
  }

  // Backward-compat aliases during transition
  window.getLineName = PT.getLineName;
  window.formatNum = PT.formatNum;
  window.LINE_ALIAS = PT.LINE_ALIAS;
  window.rebuildLineNav = PT.rebuildLineNav;
  window.setHeaderMeta = PT.setHeaderMeta;
  window.computeDodBadge = PT.computeDodBadge;
  window.buildMainNameIndex = PT.buildMainNameIndex;
  window.aggregateData = PT.aggregateData;
  Object.defineProperty(window, 'charts', {
    get: function () { return PT.state.charts; },
    set: function (v) { PT.state.charts = v; },
    configurable: true
  });
  Object.defineProperty(window, 'dataTableInstance', {
    get: function () { return PT.state.dataTableInstance; },
    set: function (v) { PT.state.dataTableInstance = v; },
    configurable: true
  });
  Object.defineProperty(window, 'mainNameIndex', {
    get: function () { return PT.state.mainNameIndex; },
    set: function (v) { PT.state.mainNameIndex = v; },
    configurable: true
  });
  Object.defineProperty(window, 'lineChartInstance', {
    get: function () { return PT.state.lineChartInstance; },
    set: function (v) { PT.state.lineChartInstance = v; },
    configurable: true
  });
})(window.PocTrace);