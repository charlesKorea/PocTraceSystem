/* DIQS Core: Grid System (DataTables) - Typo Fixed */

function initGrid(tableSelector, data, toolbarSelector) {
    // 1. 데이터 유효성 검사
    if (!data || data.length === 0) {
        if ($.fn.DataTable.isDataTable(tableSelector)) $(tableSelector).DataTable().destroy();
        $(tableSelector).html('<thead><tr><th>No Data</th></tr></thead><tbody><tr><td>데이터가 없습니다.</td></tr></tbody>');
        return;
    }

    var cols = [];
    var colNames = [];

    // 2. 컬럼 정의
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

    Object.keys(data[0]).forEach(k => {
        if (!k.startsWith('_')) {
            cols.push({ title: k, data: k });
            colNames.push({ id: k, text: k });
        }
    });

    // 3. DataTables 초기화
    if ($.fn.DataTable.isDataTable(tableSelector)) $(tableSelector).DataTable().destroy();
    $(tableSelector).empty();

    var dt = $(tableSelector).DataTable({
        data: data, columns: cols, dom: 'rtip',
        scrollX: true, pageLength: 15, deferRender: true, searching: true,
        createdRow: function(row, d) {
            $(row).on('click', function(e) { if (e.altKey && typeof showLineage === 'function') showLineage(d); });
        }
    });

    // 4. 툴바 생성
    if (toolbarSelector) {
        var $container = $(toolbarSelector);
        if ($container.length === 0) {
            // Self-Healing
            $container = $(`<div id="${toolbarSelector.replace('#','')}" class="d-flex align-items-center gap-2"></div>`);
            $(tableSelector).closest('.card-body').parent().find('.card-header').append($container);
        }
        $container.empty();

        // A. Buttons
        new $.fn.dataTable.Buttons(dt, {
            buttons: [
                { extend: 'copy', className: 'btn btn-sm btn-outline-secondary' },
                { extend: 'excel', className: 'btn btn-sm btn-success', text: '<i class="bi bi-file-excel"></i> Excel' }
            ]
        }).container().appendTo($container);

        // B. Search UI (Variable Name: uid)
        var uid = Date.now() + Math.floor(Math.random() * 1000);
        var searchHtml = `
            <div class="input-group input-group-sm ms-2" style="width: 450px;">
                <select class="form-select bg-light search-col" id="sc-${uid}" style="max-width: 120px;">
                    <option value="all">All Columns</option>
                </select>
                <input type="text" class="form-control border-start-0 search-input" id="si-${uid}" placeholder="검색어 (즉시 반응)">
                <div class="input-group-text bg-white">
                    <input class="form-check-input mt-0 search-exact" id="se-${uid}" type="checkbox">
                    <label class="form-check-label small ms-1">일치</label>
                </div>
            </div>
            <span class="badge bg-secondary ms-2 search-count" id="cnt-${uid}">${data.length.toLocaleString()}건</span>
        `;
        $container.append(searchHtml);

        // C. Element Reference
        var $sel = $(`#sc-${uid}`);
        var $input = $(`#si-${uid}`);
        var $exact = $(`#se-${uid}`);
        var $count = $(`#cnt-${uid}`); // [Fix] uiId -> uid 오타 수정 완료

        // D. Populate Data
        if(colNames.length > 0) {
            colNames.forEach(c => $sel.append(new Option(c.text, c.id)));
        }

        // E. Event Binding
        function triggerSearch() {
            dt.draw();
            $count.text(dt.rows({ filter: 'applied' }).count().toLocaleString() + "건");
        }
        $input.on('input', triggerSearch);
        $sel.on('change', triggerSearch);
        $exact.on('change', triggerSearch);

        // F. Custom Filter
        $.fn.dataTable.ext.search.push(function(settings, searchData, index, rowData) {
            if (settings.nTable.id !== $(tableSelector).attr('id')) return true;
            
            var raw = settings.aoData[index]._aData; 
            if (!raw) return true;

            var colKey = $sel.val();
            var term = ($input.val() || '').toLowerCase();
            var isExact = $exact.is(':checked');

            if (!term) return true;

            if (colKey === 'all') {
                return Object.values(raw).some(val => {
                    var s = String(val || '').toLowerCase();
                    if (s.includes('<')) s = s.replace(/<[^>]*>?/gm, '');
                    return isExact ? s === term : s.includes(term);
                });
            } else {
                var s = String(raw[colKey] || '').toLowerCase();
                if (s.includes('<')) s = s.replace(/<[^>]*>?/gm, '');
                return isExact ? s === term : s.includes(term);
            }
        });
    }

    if (tableSelector === '#univTable') table = dt;
    else drillTable = dt;
}