# PocTrace 特殊 Trace / Takeoff 逻辑说明（中文）

> Excel：打开同目录 `PocTrace_Special_Trace_Rules_ZH.csv`，或复制下表粘贴到 Excel  
> 相关：检核目录 `PocTrace_Validation_Catalog_ZH.*` · 实现 `TraceAlgorithm.WaterTakeoff.cs` / `TraceAlgorithm.MainLineTakeoff.cs`

文档版本：2026-07

---

## 1. 一眼看懂 — 到达 Takeoff 时的分支

当 Outlet 为 Takeoff(Damper) 时，按以下**优先级**处理（`TraceAlgorithm`）。

| 优先级 | 特例名称 | 条件（起始 POC/设备） | 行为 | STATUS 倾向 |
|--------|----------|----------------------|------|-------------|
| 1 | MainLine Takeoff (VOC-1) | SubDivision1 含 **MAIN**，且 UtilityName 含 **vent / vac / fore line·fore-line·foreline** | **不以 TOF 结束**。写入 Takeoff Step，将下游 continuation 入 BFS 队列。**清空兄弟队列**。结束后若有 EQP，只保留**最短 EQP 1 条** | 正常：**EQP**（经 TOF）。失败：ING 等 |
| 2 | Water Takeoff | UtilityGroupName 含 **water**（忽略大小写） | **不以 TOF 结束**。continuation 入队（保留兄弟队列）。继续 BFS 至 NOZ/CAP/EQP | 下游终点 STATUS |
| 3 | 普通 Takeoff | 非以上 | 在 Takeoff **以 TOF 结束**。Tag 相关用 `EquipmentTagMatcher` | **TOF** |

```
POC → Pipe → … → Takeoff?
              ├─ MainLine? → continuation BFS → (Prefer 最短 EQP)
              ├─ Water?    → continuation BFS
              └─ else      → STATUS=TOF 结束
```

---

## 2. 特例明细表（可复制到 Excel）

| No | 特例ID | 类型 | 适用条件 | 引擎行为（详细） | 结果/副作用 | 检核关联 | 实现文件 |
|----|--------|------|----------|------------------|-------------|----------|----------|
| 1 | SP-WATER-TOF | Trace 引擎 | 起始 POC `UtilityGroupName` 含 "water" | 追加 Takeoff Step（`Damper[Tag]`），用 `TryResolveTakeoffContinuationPoint` 求下游 Outlet 并 Enqueue。再次访问同一 Takeoff 视为回流**消亡（不记 ING）**。无 continuation → **ING** | 抑制普通 TOF 结束。Water **不清空**兄弟队列 | 若仍为 TOF，连接信息不符 Feedback 可能含 `{UtilityGroup} Takeoff`。通常为下游 STATUS | `TraceAlgorithm.WaterTakeoff.cs` |
| 2 | SP-MAINLINE-TOF | Trace 引擎 (VOC-1) | Owner Sub1 含 "MAIN"，且 UtilityName 包含 vent/vac/fore line/fore-line/foreline | 与 Water 相同 continuation+BFS。**差异**：到达 TOF 时 `queue.Clear()` 丢弃兄弟分支；结束时 `PreferMainLineEqpResult`：有 EQP 则按 Length→Hop 保留**最短 1 条**。无 EQP 则保留下游全部结果 | 修复旧版连续 Takeoff 误判 ING。前提为单一最短 EQP | `MainLineTraceHelper.IsMainLineTofChaseResult`（EQP+MAIN+vent/vac/fore*+Steps 含 Takeoff）→ 连接信息不符检查 S.Tag≠T.Tag | `TraceAlgorithm.MainLineTakeoff.cs`, `MainLineTraceHelper.cs` |
| 3 | SP-TOF-DEFAULT | Trace 引擎 | 非 MainLine/Water | 在 Takeoff 结束；`AreRelated(takeoff.Tag, startEquipTag)` | STATUS=TOF | 连接信息不符：S.Tag vs T.Tag | `TraceAlgorithm.cs` |
| 4 | SP-CONT-RESOLVE | 共用解析 | Water·MainLine | (1) 有效 `Takeoff.PipeId` → 下一 Outlet；(2) 否则父 Pipe 其他未访问 Outlet → Takeoff/Pipe/POC | 失败 → ING | — | `TryResolveTakeoffContinuationPoint` |
| 5 | SP-TAG-MATCH | Tag 比较 | 搜索/检核/TOF Match | 精确匹配 · 双向包含 · `Damper[a,b]` 去括号 · 逗号/分号**令牌交集**（Scrubber） · 遗留 substring | 减少误报 | 设备信息/连接信息/TOF Match | `EquipmentTagMatcher.cs` |
| 6 | SP-GCS-EQP-SKIP | 检核例外 | 设备信息不符 | Start 或 Target **Sub1 含 GCS** 则跳过 Tag 不符 | 防 GCS Stick 误报 | `EquipmentInfoMismatchRule` | 同左 |
| 7 | SP-INOUT-SKIP | 检核例外 | 连接信息不符 EQP | Information 为 IN/OUT 则跳过 | — | `ConnectionInfoMismatchRule` | 同左 |
| 8 | SP-UT-VOC | 检核 VOC | UT 不符 | NOZ：IWW1↔FWW 等例外；TOF：Diff Pump / IMP-GN2；EQP：FORE-LINE/By Pass；VENT/FORE/VAC 同义；`UtMapping.csv` | 例外名称则整规则跳过 | `UtilityMismatchRule` | 同左 |
| 9 | SP-POCUT | 检核 | POCUT 不符 · NOZ | PZ&lt;5400：GO2→O2；PZ≥5400：PO2→O2 | 分层 O2 | `PocUtMismatchRule` | 同左 |
| 10 | SP-FIXRESOLVE | 检核 | FixResolve · EQP | Division 非 Equipment，或单分支且 S.GUID=T.GUID | 自回归/非设备 | `FixResolveRule` | 同左 |
| 11 | SP-HOOKUP | Hookup | Main/Sub + Exhaust/Vac | Expected Hookup vs DynProps | MISSING/MISMATCH 等 | `HookupValidationService` | 同左 |

