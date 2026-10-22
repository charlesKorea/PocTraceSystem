/* DIQS Core: Module 1 - Commons (Variables, Helpers, Filters, Theme, I18n, Auth) */

// [Global State]
var table, drillTable, kpiChart, trendChart, dynChart;
var chartObj = null; 
const PALETTE = ['#5470c6', '#91cc75', '#fac858', '#ee6666', '#73c0de', '#3ba272', '#fc8452', '#9a60b4', '#ea7ccc'];
const SOLID = PALETTE;
window.GLOBAL_DATA = [];
var DT_LANG_CURRENT = {};

// --- [Theme System] ---
(function() {
    const savedTheme = localStorage.getItem('theme') || 'light';
    document.documentElement.setAttribute('data-theme', savedTheme);
})();

function applyTheme(themeName) {
    document.documentElement.setAttribute('data-theme', themeName);
    localStorage.setItem('theme', themeName);
}

// --- [I18n System] ---
function applyGlobalLanguage(lang) {
    if (!lang) lang = localStorage.getItem('lang') || 'ko';
    localStorage.setItem('lang', lang);
    
    var dictData = null;
    if (typeof window.SYSTEM_LANG !== 'undefined') {
        dictData = window.SYSTEM_LANG.Dictionary;
        if (window.SYSTEM_LANG.DataTable && window.SYSTEM_LANG.DataTable[lang]) setGridLanguage(window.SYSTEM_LANG.DataTable[lang]);
    } else if (typeof LANG_DATA !== 'undefined') {
        dictData = LANG_DATA;
        if (typeof DT_LANG_CONFIG !== 'undefined' && DT_LANG_CONFIG[lang]) setGridLanguage(DT_LANG_CONFIG[lang]);
    }

    if (!dictData) { console.warn("[I18n] Data not loaded."); return; }

    $('[data-i18n]').each(function() {
        var key = $(this).data('i18n');
        var textObj = dictData[key];
        if (textObj && textObj[lang]) {
            var text = textObj[lang];
            if ($(this).is('input') || $(this).is('textarea')) $(this).attr('placeholder', text);
            else $(this).text(text);
        }
    });
    console.log(`[I18n] Applied: ${lang}`);
}

function T(key) {
    var lang = localStorage.getItem('lang') || 'ko';
    var dict = null;
    if (typeof window.SYSTEM_LANG !== 'undefined') dict = window.SYSTEM_LANG.Dictionary;
    else if (typeof LANG_DATA !== 'undefined') dict = LANG_DATA;
    if (dict && dict[key] && dict[key][lang]) return dict[key][lang];
    return key;
}

function setGridLanguage(config) { DT_LANG_CURRENT = config; }

// --- [Auth System] (이동됨) ---
function checkLogin() {
    const urlParams = new URLSearchParams(window.location.search);
    if (urlParams.get('auth') === 'master') {
        var admin = { id:'master', name:'Master', company:'System', role:'admin' };
        localStorage.setItem('currentUser', JSON.stringify(admin));
        window.history.replaceState({}, document.title, window.location.pathname);
    }
    var user = JSON.parse(localStorage.getItem('currentUser'));
    if (!user) { 
        $('#login-backdrop').css('display', 'flex'); 
    } else { 
        updateUserUI(user); 
        applyPermissions(user.role); 
    }
}

function updateUserUI(u) {
    $('#user-name').text(u.name); 
    $('#user-company').text(u.company); 
    $('#user-initial').text(u.name ? u.name.charAt(0) : 'U');
}

function applyPermissions(r) {
    if (r !== 'admin') { $('#nav-settings').hide(); $('.admin-only').hide(); }
}

function logout() {
    if(confirm('Log out?')) { localStorage.removeItem('currentUser'); location.reload(); }
}

function tryLogin() {
    var inputId = $('#login-id').val().trim();
    if (typeof window.USER_DB === 'undefined') {
        if(inputId === 'admin') {
            localStorage.setItem('currentUser', JSON.stringify({ id:'admin', name:'Admin', role:'admin' }));
            location.reload();
        } else { $('#login-msg').text('User DB Error'); }
        return;
    }
    var found = window.USER_DB.find(u => u.id === inputId);
    if (found) {
        localStorage.setItem('currentUser', JSON.stringify(found));
        location.reload();
    } else {
        $('#login-msg').text('ID not found');
    }
}

