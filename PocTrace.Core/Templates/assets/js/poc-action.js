'use strict';
/**
 * Top Issue → Owner → Excel / 5D memo 원클릭 조치 루프
 * + 이슈 해결 시 Quality Point 개선 예측
 */
window.PocTrace = window.PocTrace || {};
(function (PT) {
  function t(key) {
    return (typeof window.T === 'function') ? window.T(key) : key;
  }

  function flashBtn($btn, okText) {
    const prev = $btn.html();
    $btn.prop('disabled', true).html(okText);
    setTimeout(function () {
      $btn.prop('disabled', false).html(prev);
    }, 1600);
  }

  PT.filterGridByIssue = function (issueName) {
    const api = PT.state && PT.state.dataTableInstance;
    if (!api) return;
    try { api.column(3).search('FAIL'); } catch (e) { /* ignore */ }
    api.search(issueName || '').draw();
    $('#result-filter-group button').removeClass('active');
    $('#result-filter-group button[data-filter="FAIL"]').addClass('active');
    if (typeof PT.scrollToNode === 'function') PT.scrollToNode('#mainTable');
  };

  PT.copyFiveDMemo = function (issueName, count, owner, $btn) {
    const dbName = (typeof REPORT_META !== 'undefined' && REPORT_META.dbName)
      ? REPORT_META.dbName
      : '';
    const line = PT.getLineName(dbName);
    const excel = dbName ? ('./Excel/' + dbName + '_Error_Detail.xlsx') : './Excel/_Error_Detail.xlsx';
    const text = '[PocTrace] Issue=' + issueName +
      '; Count=' + count +
      '; Owner=' + (owner || '-') +
      '; Line=' + line +
      '; Excel=' + excel +
      '; Action=5D Upload';

    function fallbackCopy(s) {
      const ta = document.createElement('textarea');
      ta.value = s;
      ta.style.position = 'fixed';
      ta.style.left = '-9999px';
      document.body.appendChild(ta);
      ta.select();
      try { document.execCommand('copy'); } catch (e) { /* ignore */ }
      document.body.removeChild(ta);
    }

    const done = function () {
      if ($btn && $btn.length) flashBtn($btn, t('btn_action_copied'));
    };

    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(done).catch(function () {
        fallbackCopy(text);
        done();
      });
    } else {
      fallbackCopy(text);
      done();
    }
  };

  PT.renderActionLoop = function (containerSelector, dto, meta) {
    const $el = $(containerSelector);
    if (!$el.length) return;

    const errorStats = (dto && dto.ErrorStats) ? dto.ErrorStats : {};
    const belongStats = (dto && dto.BelongStats) ? dto.BelongStats : {};
    const dbName = (meta && meta.dbName) ? meta.dbName : '';

    const eqList = (dto && dto.Equipments) ? dto.Equipments : [];
    const totalPoc = eqList.reduce(function (s, e) { return s + (e.TotalPoc || 0); }, 0);
    const passPoc = eqList.reduce(function (s, e) { return s + (e.PassPoc || 0); }, 0);
    const hookup = (dto && dto.HookupStats) ? dto.HookupStats : {};
    const hookupTotal = hookup.TotalObjects || 0;
    const hookupOk = hookup.OK || 0;
    const hasHookup = hookupTotal > 0;
    const hookupPassRate = hasHookup ? (hookupOk / hookupTotal) * 100 : 0;
    const curQp = (window._pocTraceQpContext && window._pocTraceQpContext.qualityPoint != null)
      ? window._pocTraceQpContext.qualityPoint
      : PT.computeQualityPoint({
          pocPassRate: totalPoc > 0 ? (passPoc / totalPoc) * 100 : 0,
          hookupPassRate: hookupPassRate,
          hasHookup: hasHookup
        });

    let primaryBelong = '-';
    let maxBelong = -1;
    Object.keys(belongStats).forEach(function (k) {
      const c = Number(belongStats[k]) || 0;
      if (c > maxBelong) {
        maxBelong = c;
        primaryBelong = k;
      }
    });

    const belongSummary = Object.keys(belongStats)
      .map(function (k) { return k + ' ' + PT.formatNum(belongStats[k]); })
      .join(' · ') || '-';

    const top8 = Object.keys(errorStats)
      .map(function (k) { return { name: k, count: Number(errorStats[k]) || 0 }; })
      .sort(function (a, b) { return b.count - a.count; })
      .slice(0, 8);

    let rowsHtml = '';
    if (!top8.length) {
      rowsHtml = '<tr><td colspan="6" class="text-muted text-center py-3">' + t('action_no_issues') + '</td></tr>';
    } else {
      top8.forEach(function (item, idx) {
        const safeName = String(item.name).replace(/"/g, '&quot;');
        const gain = PT.estimateQpGain(item.count, totalPoc, passPoc, hookupPassRate, hasHookup);
        const projected = PT.clampPercent(curQp + gain);
        rowsHtml +=
          '<tr>' +
          '<td class="text-muted">' + (idx + 1) + '</td>' +
          '<td class="fw-semibold">' + $('<div>').text(item.name).html() + '</td>' +
          '<td class="text-end">' + PT.formatNum(item.count) + '</td>' +
          '<td class="small">' + $('<div>').text(primaryBelong).html() + '</td>' +
          '<td class="text-end text-success fw-bold" title="' + t('action_qp_hint') + '">' +
          '+' + gain.toFixed(2) + '%p' +
          '<div class="small text-muted fw-normal">' + projected.toFixed(1) + '%</div></td>' +
          '<td class="text-nowrap">' +
          '<button type="button" class="btn btn-sm btn-outline-primary me-1 btn-action-filter" data-issue="' + safeName + '" title="' + t('btn_action_filter') + '">' +
          '<i class="bi bi-funnel" aria-hidden="true"></i> ' + t('btn_action_filter') + '</button>' +
          '</td>' +
          '</tr>';
      });
    }

    const html =
      '<div class="card shadow-sm border-primary border-opacity-25">' +
      '<div class="card-header bg-white d-flex flex-wrap justify-content-between align-items-center gap-2 py-2">' +
      '<span class="small text-muted">' + t('action_loop_hint') + '</span>' +
      '<span class="badge bg-light text-dark border">' + t('action_owner_hint') + ': ' +
      $('<div>').text(primaryBelong).html() +
      ' · QP ' + Number(curQp).toFixed(1) + '%</span>' +
      '</div>' +
      '<div class="card-body p-0">' +
      '<div class="table-responsive">' +
      '<table class="table table-sm table-hover mb-0 align-middle">' +
      '<thead class="table-light"><tr>' +
      '<th style="width:3rem">#</th>' +
      '<th>' + t('action_col_issue') + '</th>' +
      '<th class="text-end" style="width:6rem">' + t('action_col_count') + '</th>' +
      '<th style="width:8rem">' + t('action_col_owner') + '</th>' +
      '<th class="text-end" style="width:7rem">' + t('action_col_qp_gain') + '</th>' +
      '<th style="width:16rem">' + t('action_col_actions') + '</th>' +
      '</tr></thead>' +
      '<tbody>' + rowsHtml + '</tbody>' +
      '</table></div></div>' +
      '<div class="card-footer bg-white small text-muted">' +
      t('action_belong_line') + ': ' + $('<div>').text(belongSummary).html() +
      ' · ' + t('action_qp_hint') +
      '</div></div>';

    $el.html(html);

    $el.off('click.actionLoop');
    $el.on('click.actionLoop', '.btn-action-filter', function () {
      PT.filterGridByIssue($(this).data('issue'));
    });
  };

  window.filterGridByIssue = PT.filterGridByIssue;
  window.copyFiveDMemo = PT.copyFiveDMemo;
  window.renderActionLoop = PT.renderActionLoop;
})(window.PocTrace);
