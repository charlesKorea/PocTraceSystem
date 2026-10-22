'use strict';
window.PocTrace = window.PocTrace || {};
(function (PT) {
  const _ = window._;
  const T = window.T;

PT.analyzeSimilarModels = function(targetTag) {
    console.log(`[SimAnalysis] Starting analysis for: ${targetTag}`);
    const allData = PT.state.dataTableInstance.rows().data().toArray();
    const targetData = allData.find(d => d.Tag === targetTag);
    if (!targetData) {
        console.error("[SimAnalysis] Target data not found.");
        return;
    }

    // Similar models = exact same MainName (메인 설비명 완전 일치). Tag/ConstCode prefix 매칭 폐기.
    const targetName = (targetData.MainName || '').trim();
    if (!targetName) {
        alert(`${T('lbl_similar_model')}: ${T('alert_no_similar_name')}\n(MainName empty)`);
        return;
    }

    const similarGroup = (PT.state.mainNameIndex && PT.state.mainNameIndex[targetName]
        ? PT.state.mainNameIndex[targetName]
        : allData.filter(d => (d.MainName || '').trim() === targetName)
    ).filter(d => d.Tag !== targetTag);

    if (similarGroup.length === 0) {
        alert(`${T('lbl_similar_model')}: ${T('alert_no_similar_name')}\n(${T('lbl_main_name')}: ${targetName})`);
        return;
    }

    // 2. Average stats over similar group
    const calcAvg = (arr, key) => {
        const sum = arr.reduce((acc, cur) => acc + (cur[key] || 0), 0);
        return (sum / arr.length);
    };
    
    // 비율 평균은 (합 Pass / 합 Total)로 계산해야 정확
    const totalSimPoc = similarGroup.reduce((a,c) => a + c.TotalPoc, 0);
    const totalSimPass = similarGroup.reduce((a,c) => a + c.PassPoc, 0);
    
    const avgStats = {
        Tag: '-',
        ConstCode: '-',
        MainName: targetName,
        EqpQty: calcAvg(similarGroup, 'EqpQty'),
        TotalPoc: calcAvg(similarGroup, 'TotalPoc'),
        IssueCount: calcAvg(similarGroup, 'IssueCount'),
        PassRate: totalSimPoc > 0 ? ((totalSimPass/totalSimPoc)*100) : 0
    };

    // 3. Table Construction
    // [Fix 1] Check Diff: Red Text Only (No Background)
    const checkDiff = (val, baseVal) => val !== baseVal ? 'text-danger fw-bold' : '';
    
    let rowsHtml = '';

    // (A) Target Row (기�?)
    const targetRate = targetData.TotalPoc > 0 ? ((targetData.PassPoc/targetData.TotalPoc)*100).toFixed(1) : 0;
    rowsHtml += `
        <tr class="table-primary border-primary fw-bold">
            <td>${T('lbl_target')}</td>
            <td>${targetData.Tag}</td>
            <td>${targetData.ConstCode}</td>
            <td class="text-start">${targetData.MainName}</td>
            <td>${PT.formatNum(targetData.EqpQty)}</td>
            <td>${PT.formatNum(targetData.TotalPoc)}</td>
            <td>${PT.formatNum(targetData.IssueCount)}</td>
            <td>${targetRate}%</td>
        </tr>
    `;

    // (B) Average Row (?�균)
    rowsHtml += `
        <tr class="table-secondary fw-bold" style="border-bottom: 2px solid #aaa;">
            <td>${T('lbl_avg')} (${similarGroup.length})</td>
            <td>-</td>
            <td>-</td>
            <td>-</td>
            <td>${PT.formatNum(avgStats.EqpQty.toFixed(1))}</td>
            <td>${PT.formatNum(avgStats.TotalPoc.toFixed(1))}</td>
            <td>${PT.formatNum(avgStats.IssueCount.toFixed(1))}</td>
            <td>${avgStats.PassRate.toFixed(1)}%</td>
        </tr>
    `;

    // (C) Similar Rows (비교)
    similarGroup.forEach(d => {
        const rate = d.TotalPoc > 0 ? ((d.PassPoc/d.TotalPoc)*100).toFixed(1) : 0;
        
        // [Fix 3] �?불일�???빨간???�시 (기�? ?�비?� 비교)
        const clsName = checkDiff(d.MainName, targetData.MainName);
        const clsQty = checkDiff(d.EqpQty, targetData.EqpQty);
        const clsPoc = checkDiff(d.TotalPoc, targetData.TotalPoc);

        rowsHtml += `
            <tr>
                <td class="text-muted small">${T('lbl_similar')}</td>
                <td class="fw-bold">${d.Tag}</td>
                <td>${d.ConstCode}</td>
                <td class="text-start ${clsName}">${d.MainName}</td>
                <td class="${clsQty}">${PT.formatNum(d.EqpQty)}</td>
                <td class="${clsPoc}">${PT.formatNum(d.TotalPoc)}</td>
                <td class="${d.IssueCount > 0 ? 'text-danger fw-bold' : ''}">${PT.formatNum(d.IssueCount)}</td>
                <td>${rate}%</td>
            </tr>
        `;
    });

    const bodyHtml = `
        <div class="d-flex justify-content-between align-items-center mb-3">
             <div>
                <span class="badge bg-info text-dark me-2">${T('lbl_main_name')}: ${targetName}</span>
                <span class="badge bg-secondary">${T('lbl_similar')}: ${similarGroup.length}</span>
             </div>
             <button class="btn btn-success btn-sm fw-bold" id="btn-sim-export">
                <i class="bi bi-file-earmark-excel" aria-hidden="true"></i> ${T('btn_excel_sim')}
             </button>
        </div>
        <div class="table-responsive">
            <table class="table table-bordered table-hover text-center align-middle" style="font-size: 0.9rem;">
                <thead class="table-light">
                    <tr>
                        <th width="10%">${T('lbl_type')}</th>
                        <th width="12%">${T('header_eqp')}</th>
                        <th width="12%">${T('header_cc')}</th>
                        <th width="20%">${T('lbl_main_name')}</th>
                        <th width="10%">${T('th_eq_qty')}</th>
                        <th width="10%">${T('header_poc')}</th>
                        <th width="10%">${T('lbl_issue_poc')}</th>
                        <th width="10%">${T('lbl_pass_rate')}</th>
                    </tr>
                </thead>
                <tbody>
                    ${rowsHtml}
                </tbody>
            </table>
        </div>
    `;

    $('#detailModal .modal-title').html(`<i class="bi bi-diagram-3" aria-hidden="true"></i> ${T('lbl_similar_model')}`);
    $('#detailModal .modal-body').html(bodyHtml);
    
    window.simExportData = { 
        target: targetData, 
        avg: avgStats, 
        similar: similarGroup,
        matchKey: targetName
    };
    
    $('#btn-sim-export').off('click').on('click', function() {
        console.log("[SimAnalysis] Export button clicked");
        PT.exportSimExcel();
    });

    new bootstrap.Modal(document.getElementById('detailModal')).show();
    
    window.simPrefix = targetName.replace(/[\\/:*?"<>|]/g, '_');
}

// [Fix 2] Export Function with Debugging
PT.exportSimExcel = function() {
    console.log("[SimAnalysis] Export function started");
    
    try {
        const data = window.simExportData;
        if (!data) {
            console.error("[SimAnalysis] No export data found in window.simExportData");
            alert("No data to export. Please reopen the modal.");
            return;
        }

        const target = data.target;
        const avg = data.avg;
        const sim = data.similar;
        const prefix = data.prefix || "Analysis";

        console.log(`[SimAnalysis] Exporting ${sim.length + 2} rows...`);

        // CSV Header
        let csv = `\uFEFFType,Tag,ConstCode,MainName,EqpQty,TotalPoc,IssuePoc,PassRate\n`;

        // 1. Target
        const tRate = target.TotalPoc > 0 ? ((target.PassPoc/target.TotalPoc)*100).toFixed(1) : 0;
        csv += `TARGET,${target.Tag},${target.ConstCode},"${target.MainName}",${target.EqpQty},${target.TotalPoc},${target.IssueCount},${tRate}%\n`;

        // 2. Average
        csv += `AVERAGE,-,-,-,${avg.EqpQty.toFixed(1)},${avg.TotalPoc.toFixed(1)},${avg.IssueCount.toFixed(1)},${avg.PassRate.toFixed(1)}%\n`;

        // 3. Similar
        sim.forEach(d => {
            const rate = d.TotalPoc > 0 ? ((d.PassPoc/d.TotalPoc)*100).toFixed(1) : 0;
            csv += `SIMILAR,${d.Tag},${d.ConstCode},"${d.MainName}",${d.EqpQty},${d.TotalPoc},${d.IssueCount},${rate}%\n`;
        });

        const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
        const link = document.createElement("a");
        link.href = URL.createObjectURL(blob);
        link.setAttribute("download", `SimAnalysis_${prefix}.csv`);
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        
        console.log("[SimAnalysis] Export successful.");

    } catch (e) {
        console.error("[SimAnalysis] Export failed:", e);
        alert("Export failed. Check console for details.");
    }
}


PT.renderBadge = function(res) {
    return res === 'FAIL' ? '<span class="badge bg-danger">FAIL</span>' : '<span class="badge bg-success">PASS</span>';
}

  window.analyzeSimilarModels = PT.analyzeSimilarModels;
  window.exportSimExcel = PT.exportSimExcel;
  window.renderBadge = PT.renderBadge;
})(window.PocTrace);