// --- [Layout Init] ---
function initLayout() {
    console.log("[Layout] Initializing...");

    var path = window.location.pathname.split("/").pop() || "index.html";
    var map = {
        'index.html': {id:'nav-dashboard', g:'menu-dash'},
        'equip_dashboard.html': {id:'nav-equip-dash', g:'menu-dash'},
        'nozzle_report.html': {id:'nav-nozzle', g:'menu-integrity'},
        'duct_report.html': {id:'nav-duct', g:'menu-integrity'},
        'eq_obj_report.html': {id:'nav-eqobj', g:'menu-integrity'},
        'vacuum_report.html': {id:'nav-vacuum', g:'menu-integrity'},
        'poc_report.html': {id:'nav-poc', g:'menu-integrity'},
        'search.html': {id:'nav-search', g:'menu-search'},
        'equip_search.html': {id:'nav-equip-search', g:'menu-search'},
        'settings.html': {id:'nav-settings', g:''}
    };
    var t = map[path];
    if(t) {
        var el = document.getElementById(t.id);
        if(el) el.classList.add("active");
        else console.warn("[Layout] Active menu element not found: " + t.id);

        if(t.g && typeof bootstrap !== 'undefined') {
            var groupEl = document.getElementById(t.g);
            // [Fix] 요소 존재 여부 확인 (방어 코드)
            if (groupEl) {
                new bootstrap.Collapse(groupEl, {toggle:false, show:true});
            } else {
                console.warn("[Layout] Sidebar Group not found: " + t.g + ". Is Sidebar Component injected?");
            }
        }
    }

    applyGlobalLanguage();
    checkLogin(); // 이제 여기서 호출해도 문제 없음

    // 로그인 이벤트
    $('#btn-login').off('click').on('click', tryLogin);
    $('#login-id').off('keyup').on('keyup', function(e){ if(e.keyCode==13) tryLogin(); });
}


// --- [Helpers] ---
window.animateValue = function(id, start, end, duration) {
    var obj = document.getElementById(id); if(!obj) return;
    var range = end - start; var current = start; var increment = end > start ? 1 : -1;
    var stepTime = Math.abs(Math.floor(duration / range));
    if (range == 0) { obj.innerHTML = end.toLocaleString(); return; }
    if (stepTime < 10) { stepTime = 10; increment = Math.ceil(range / (duration / 10)); }
    var timer = setInterval(function() {
        current += increment;
        if ((increment > 0 && current >= end) || (increment < 0 && current <= end)) { current = end; clearInterval(timer); }
        obj.innerHTML = current.toLocaleString();
    }, stepTime);
};
function fillSelect(el, data, key) { if (!el) return; var set = new Set(); data.forEach(d => { if (d[key]) set.add(d[key]); }); [...set].sort().forEach(v => el.append(new Option(v, v))); }
function safeStr(val) { return (val === null || val === undefined) ? '' : String(val); }
function showLineage(data) { var text = data._Lineage || "No Lineage"; var title = T('uv_lineage_title') || "Data Lineage"; if (typeof Swal !== 'undefined') Swal.fire({ title: title, text: text, icon: 'info' }); else alert(text); }

// --- [Filter Logic] ---
function applyFilters(f) {
    var data = window.GLOBAL_DATA || [];
    var key = (f.keyword || '').toLowerCase();
    var filtered = data.filter(d => {
        var matchP = f.project === '' || d._ProjectId === f.project;
        var matchProc = f.process === '' || d._Process === f.process;
        var matchSrc = f.source === '' || d._SourceType === f.source;
        var matchInv = f.invest === '' || d._InvestCode === f.invest;
        var dDate = d._UpdateDate || "";
        var matchDate = (f.start && f.end && dDate) ? (dDate >= f.start && dDate <= f.end) : true;
        var jsonStr = JSON.stringify(d).toLowerCase();
        var matchKey = key === '' || jsonStr.includes(key);
        return matchP && matchProc && matchSrc && matchInv && matchDate && matchKey;
    });
    if (typeof table !== 'undefined' && table) { table.clear().rows.add(filtered).draw(); $('#total-badge').text("Total: " + filtered.length.toLocaleString()); }
    if ($('#view-chart-area').is(':visible') && typeof renderAnalysis === 'function') { renderAnalysis(filtered); }
}

// --- [Init Global Filter] ---
window.initGlobalFilter = function(options) {
    $('#global-filter-bar').show();
    if (options.useKeyword) $('#global-keyword-area').show(); else $('#global-keyword-area').hide();
    if (options.defaultKeyword) $('#global-keyword').val(options.defaultKeyword);
    if (typeof flatpickr !== "undefined") {
        var koLocale = { weekdays: { shorthand: ["일", "월", "화", "수", "목", "금", "토"], longhand: ["일요일", "월요일", "화요일", "수요일", "목요일", "금요일", "토요일"] }, months: { shorthand: ["1월", "2월", "3월", "4월", "5월", "6월", "7월", "8월", "9월", "10월", "11월", "12월"], longhand: ["1월", "2월", "3월", "4월", "5월", "6월", "7월", "8월", "9월", "10월", "11월", "12월"] }, rangeSeparator: " ~ ", time_24hr: true };
        flatpickr("#global-date-range", { mode: "range", dateFormat: "Y-m-d", locale: koLocale, defaultDate: options.defaultDate });
    }
    if (typeof options.populateOptions === 'function') { options.populateOptions({ project: $('#filter-project'), process: $('#filter-process'), source: $('#filter-source'), invest: $('#filter-invest') }); }
    $('#global-btn-search').off('click').on('click', function() {
        var dates = $('#global-date-range').val().split(' to ');
        if (typeof options.onSearch === 'function') {
            options.onSearch({ project: $('#filter-project').val(), process: $('#filter-process').val(), source: $('#filter-source').val(), invest: $('#filter-invest').val(), start: dates[0] || "", end: dates[1] || dates[0] || "", keyword: $('#global-keyword').val() });
        }
    });
    $('#global-keyword').off('keyup').on('keyup', function(e) { if (e.key === 'Enter' || e.keyCode === 13) $('#global-btn-search').click(); });
};