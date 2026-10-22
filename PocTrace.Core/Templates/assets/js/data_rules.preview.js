const IS_RULES_PAGE = true;
const RULES_META = {
  genDate: '2026-07-22 07:30:00',
  ruleCount: 10,
  totalHits: 54821
};
const RULES_CATALOG = [
  { Order: 1, RuleId: '철거설비연결', Description: 'STATUS=ERR (철거/Delete 등)인 연결을 검출합니다.', HitCount: 312 },
  { Order: 2, RuleId: '모델링미완료', Description: 'STATUS=ING이며 예외 UT/이름이 아니면 모델링 미완료로 판정합니다.', HitCount: 6986 },
  { Order: 3, RuleId: '설비정보상이', Description: 'EQP 도달 시 S.Tag와 T.Tag 불일치(Scrubber 콤마 태그는 교집합 허용)를 검출합니다.', HitCount: 646 },
  { Order: 4, RuleId: 'POCUT상이', Description: 'NOZ POC의 층별 GO2/PO2와 O2 UT 불일치를 검출합니다.', HitCount: 128 },
  { Order: 5, RuleId: '모델링없음', Description: 'STATUS=EMP이고 UT에 미사용이 없으면 모델링 없음으로 판정합니다.', HitCount: 20764 },
  { Order: 6, RuleId: '연결정보누락', Description: 'TOF/NOZ/EQP(GCS STICK)에서 Tag/ConnectionTag 누락을 검출합니다.', HitCount: 47 },
  { Order: 7, RuleId: '연결정보상이', Description: 'TOF/NOZ/EQP 연결정보 불일치(MainLine VOC, Scrubber, GCS STICK 포함)를 검출합니다.', HitCount: 5835 },
  { Order: 8, RuleId: 'FixResolve필요', Description: 'EQP인데 Division이 Equipment가 아니거나 단일 배관 S.GUID==T.GUID인 Fix Resolve 대상을 검출합니다.', HitCount: 9920 },
  { Order: 9, RuleId: 'UT상이', Description: 'NOZ/TOF/EQP의 Connected UT 불일치(UtMapping 그룹, VOC 예외 포함)를 검출합니다.', HitCount: 9859 },
  { Order: 10, RuleId: 'HookupValidation', Description: 'H/U Number 검증(단일 고정 Rule). Exhaust/Vac 대상 POC의 Connector Tag와 Hookup 번호 일치/누락/불일치를 검사합니다.', HitCount: 324 }
];
const RULES_HISTORY = [
  { Date: '2026-04-01', RuleCount: 7, TotalHits: 41200, Hits: { '모델링없음': 18000, 'FixResolve필요': 8000, '모델링미완료': 6000, '연결정보상이': 4000, 'UT상이': 4200, '설비정보상이': 500, '연결정보누락': 500 } },
  { Date: '2026-04-08', RuleCount: 8, TotalHits: 43850, Hits: { '모델링없음': 19000, 'FixResolve필요': 8500, '모델링미완료': 6500, '연결정보상이': 4500, 'UT상이': 4500, '설비정보상이': 520, '연결정보누락': 80, 'POCUT상이': 250 } },
  { Date: '2026-04-15', RuleCount: 9, TotalHits: 48120, Hits: { '모델링없음': 20100, 'FixResolve필요': 9200, '모델링미완료': 6800, '연결정보상이': 5200, 'UT상이': 5600, '설비정보상이': 600, '연결정보누락': 60, 'POCUT상이': 180, '철거설비연결': 380 } },
  { Date: '2026-04-21', RuleCount: 9, TotalHits: 51240, Hits: { '모델링없음': 20500, 'FixResolve필요': 9600, '모델링미완료': 6900, '연결정보상이': 5600, 'UT상이': 7200, '설비정보상이': 640, '연결정보누락': 50, 'POCUT상이': 150, '철거설비연결': 600 } },
  { Date: '2026-05-05', RuleCount: 9, TotalHits: 50110, Hits: { '모델링없음': 20200, 'FixResolve필요': 9400, '모델링미완료': 6700, '연결정보상이': 5500, 'UT상이': 7000, '설비정보상이': 610, '연결정보누락': 45, 'POCUT상이': 140, '철거설비연결': 515 } },
  { Date: '2026-05-19', RuleCount: 9, TotalHits: 49580, Hits: { '모델링없음': 19950, 'FixResolve필요': 9300, '모델링미완료': 6600, '연결정보상이': 5450, 'UT상이': 6900, '설비정보상이': 590, '연결정보누락': 42, 'POCUT상이': 135, '철거설비연결': 613 } },
  { Date: '2026-06-02', RuleCount: 9, TotalHits: 48820, Hits: { '모델링없음': 19600, 'FixResolve필요': 9100, '모델링미완료': 6400, '연결정보상이': 5300, 'UT상이': 6800, '설비정보상이': 570, '연결정보누락': 40, 'POCUT상이': 130, '철거설비연결': 880 } },
  { Date: '2026-06-16', RuleCount: 10, TotalHits: 52100, Hits: { '모델링없음': 20400, 'FixResolve필요': 9700, '모델링미완료': 6900, '연결정보상이': 5700, 'UT상이': 7600, '설비정보상이': 620, '연결정보누락': 48, 'POCUT상이': 145, '철거설비연결': 420, 'HookupValidation': 567 } },
  { Date: '2026-06-30', RuleCount: 10, TotalHits: 53440, Hits: { '모델링없음': 20650, 'FixResolve필요': 9850, '모델링미완료': 7050, '연결정보상이': 5800, 'UT상이': 8200, '설비정보상이': 635, '연결정보누락': 50, 'POCUT상이': 140, '철거설비연결': 380, 'HookupValidation': 685 } },
  { Date: '2026-07-07', RuleCount: 10, TotalHits: 54110, Hits: { '모델링없음': 20720, 'FixResolve필요': 9900, '모델링미완료': 7020, '연결정보상이': 5820, 'UT상이': 8800, '설비정보상이': 640, '연결정보누락': 48, 'POCUT상이': 135, '철거설비연결': 350, 'HookupValidation': 677 } },
  { Date: '2026-07-14', RuleCount: 10, TotalHits: 54690, Hits: { '모델링없음': 20750, 'FixResolve필요': 9950, '모델링미완료': 7000, '연결정보상이': 5840, 'UT상이': 9200, '설비정보상이': 645, '연결정보누락': 47, 'POCUT상이': 132, '철거설비연결': 330, 'HookupValidation': 796 } },
  { Date: '2026-07-21', RuleCount: 10, TotalHits: 54980, Hits: { '모델링없음': 20800, 'FixResolve필요': 9980, '모델링미완료': 7010, '연결정보상이': 5860, 'UT상이': 9500, '설비정보상이': 648, '연결정보누락': 47, 'POCUT상이': 130, '철거설비연결': 320, 'HookupValidation': 685 } },
  { Date: '2026-07-22', RuleCount: 10, TotalHits: 54821, Hits: { '모델링없음': 20764, 'FixResolve필요': 9920, '모델링미완료': 6986, '연결정보상이': 5835, 'UT상이': 9859, '설비정보상이': 646, '연결정보누락': 47, 'POCUT상이': 128, '철거설비연결': 312, 'HookupValidation': 324 } }
];
const AVAILABLE_LINES = ['x1', 'x2', 'x2_2'];
