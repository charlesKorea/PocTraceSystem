# PocTrace 检核项目目录 (ZH)

> 用 Excel 打开同目录 `PocTrace_Validation_Catalog_ZH.csv`  
> 或全选下表 → 复制 → 粘贴到 Excel

| No | RuleId | 问题标题(报告) | 分类 | 目标 STATUS | 检核内容(详细) | 例外/备注 | 归属提示 | 实现 |
|----|--------|----------------|------|-------------|---------------|-----------|----------|------|
| 1 | 拆除设备连接 | 拆除设备连接 | 连接/状态 | ERR | 结束状态为ERR(拆除/Delete等) | 无 | SCS | ErrStatusRule |
| 2 | 建模未完成 | 建模未完成 | 建模 | ING | ING且非例外UT/名称 | 例外则跳过 | 设计社 | ModelingIncompleteRule |
| 3 | 设备信息不符 | 基准信息问题 | 基准信息 | EQP | S.Tag与T.Tag不相关；Scrubber/Damper[]允许 | Sub1含GCS排除 | SCS | EquipmentInfoMismatchRule |
| 4 | POCUT不符 | 基准信息问题 | 基准信息 | NOZ | 1层GO2→O2；2/3层PO2→O2 | 仅该组合 | SCS | PocUtMismatchRule |
| 5 | 无建模 | 无建模 | 建模 | EMP | EMP且UT无“未使用” | 含未使用则跳过 | 设计社 | ModelingAbsentRule |
| 6 | 连接信息缺失 | 连接信息缺失 | 连接信息 | TOF/NOZ/EQP | Tag/ConnectionTag/GCS STICK空 | 仅GCS STICK | 设计社 | ConnectionMissingRule |
| 7 | 连接信息不符 | 连接信息不符 | 连接信息 | TOF/NOZ/EQP | MainLine/TOF/NOZ/Scrubber/GCS/分支 | IN/OUT排除 | 设计社 | ConnectionInfoMismatchRule |
| 8 | 需FixResolve | 需Fix Resolve | 结构 | EQP | Division≠Equipment或S.GUID=T.GUID | 单分支 | 设计社 | FixResolveRule |
| 9 | UT不符 | UT不符/(空白) | Utility | NOZ/TOF/EQP | UT空白或Connected UT不一致 | VOC例外 | 设计社 | UtilityMismatchRule |
| 10 | HookupValidation | MISSING等 | Hookup | Exhaust/Vac | Main/Sub期望Hookup比对 | 固定1条 | SCS/运营 | HookupValidationService |
| 11 | (预处理) | 基准信息问题 | 基准信息 | EQP/NOZ/TOF | 例外Utility→POC Utility不符 | 管线外 | SCS | TraceValidator |
| 12 | (预处理) | 建模未完成 | 建模 | ING | 例外UT或elec | 管线外 | 设计社 | TraceValidator |

## STATUS 缩写
| STATUS | 含义 |
|--------|------|
| EQP | 到达其他设备POC |
| NOZ | Nozzle终端 |
| TOF | Takeoff终端 |
| CAP | 封盖/结束 |
| EMP | 无连接 |
| ING | 未完成/环路/中断 |
| ERR | 错误/拆除 |

## 归属规则(摘要)
- 标题含 **基准信息** 或 **装备** → 归属 **SCS**
- 其余设计责任 → **设计社**，计入 HU invalidCount
