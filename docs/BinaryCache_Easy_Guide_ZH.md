# 共享缓存里有什么？（非开发人员用）

> PocTrace BinaryCache 简明说明 · 同事共享用  
> 默认路径：`X:\jHOOK\PocTraceData` · 按产线：x1、x2、x2_2 …

---

## 一句话总结

这是 Admin 每天（或按需）从 BIM/配管数据库取出的 **离线快照**。

缓存中包含已布置设备的 **Tag、名称、建设代码**，**连接点（POC）**，**Nozzle / Stick / Takeoff（风阀）**，**配管（长度与连接）**，**Hookup 编号**，以及 **BIM Group 路径**；据此可进行 **沿配管追踪（Trace）** 以及各类检查与 Excel 自动生成。

Viewer、拆除检查（Demolition）以及今后的自动化，只需读取该文件夹即可。无需互联网/CDN，在共享盘上即可运行。  
缓存过旧时（默认约 **48小时**）Viewer 会要求重新生成。

---

## 把文件夹看成菜单

| 文件夹 | 形象比喻 | 里面有什么 |
|--------|----------|------------|
| `GraphCache_产线` | 主菜 | 设备 · 连接点 · 配管 · Takeoff · Hookup — Trace/检查的核心 |
| `BimGroupCache_产线` | 配菜（分组/路径） | Revit/BIM 分组树 → EQPID 属于哪条路径/分组 |
| `TemplateEqpCache_产线` | 单独菜单（3-% 模板） | 不进入普通 Trace 的模板设备 — 拆除「设备信息」补强用 |

---

## GraphCache 内文件 — 用业务语言说

### nodes.bin ≈ 已布置的所有设备/配件清单

现场模型中的设备与零件的 **Tag（EQPID）、名称、分类、工序（Sub）、建设代码、BIM ID** 等。  
可直接查询「这个 Tag 是否在缓存里？」「建设代码是什么？」。  
（拆除 Delete 等在写入阶段已过滤。）

### pocs.bin ≈ POC / Connector（设备上的配管进出口）

各设备的连接点信息，含 **Connector Tag、Connection Tag、Utility、口径、nozzle/stick 区分** 等，并关联「挂在哪台设备上」。  
Nozzle、Stick 工作表与连接检查由此产生。

### pipes.bin ≈ 配管段

每一段管道的 **长度、Utility、两端连接、作业日/作业者**。  
Trace「从这一口走到那一口」时实际走的就是这个文件。设计进度（长度合计）也用到它。

### takeoffs.bin ≈ Takeoff / Damper（风阀/分支）

分支或风阀点。含 **Tag（例如 Damper 上的 EQPID）及相连配管**，用于 TakeOff 工作表及特殊 Trace 继续追踪。

### dynprops.bin ≈ Hookup 编号 · POC 编号

对象上的动态属性。尤其是 **Hookup Number** 中若含 EQPID，可不必沿配管逐段跟踪，仅扫描属性即可查找。（拆除「Hook Up Number」工作表）

### manifest.bin ≈ 保质期标签

记录何时保存、大约多少条。Viewer 若发现缓存过旧会拒绝启动，并提示用 Admin 重新生成。

---

## 旁路文件夹

| 文件夹 | 通俗说明 | 用在哪里 |
|--------|----------|----------|
| `BimGroupCache` | BIM/分组名称树（父→子） | 拆除 Group 工作表：EQPID 所属路径 |
| `TemplateEqpCache` | 仅 3-% 模板设备 | 拆除设备信息中补充模板设备 |

---

## 因此可以做什么

| 想做的事 | 用缓存能否做到 | 说明 |
|----------|----------------|------|
| 设备 Tag·名称·建设代码清单 | 可以 | nodes（必要时加 Template） |
| 查找 Nozzle / Stick / Takeoff | 可以 | 扫描 pocs · takeoffs |
| 沿配管追踪（Trace） | 可以 | POC→Pipe→… 连接追踪 |
| 连接/建模检查 | 可以 | Viewer / Admin 日批 |
| Hookup Number ↔ EQPID | 可以 | 扫描 dynprops 属性 |
| BIM Group 路径 | 可以 | BimGroupCache |
| 实时直连数据库查询 | 通常不需要 | 缓存即快照 |

---

## 拆除检查（Demolition）已经证明的用法

只要有 Input Excel 中的 EQPID，即可只读上述缓存，生成 **设备信息 · Status · Nozzle · Stick · TakeOff · Hookup · Group** 等 Excel，**无需再连数据库**。

同样方式可扩展到其他自动化（清单导出、质量检查、看板）。

---

## 请记住

- 缓存是 **「某一时刻」的拷贝**。模型变更后需用 Admin 重新保存才是最新。
- 各产线（x1、x2…）有独立文件夹。

---

*相关：韩文版 `BinaryCache_Easy_Guide_KO.md` · 项目 docs 文件夹*
