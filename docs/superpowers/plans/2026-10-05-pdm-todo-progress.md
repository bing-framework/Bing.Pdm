# PDM TODO 执行状态（2026-10-05）

目标：继续实现上次审查的计划内缺口与欠缺考虑项。源码修改位于当前仓库工作区。
冻结范围为 12 项本地工作与 2 项外部验收；本地工作已关闭 12/12，外部验收仍为 0/2。
本地完成指已列出的实现与回归条件完成，不表示 PowerDesigner 像素级视觉已获人工认可。

## 范围与状态

| 编号 | 状态 | 完成内容与证据 |
|---|---|---|
| I1 | CLOSED | 名称兜底后统一键、索引、外键及列顺序的配对身份；GUID 重建与关系身份回归通过。 |
| I2 | CLOSED | 引用按递归包路径兜底；跨包同名关系不误配，GUID 保持的移动列出两侧路径。 |
| I3 | CLOSED | 迁移和读取共用已知字段类型校验；重复属性和损坏结构拒绝，节点迁移保留未知字段。 |
| I4 | CLOSED | Schema 覆盖实际导出字段及新增 Trigger/段落对齐字段，自动属性覆盖测试通过；格式版本仍为 1。 |
| I5 | CLOSED | 可注入字体度量；Windows CLI 使用 GDI。支持文字单元换行、段落对齐、裁剪、模型字段顺序；新增原生段落样例，15 例记录差异。8pt 按原生 SVG 校准；人工像素验收保留 E1。 |
| I6 | CLOSED | RTF 字体/颜色/下划线、每图元阴影、触发器 Time/Event/Text 与清单；内置全部/主键/键列过滤。自定义过滤通过显式回调解释，未知配置保留并诊断。 |
| C1 | CLOSED | 结构校验问题进入 ValidationIssues/Incomplete；JSON/Markdown/HTML 显示原因。默认写报告返回 0，--fail-on-incomplete 返回 1，优先于变更返回码 3。 |
| C2 | CLOSED | 双工作区按稳定模型 GUID/对象 GUID 比较依赖，别名与等价 GUID 表示不误报；CLI 清单入口与重复 GUID 错误退出码通过。 |
| C3 | CLOSED | 明确节点迁移保留未知字段；读入业务模型再导出忽略未建模字段。 |
| C4 | CLOSED | PdmModelValidator 独立检查当前集合的身份、悬空引用、类型与归属；不依赖旧 Lookup，不改诊断。结构/图形/元数据范围分开，集合环可报告。 |
| C5 | CLOSED | export/generate/diff/migrate 先暂存再发布；同名计划文件可覆盖，其他文件保留；受控中途失败恢复原文件，恢复失败保留路径。 |
| C6 | CLOSED | 15 个原生候选各自绑定输入、原图、几何、SVG、核心/CLI 程序集、脚本与浏览器版本；历史证据不覆盖。 |
| E1 | BLOCKED_APPROVAL | 原生视觉人工确认。差异指标仅用于记录；没有像素通过阈值，也未声明像素级通过。 |
| E2 | BLOCKED_EXTERNAL | 远程 CI 未执行；没有提交/推送/触发授权，本地验证不替代该项。 |

## 变更影响分析

- 生产范围：模型比较、独立校验、GUID 身份、工作区错误归类、JSON 契约、触发器模型/索引、RTF 与 SVG 排版、CLI 输出暂存恢复。
- 公开契约：新增类型、选项和可选字段，既有签名保留；核心 netstandard2.0/C# 7.1，CLI/测试 .NET 8。
- 构建/依赖：无新增运行时包与 TFM 变更。Windows 字体度量使用本机 GDI，不打包字体。
- 风险：中等，涉及输入、关系匹配与多文件写出。受控失败、双模型 CLI 和实际消费均已验证。

## 验证证据

- L0：核心及 CLI 构建 0 警告/0 错误；git diff --check 通过。
- L1：本轮新增/修改的校验、身份、输出恢复、排版、Schema 与 CLI 定向测试通过。
- L4 收口：dotnet test Bing.Pdm.sln --no-restore，145/145。
- 浏览器：全集合 16 项通过，1 项旧图元数量断言失败；修正选择器排除 clipPath 定义后，仅重跑该项通过。17 项均已通过，不为文档修改重复全测。
- Browser plugin 不可用，复用仓库 Playwright 与本机 Chrome 154.0.8037.58；桌面 1280x850/移动 390x844 验证离线、链接、缩放、富文本与触发器，无外部请求/应用错误。
- 四份真实 PDM：EDI 29/281、物流 135/2412、仓储 141/2428、进销存 294/5891（表/列）。诊断 0/2/0/3，全部 Shortcut 成功解析；六格式导出、严格预检及 fallback 实体编译通过。
- PDM/JSON 比较均 Changes=0；实体内容完全相同（29/135/141/294），复用其已通过的编译证据。
- EDI/仓储比较完整；物流 o2884 缺失父键、进销存 o7727 缺失子列，两侧均明确进入不完整原因，未被当作完整比较。
- 原生 15 例均产生原图、渲染图、叠加图、差异图、指标与证据绑定。基础图像素 MAE 16.61/255、触发器 19.17/255、段落 17.01/255；均待人工确认。
- 未执行性能 Benchmark；本轮没有性能门槛。远程 CI 未执行。

本机证据位于忽略的 artifacts 目录：

- artifacts/native-compatibility/30e8ce6248f24e4f81f286283f36f7a3/summary.json
- artifacts/browser-qa/native-trigger-paragraph-1791159397749
- artifacts/browser-qa/rich-text-1791159397305
- artifacts/real-pdm-compatibility/run-20261005-081431-a7dcfb96/json-consumer-verification.json

公开基准新增 native-triggers 与 native-paragraphs，由 PowerDesigner 16.7.4.6866 原生导出。
原有 13 例基准与历史候选未修改。真实业务输入不修改、不进入仓库。

## 已验证边界与视觉差异记录

- 自定义过滤没有任意 PowerDesigner 脚本解释器；调用方可提供 ColumnFilter，无法解释则报告诊断并保留可查看结果。
- 字体测量器缺失或无效会产生 APPROXIMATE_FONT_METRICS；缺失字体等诊断阻止直接原生视觉资格声明。
- RTF 段落文字及样式、换行与裁剪已实现；原生注释的白色文字背景、可见行数、抗锯齿及部分间距仍有差异。人工对照决定视觉是否可接受，不用无批准阈值替代人工确认。
- 输出恢复覆盖进程内异常，不保证断电、强制终止或同时写同一目录的跨文件原子性。
- 校验检查身份与引用，不判断 SQL 等价性、数据库迁移安全性，也不自动重解析 Shortcut。

## 收口

Open Actionable: none（冻结的本地 TODO 已完成）。
Blocked Approval: E1；Blocked External: E2。
Not Applicable: 性能 Benchmark。
Deferred: 本轮没有新增延期项，原计划既定排除范围保持不变。
No-Progress Check: CHANGED（源码、测试、文档与新证据均有进展）。
Next Action: STOP，等待原生视觉人工确认及远程 CI 授权；不继续用本地重复测试代替外部门禁。
Goal Status: STOPPED_BLOCKED（本地实现完成，外部发布验收未完成）。