---

## 3. Water 与 MainLine 差异

| 项目 | Water | MainLine (VOC-1) |
|------|-------|------------------|
| 判定键 | Utility**Group** 含 water | Sub1 含 MAIN + Utility**Name** 含 vent/vac/fore* |
| 在 Takeoff 结束？ | 否 | 否 |
| 兄弟 BFS 队列 | 保留 | **清空** |
| 结束后过滤 | 无（可多路径） | 有 EQP 则**只留最短 1 条** |
| 再访 Takeoff | 消亡（无结果） | 相同 |
| 无 continuation | ING | ING |
| 检核重点 | 下游 STATUS | EQP 经 TOF 时强化 Tag 连接检查 |

---

## 4. continuation Outlet 解析顺序

1. `TakeoffData.PipeId` 在缓存中存在 → 下一入口 = 该 PipeId  
2. 否则：以 TakeoffId 为 Outlet 的父 Pipe 的 Outlet1/2/3 中，排除自身与已访问 → 其他 Takeoff / Pipe / POC  
3. 全失败 → Broken(ING)

---

## 5. 运维排查提示

| 现象 | 可能原因 |
|------|----------|
| 是 Water 却以 TOF 结束 | UtilityGroupName 不含 "water" |
| MAIN vent 却以 TOF 结束 | Sub1 无 MAIN，或 UT 名无 vent/vac/fore* |
| MainLine 常 ING | continuation 未连上，或未达 EQP |
| MainLine EQP 正确但其他分支结果消失 | 正常（清队列 + Prefer 最短 EQP） |
| Damper[a,b] vs 设备a 误报不符 | 确认是否走 TagMatcher |
| 详细日志 | `EnableDetailedDebug=true` → `[Water UT]` / `[MainLine]` |

---

## 6. 源码位置

| 文件 | 作用 |
|------|------|
| `PocTrace.Core/Engine/TraceAlgorithm.cs` | Takeoff 优先级、Prefer 调用 |
| `…/TraceAlgorithm.WaterTakeoff.cs` | Water 续追 + continuation |
| `…/TraceAlgorithm.MainLineTakeoff.cs` | MainLine 续追 + 清队列 + Prefer EQP |
| `…/Validation/MainLineTraceHelper.cs` | 检核用 MainLine(经 TOF 的 EQP) 判定 |
| `…/Validation/EquipmentTagMatcher.cs` | Scrubber·Damper Tag |
| `…/Validation/Rules/*.cs` | VOC/例外检核 |
