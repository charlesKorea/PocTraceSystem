# M-Link / PocTrace 用户手册 (中文)

文档版本: 2026-07 · 适用: Viewer · Admin · HTML日报

---

## 1. 系统概览

| 组件 | 作用 |
|------|------|
| **PocTrace.UI (Viewer)** | 加载缓存 → 按设备Tag追踪 → 表格/Excel |
| **PocTrace.Admin** | DB→BinaryCache→全量Trace→检核→HTML/Excel 日批 |
| **HTML DailyReport** | index / report_{线} / rules / progress |
| **BinaryCache v5** | 每线 GraphCache_{db}/ 分段文件 |

连接一致性检核 与 设计进度(Progress) 为 **独立KPI**。

---

## 2. Viewer 使用步骤

1. 运行 `PocTrace.UI.exe`（检查同目录 `PocTrace.UI.exe.config`）
2. 选择**线体(DB)**并加载缓存  
   - 超过 `CacheMaxAgeHours`(默认48h) 则无法启动 → 用Admin刷新缓存
3. 输入设备 **Tag** 搜索（支持部分匹配、Scrubber逗号Tag）
4. 在结果中查看 PASS/FAIL、问题、相似模型(MainName)
5. 导出 Excel 保存路径与问题明细

### 主要配置

| 键 | 说明 |
|----|------|
| SharedCachePath | 缓存根路径 |
| TargetDatabases | x1,x2,x2_2 … |
| CacheMaxAgeHours | Viewer硬门禁 |
| EnableDetailedDebug | 详细Trace日志 |
| MaxParallelTrace | 并行度 |

---

## 3. Admin 日批

1. 运行 Admin，检查连接串与路径  
2. **Full**: 查库→写缓存→全量Trace+检核→HTML/Excel→Rules/Progress  
   **CacheOnly**: 仅刷新缓存  
3. 输出目录 `DailyReportPath`：`index.html`、`report_{db}.html`、`rules.html`、`progress.html` 等

| 键 | 说明 |
|----|------|
| DailyReportPath | 报告输出 |
| DesignProgressEnabled | 设计进度开关 |
| FrmConnectionString | 空=仅Graph进度 |
| EnableDetailedDebug | 批处理详细日志 |

---

## 4. 查看 HTML 报告

用浏览器打开输出目录HTML。**禁止CDN，仅本地assets**。

| 页面 | 内容 |
|------|------|
| index.html | 多线汇总 · Quality Point |
| report_{db}.html | KPI·图表·今日处置·表格 |
| rules.html | Check Rules · 日Hit趋势 |
| progress.html | 设计进度% |

语言切换: ko / en / zh。

---

## 5. 如何理解检核结果

- **PASS**: 该POC路径无Issue  
- **FAIL**: 存在一条及以上检核问题  
- 明细见 Excel / HTML Top问题 / 归属图  
- 全量规则: `PocTrace_Validation_Catalog_ZH.csv`

STATUS: EQP / NOZ / TOF / CAP / EMP / ING / ERR — 见目录文档。

---

## 6. Design Progress

- **进行中**: 配管 Pipe.OpTime 在今天 D-2 内  
- **进度%**: 相对同 MainName 已完成设备的 Length/BOM **中位数** (默认权重0.6/0.4)  
- 样本不足显示 Low Confidence  
- Utility 按钮查看分项

---

## 7. 故障排除

| 现象 | 处理 |
|------|------|
| 缓存过期 | Admin CacheOnly/Full |
| 无详细日志 | 同目录config开启 EnableDetailedDebug |
| HTML无图 | 保持assets相对路径 |
| Progress不可信 | MainName完成样本不足或未接FRM |

---

## 8. 相关文档

- 检核目录: `PocTrace_Validation_Catalog_ZH.csv` / `.md`
- **特殊 Trace（Water/MainLine Takeoff 等）**: `PocTrace_Special_Trace_Rules_ZH.md` / `.csv`
- 韩文: `PocTrace_User_Manual_KO.md`, `PocTrace_Validation_Catalog_KO.*`, `PocTrace_Special_Trace_Rules_KO.*`
