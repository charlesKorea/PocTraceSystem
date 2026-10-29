'use strict';
window.PocTrace = window.PocTrace || {};
(function (PT) {
  const _ = window._;
  const T = window.T;
  let currentModalPocs = [];

PT.initGrid = function(eqList, dbName) {
    if (PT.state.dataTableInstance) { PT.state.dataTableInstance.destroy(); $('#mainTable').empty(); }

    const excelLink = `./Excel/${dbName}_Error_Detail.xlsx`;
    const failCount = (eqList || []).filter(e => e.Result === 'FAIL').length;
    const colKey = 'poc_cols_detail_' + String(dbName || 'line');

    PT.state.dataTableInstance = $('#mainTable').DataTable({
        data: eqList,
        dom: '<"dt-top-container" <"dt-left-group" <"dt-custom-title"> l <"dt-result-filter"> > <"dt-right-group" f <"dt-col-picker"> <"btn-excel-wrapper"> > >rtip',
        deferRender: true,
        
        initComplete: function() {
            // initComplete 시점에는 outer `PT.state.dataTableInstance` 대입이 아직 안 됐을 수 있음 → this.api() 사용
            const api = this.api();
            PT.state.dataTableInstance = api;

            if (typeof PT.applyColumnPrefs === 'function') {
                PT.applyColumnPrefs(api, colKey);
            }

            $("div.dt-custom-title").html(
                `<h5 class="m-0 fw-bold text-dark"><i class="bi bi-table" aria-hidden="true"></i> ${T('header_detail')}</h5>`
            );

            const passCount = (eqList || []).filter(e => e.Result === 'PASS').length;
            const allCount = (eqList || []).length;
            $("div.dt-result-filter").html(
                '<div class="filter-chips" id="result-filter-group" role="group" aria-label="' + T('header_res') + '">' +
                '<button type="button" class="filter-chip fail' + (failCount > 0 ? ' active' : '') + '" data-filter="FAIL">' + T('filter_fail') + ' ' + failCount + '</button>' +
                '<button type="button" class="filter-chip' + (failCount > 0 ? '' : ' active') + '" data-filter="ALL">' + T('filter_all') + ' ' + allCount + '</button>' +
                '<button type="button" class="filter-chip pass" data-filter="PASS">' + T('filter_pass') + ' ' + passCount + '</button>' +
                '</div>'
            );

            $("div.btn-excel-wrapper").html(`
                <a href="${excelLink}" class="btn btn-success btn-sm fw-bold">
                    <i class="bi bi-file-earmark-excel" aria-hidden="true"></i> ${T('btn_export')}
                </a>
            `);

            if (typeof PT.mountColumnPicker === 'function') {
                PT.mountColumnPicker({
                    api: api,
                    storageKey: colKey,
                    containerSelector: 'div.dt-col-picker',
                    lockedNames: ['Tag', 'Actions']
                });
            }

            // 기본: FAIL만 표시 (조치 루프) — 컬럼 prefs와 무관
            if (failCount > 0) {
                api.column(3).search('FAIL').draw();
            }

            $('#result-filter-group').off('click').on('click', 'button', function() {
                const mode = $(this).data('filter');
                $('#result-filter-group button').removeClass('active');
                $(this).addClass('active');
                api.search('');
                api.column(3).search(mode === 'ALL' ? '' : String(mode)).draw();
            });
        },
        
        columns: [
            { name: 'Tag', title: T('header_eqp'), data: "Tag", render: d => `<span class="fw-bold">${d}</span>` },
            { name: 'ConstCode', title: T('header_cc'), data: "ConstCode", className: "text-center" },
            { name: 'Process', title: T('header_proc'), data: "Process" },
            {
                name: 'Result', title: T('header_res'), data: "Result",
                render: function (d, type) {
                    if (type !== 'display') return d || '';
                    return (typeof PT.resultBadge === 'function') ? PT.resultBadge(d) : (d || '');
                }
            },
            { name: 'EqpQty', title: T('th_eq_qty'), data: "EqpQty", className: "text-center", render: PT.formatNum },
            { name: 'TotalPoc', title: T('header_poc'), data: "TotalPoc", className: "text-center", render: PT.formatNum },
            { name: 'IssueCount', title: T('header_issue'), data: "IssueCount", className: "text-center", render: (d, t, r) => d > 0 ? `<span class="text-danger fw-bold">${PT.formatNum(d)}</span>` : PT.formatNum(d) },
            { 
                name: 'PocRate',
                title: T('header_poc_rate') || 'POC Rate', 
                data: null, 
                className: "text-center",
                render: (data, type, row) => {
                    const rate = row.TotalPoc > 0 ? ((row.PassPoc / row.TotalPoc) * 100).toFixed(1) : 0;
                    const colorClass = rate < 100 ? 'text-danger' : 'text-primary';
                    return `<span class="${colorClass} fw-bold">${rate}%</span>`;
                }
            },
            { name: 'MainIssues', data: "MainIssues", visible: false }, 
            {
                name: 'Actions',
                title: T('header_detail'), data: null, className: "text-center text-nowrap", width: "180px",
                orderable: false,
                render: (data, type, row) => {
                    let html = `<div class="btn-group btn-group-sm action-btn-group">`;
                    if (row.Result === 'FAIL' || row.IssueCount > 0) {
                        html += `<button type="button" class="btn btn-detail-view view-error-btn" data-tag="${row.Tag}" title="${T('title_view_detail')}">` +
                            `<i class="bi bi-search" aria-hidden="true"></i><span class="action-label"> ${T('btn_action_detail')}</span></button>`;
                    } else {
                        html += `<button type="button" class="btn btn-light disabled" title="${T('title_view_detail')}">` +
                            `<i class="bi bi-search" aria-hidden="true"></i></button>`;
                    }
                    html += `<button type="button" class="btn btn-info text-white view-sim-btn" data-tag="${row.Tag}" title="${T('title_compare_similar')}">` +
                        `<i class="bi bi-diagram-3" aria-hidden="true"></i><span class="action-label"> ${T('btn_action_similar')}</span></button>`;
                    html += `</div>`;
                    return html;
                }
            }
        ],
        order: [[3, 'asc'], [6, 'desc']]
    });

    $('#mainTable tbody').off('click');
    $('#mainTable tbody').on('click', '.view-error-btn', function() {
        const tag = $(this).data('tag');
        const rowData = PT.state.dataTableInstance.rows().data().toArray().find(r => r.Tag === tag);
        openLiteModal(rowData, excelLink);
    });
    $('#mainTable tbody').on('click', '.view-sim-btn', function() {
        const tag = $(this).data('tag');
        PT.analyzeSimilarModels(tag);
    });
}

// ==========================================
// 6. Lite Modal (Raw Data ?�음)
// ==========================================
PT.openLiteModal = function(rowData, excelLink) {
    if (!rowData) return;
    const title = `${rowData.Tag} <span class="text-muted fs-6">(${rowData.ConstCode || 'No Code'})</span>`;
    const issuesLabel = T('lbl_issues_found');
    const bodyHtml = `
        <div class="text-center mb-4">
            <h4 class="fw-bold text-dark">${title}</h4>
            <div class="text-primary fw-bold mb-2">${rowData.MainName || ''}</div>
            <div class="badge bg-danger fs-6 mb-2">${rowData.IssueCount} ${issuesLabel}</div>
            <p class="text-muted small">${T('modal_detail_hint')}</p>
        </div>

        <div class="card bg-light mb-3">
            <div class="card-body">
                <h6 class="fw-bold border-bottom pb-2 mb-2">${T('modal_main_issues')}</h6>
                <p class="text-danger fw-bold" style="white-space: pre-wrap;">${rowData.MainIssues || '-'}</p>
            </div>
        </div>

        <div class="d-grid gap-2">
            <a href="${excelLink}" class="btn btn-success btn-lg shadow-sm">
                <i class="bi bi-file-earmark-excel-fill" aria-hidden="true"></i> ${T('btn_download_excel_full')}
            </a>
            <button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">${T('btn_close')}</button>
        </div>
    `;

    $('#detailModal .modal-title').html(`<i class="bi bi-search" aria-hidden="true"></i> ${T('title_view_detail')}`);
    $('#detailModal .modal-body').html(bodyHtml);
    new bootstrap.Modal(document.getElementById('detailModal')).show();
}


// ==========================================
// 6. Modal (Expanded Columns)
// ==========================================
PT.openErrorModal = function(guid) {
    // guid??First Poc??StartEquipmentGuid ?��?�? ?�본 ?�이?�에???�당 GUID�?가�?모든 POC�?찾아????
    // aggregateData?�서 'RawPocs'�??�?�해?�?��?�?그것???�용
    const rowData = PT.state.dataTableInstance.rows().data().toArray().find(r => r.Guid === guid);
    if (!rowData) return;

    // FailCount > 0 ??것만
    const targetPocs = rowData.RawPocs.filter(p => p.FailCount > 0); 
    currentModalPocs = targetPocs;

    // [Fix 3] Modal Header & Table Structure
    const bodyHtml = `
        <div class="d-flex justify-content-between mb-3 align-items-center">
            <div>
                <h6 class="text-dark fw-bold mb-0">${rowData.Tag} (${rowData.Name})</h6>
                <small class="text-muted">Found ${PT.formatNum(targetPocs.length)} Issues</small>
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

PT.exportModalData = function(tagName) {
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

  window.initGrid = PT.initGrid;
  window.openLiteModal = PT.openLiteModal;
  window.openErrorModal = PT.openErrorModal;
  window.exportModalData = PT.exportModalData;
})(window.PocTrace);