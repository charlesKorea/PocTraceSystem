const IS_PROGRESS_PAGE = true;
const PROGRESS_META = {
  dbName: 'ALL',
  genDate: '2026-07-23 07:40:00',
  totalEquip: 6,
  inProgressCount: 3,
  lowConfidenceCount: 1,
  avgProgressPct: 62.4
};
const PROGRESS_HISTORY = [
  { Date: '2026-07-16', InProgressCount: 5, TotalEquip: 6, AvgProgressPct: 54.0 },
  { Date: '2026-07-18', InProgressCount: 4, TotalEquip: 6, AvgProgressPct: 58.2 },
  { Date: '2026-07-20', InProgressCount: 4, TotalEquip: 6, AvgProgressPct: 60.1 },
  { Date: '2026-07-22', InProgressCount: 3, TotalEquip: 6, AvgProgressPct: 61.8 },
  { Date: '2026-07-23', InProgressCount: 3, TotalEquip: 6, AvgProgressPct: 62.4 }
];
const PROGRESS_DATA = [
  {
    Tag: 'CACM01', DbName: 'x1', Guid: 'demo-1', MainName: 'FCAC_REFLEXION-LK_CMP-CU-LK400_R01', Process: 'CMP',
    FirstDesignAt: '2026-05-01', LastDesignAt: '2026-07-22', DesignDays: 82,
    IsInProgress: true, LowConfidence: false, Overrun: false,
    CurrentLength: 42000, CurrentBomCount: 180, BaselineLengthMedian: 55000, BaselineBomMedian: 220,
    CohortN: 5, LengthPct: 76.4, BomPct: 81.8, ProgressPct: 78.6, FrmStatus: null,
    Utilities: [
      { UtilityName: 'PCW', CurrentLength: 12000, CurrentBomCount: 40, BaselineLengthMedian: 15000, BaselineBomMedian: 50, LengthPct: 80, BomPct: 80, ProgressPct: 80, Overrun: false },
      { UtilityName: 'VAC', CurrentLength: 18000, CurrentBomCount: 70, BaselineLengthMedian: 22000, BaselineBomMedian: 90, LengthPct: 81.8, BomPct: 77.8, ProgressPct: 80.2, Overrun: false },
      { UtilityName: 'CDA', CurrentLength: 12000, CurrentBomCount: 70, BaselineLengthMedian: 18000, BaselineBomMedian: 80, LengthPct: 66.7, BomPct: 87.5, ProgressPct: 75.0, Overrun: false }
    ]
  },
  {
    Tag: 'CACM02', DbName: 'x1', Guid: 'demo-2', MainName: 'FCAC_REFLEXION-LK_CMP-CU-LK400_R01', Process: 'CMP',
    FirstDesignAt: '2026-06-10', LastDesignAt: '2026-07-21', DesignDays: 41,
    IsInProgress: true, LowConfidence: false, Overrun: false,
    CurrentLength: 31000, CurrentBomCount: 140, BaselineLengthMedian: 55000, BaselineBomMedian: 220,
    CohortN: 5, LengthPct: 56.4, BomPct: 63.6, ProgressPct: 59.3, FrmStatus: null,
    Utilities: [
      { UtilityName: 'PCW', CurrentLength: 9000, CurrentBomCount: 30, BaselineLengthMedian: 15000, BaselineBomMedian: 50, LengthPct: 60, BomPct: 60, ProgressPct: 60, Overrun: false },
      { UtilityName: 'VAC', CurrentLength: 22000, CurrentBomCount: 110, BaselineLengthMedian: 22000, BaselineBomMedian: 90, LengthPct: 100, BomPct: 100, ProgressPct: 100, Overrun: true }
    ]
  },
  {
    Tag: 'ETCH10', DbName: 'x1', Guid: 'demo-3', MainName: 'ETCH_TOOL_MODEL_A', Process: 'ETCH',
    FirstDesignAt: '2026-03-15', LastDesignAt: '2026-06-01', DesignDays: 78,
    IsInProgress: false, LowConfidence: false, Overrun: false,
    CurrentLength: 88000, CurrentBomCount: 410, BaselineLengthMedian: 90000, BaselineBomMedian: 400,
    CohortN: 8, LengthPct: 97.8, BomPct: 100, ProgressPct: 98.7, FrmStatus: 'Complete',
    Utilities: [
      { UtilityName: 'FORELINE', CurrentLength: 50000, CurrentBomCount: 200, BaselineLengthMedian: 52000, BaselineBomMedian: 210, LengthPct: 96.2, BomPct: 95.2, ProgressPct: 95.8, Overrun: false }
    ]
  },
  {
    Tag: 'CVD03', DbName: 'x1', Guid: 'demo-4', MainName: 'CVD_UNIQUE_MODEL', Process: 'CVD',
    FirstDesignAt: '2026-07-20', LastDesignAt: '2026-07-23', DesignDays: 3,
    IsInProgress: true, LowConfidence: true, Overrun: false,
    CurrentLength: 15000, CurrentBomCount: 60, BaselineLengthMedian: 0, BaselineBomMedian: 0,
    CohortN: 1, LengthPct: 0, BomPct: 0, ProgressPct: 0, FrmStatus: null,
    Utilities: [
      { UtilityName: 'UNKNOWN', CurrentLength: 15000, CurrentBomCount: 60, BaselineLengthMedian: 0, BaselineBomMedian: 0, LengthPct: 0, BomPct: 0, ProgressPct: 0, Overrun: false }
    ]
  },
  {
    Tag: 'IMP01', DbName: 'x2', Guid: 'demo-5', MainName: 'IMP_STANDARD_R2', Process: 'IMP',
    FirstDesignAt: '2026-02-01', LastDesignAt: '2026-05-10', DesignDays: 98,
    IsInProgress: false, LowConfidence: false, Overrun: true,
    CurrentLength: 72000, CurrentBomCount: 300, BaselineLengthMedian: 60000, BaselineBomMedian: 280,
    CohortN: 4, LengthPct: 100, BomPct: 100, ProgressPct: 100, FrmStatus: null,
    Utilities: [
      { UtilityName: 'PCW', CurrentLength: 72000, CurrentBomCount: 300, BaselineLengthMedian: 60000, BaselineBomMedian: 280, LengthPct: 100, BomPct: 100, ProgressPct: 100, Overrun: true }
    ]
  },
  {
    Tag: 'PHOTO02', DbName: 'x2', Guid: 'demo-6', MainName: 'PHOTO_SCANNER_X', Process: 'PHOTO',
    FirstDesignAt: '2026-01-10', LastDesignAt: '2026-04-01', DesignDays: 81,
    IsInProgress: false, LowConfidence: false, Overrun: false,
    CurrentLength: 45000, CurrentBomCount: 190, BaselineLengthMedian: 48000, BaselineBomMedian: 200,
    CohortN: 6, LengthPct: 93.8, BomPct: 95.0, ProgressPct: 94.3, FrmStatus: 'Complete',
    Utilities: []
  }
];
const AVAILABLE_LINES = ['x1', 'x2', 'x2_2'];
