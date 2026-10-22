namespace PocTrace.Demolition
{
  /// <summary>
  /// Demolition UI / Excel / HTML 표시 문자열.
  /// 영문 라벨은 그대로 두고, 한국어는 한국어(中文) 병기.
  /// </summary>
  public static class DemolitionUiText
  {
    public static string Kz(string korean, string chinese)
    {
      if (string.IsNullOrEmpty(korean)) return chinese ?? string.Empty;
      if (string.IsNullOrEmpty(chinese)) return korean;
      return korean + "(" + chinese + ")";
    }

    // —— Excel / Preview 시트명 ——
    public static readonly string SheetSummary = Kz("종합", "综合");
    public static readonly string SheetEquipmentInfo = Kz("설비정보", "设备信息");
    // Status EMP / Nozzle / Stick / TakeOff / Hook Up Number / Group — 영문 유지

    // —— 앱 / 창 ——
    public static readonly string AppTitle = "PocTrace — " + Kz("철거설비 검수", "拆除设备检查");
    public static readonly string PreviewTitle = Kz("철거설비 검수", "拆除设备检查") + " — Preview";
    public static readonly string PreviewHeader =
      Kz("철거설비 검수", "拆除设备检查") + " Preview — " + SheetSummary + " · Excel · HTML";

    // —— MainForm 그리드 ——
    public static readonly string ColProcess = Kz("공정", "工序");
    public static readonly string ColConstructionCode = Kz("건설코드", "建设代码");

    // —— 버튼 / 공통 ——
    public static readonly string BtnRun = "Run " + Kz("검수", "检查");
    public static readonly string BtnExcelSave = "Excel " + Kz("저장", "保存");
    public static readonly string BtnHtmlSummary = "HTML " + Kz("요약", "摘要");
    public static readonly string BtnClose = Kz("닫기", "关闭");

    // —— 대화상자 제목 ——
    public static readonly string DlgInputTitle = Kz("철거설비 검수", "拆除设备检查") + " Input Excel";
    public static readonly string DlgRunTitle = Kz("철거설비 검수", "拆除设备检查") + " — Run";
    public static readonly string DlgExcelSaveTitle = Kz("철거설비 검수 결과 Excel 저장", "拆除设备检查结果Excel保存");
    public static readonly string DlgHtmlSaveTitle = Kz("철거설비 검수 HTML 요약 저장", "拆除设备检查HTML摘要保存");
    public static readonly string DlgLoadFail = "Input " + Kz("로드 실패", "加载失败");
    public static readonly string DlgLoadError = "Input " + Kz("로드 오류", "加载错误");
    public static readonly string DlgInspectFail = Kz("검수 실패", "检查失败");
    public static readonly string DlgInspectError = Kz("검수 오류", "检查错误");
    public static readonly string DlgExcelSaveFail = "Excel " + Kz("저장 실패", "保存失败");
    public static readonly string DlgHtmlSaveFail = "HTML " + Kz("저장 실패", "保存失败");

    // —— 상태 / 안내 ——
    public static readonly string StatusBrowseHint =
      "Input Excel" + Kz("을 Browse로 선택하세요. (기본 폴더 = exe 경로)",
        "请通过Browse选择。(默认文件夹=exe路径)");

    public static readonly string MsgNoValidEqp =
      Kz("유효한 EQPID 행이 없습니다.", "没有有效的EQPID行。");

    public static readonly string MsgRunConfirm =
      Kz("Site별 캐시 로드 → 검수 실행 후 Preview에서 Excel/HTML을 저장합니다.",
        "按Site加载缓存 → 检查后在Preview中保存Excel/HTML。") +
      "\n\nCache: SharedCachePath (Nozzle/Stick/TakeOff=GraphCache)\n";

    public static readonly string MsgRunContinue = Kz("계속할까요?", "是否继续？");

    public static readonly string MsgExcelSavedHint =
      Kz("저장 완료", "保存完成") + ":\n{0}\n\n" +
      Kz("첫 시트", "首个工作表") + "=" + SheetSummary + ", " +
      Kz("이후 상세 7시트", "其后7个明细表");

    public static readonly string MsgHtmlSaved =
      Kz("저장 완료", "保存完成") + ":\n{0}";

    public static readonly string HtmlPageTitle = Kz("철거설비 검수 종합", "拆除设备检查综合");
    public static readonly string HtmlHeading =
      Kz("철거설비 검수", "拆除设备检查") + " — " + SheetSummary + " " + Kz("요약", "摘要");

    public static readonly string DlgInputValidate = "Input " + Kz("검증", "验证");

    public static readonly string StatusReading = Kz("읽는 중…", "读取中…") + " {0}";
    public static readonly string StatusLoadFail = Kz("로드 실패", "加载失败") + ": {0}";
    public static readonly string StatusLoadOk =
      Kz("로드 완료", "加载完成") + ": " +
      Kz("전체", "全部") + " {0}" + Kz("행", "行") + " · " +
      Kz("유효", "有效") + " {1} · " +
      Kz("오류", "错误") + " {2} · Trace EQPID(distinct) {3}";
    public static readonly string StatusLoadError = Kz("로드 오류", "加载错误") + ": {0}";
    public static readonly string StatusFail = Kz("실패", "失败") + ": {0}";
    public static readonly string StatusInspectDone =
      Kz("검수 완료", "检查完成") + " — Preview (" + "EQPID {0}, {1:0.0}s)";
    public static readonly string StatusDoneExcel = Kz("완료", "完成") + " · Excel {0}";
    public static readonly string StatusError = Kz("오류", "错误") + ": {0}";

    public static readonly string MsgPartialInvalid =
      Kz("일부 행에 오류가 있습니다.", "部分行存在错误。") + "\n" +
      Kz("유효", "有效") + ": {0} / " + Kz("오류", "错误") + ": {1}\n" +
      "(InvalidSite / InvalidEqpId — Grid LoadStatus)";

    public static readonly string MsgUnknownError = Kz("알 수 없는 오류", "未知错误");
    public static readonly string EtaSeconds = "ETA {0}" + Kz("초", "秒");
    public static readonly string ZeroSeconds = "0" + Kz("초", "秒");

    public static readonly string MsgStartupError = Kz("실행 오류", "运行错误") + ": {0}\n{1}";

    public static readonly string ProgStart =
      Kz("시작", "开始") + ": Line {0} · EQPID {1}";
    public static readonly string ProgCacheLoad =
      Kz("캐시 로드 중…", "缓存加载中…") + " {0} ({1}/{2})";
    public static readonly string ProgTrace =
      "Trace" + Kz(" 중…", "中…") + " {0} · EQPID {1} (parallel={2})";
    public static readonly string ProgLineDone =
      Kz("라인 완료", "线体完成") + " {0}: Traced={1} Missing={2} NoResult={3} Error={4}";

    public static readonly string ProgEquipScan = SheetEquipmentInfo + " " + Kz("스캔…", "扫描…") + " {0}";
    public static readonly string ProgNozStickTof = "Nozzle/Stick/TakeOff " + Kz("캐시 스캔…", "缓存扫描…") + " {0}";
    public static readonly string ProgStick = "Stick " + Kz("스캔…", "扫描…") + " {0}";
    public static readonly string ProgTakeOff = "TakeOff " + Kz("스캔…", "扫描…") + " {0}";
    public static readonly string ProgHookup = "Hookup " + Kz("스캔…", "扫描…") + " {0}";
    public static readonly string ProgGroup = "Group " + Kz("캐시…", "缓存…") + " {0}";
    public static readonly string ProgDone =
      Kz("완료", "完成") + ": " +
      Kz("설비", "设备") + "/EMP/Noz/Stick/TOF/HU/BG {0}/{1}/{2}/{3}/{4}/{5}/{6} · EQPID {7} · {8:0.0}s";
  }
}
