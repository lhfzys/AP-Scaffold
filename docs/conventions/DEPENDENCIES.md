# 项目依赖方向规范

> 任务来源：`docs/EVOLUTION_PLAN.md` T6.2。本文档定义 csproj 级引用方向的**目标规则**与**现状例外**。
> 与 `LAYERING.md`（设备访问边界）互补：那管"谁能碰设备"，这管"谁能引用谁"。

---

## 1. 目标依赖方向（允许边）

```
Hosts        → 任意（组装者，兜底）
Plugins      → Contracts.*、Shared.*（按域）
             ✗ 不得引用 Infra 实现项目（既有例外见第 3 节）、不得引用其它插件
Infra.*      → Contracts.*、Shared.*
             ✗ 不得引用其它 Infra 项目（横向依赖）、不得引用 AP.Core、不得引用插件
Contracts.*  → （无项目引用，仅包引用）
             ✗ 不得引用任何平台项目（契约是依赖图最底层）
Shared.*     → Contracts.*（Utilities 例外：最底层工具，不引用任何平台项目）
AP.Core      → Shared.Utilities
Tests        → 被测项目（含经 InternalsVisibleTo 开放的插件 internal）
```

## 2. 规则说明

- **契约层零平台引用**是底线：2026-07-28 已删除 `Contracts.Core→AP.Core` 死引用（T5.4），契约层现已回到依赖图最底层，不允许再出现回退。
- **Infra 不得横向引用**：Infra 间的协作经宿主 DI 组装或契约事件完成，不经项目引用。
- **插件不得引用 Infra 实现**：插件面向契约接口工作；需要 Infra 能力时经 DI 注入契约接口。

## 3. 现状例外登记（容忍，不强改；新增引用不得扩大例外）

| 引用 | 现状 | 备注 |
|------|------|------|
| PLC/Scanner 插件 → `AP.Infra.Hardware` | 容忍 | DeviceRuntime 组件（Supervisor/状态机）落位 Infra 所致；长期可评估拆分 `AP.Infra.Devices` 或下沉契约 |
| `AP.Plugin.Layout` → `AP.Infra.Hardware` | 容忍 | Dashboard 直接解析 `TagAcquisitionEngine` 具体类（采集状态展示）；长期可评估引擎状态抽象下沉契约 |
| PLC 插件 → `AP.Infra.Resilience` | 容忍 | 驱动工厂取 Polly 管道；更优形态是宿主注入管道抽象 |
| `AP.Infra.Database` → `AP.Infra.Resilience`、`AP.Core` | 容忍 | 仓储接重试管道；Core 引用为历史遗留 |
| `AP.Infra.Hardware` → `AP.Infra.Resilience` | 容忍 | 同上 |
| `AP.Infra.{Security,Recipe}` → `AP.Infra.Database` | 容忍 | 数据访问复用 |
| `AP.Infra.Report` → `AP.Core` | 容忍 | 历史遗留 |
| `AP.Infra.Grpc` → `AP.Core` ❄ | 冻结中 | 不维护 |

## 4. 评审清单（新增/修改 csproj 引用时）

1. 新增引用是否符合第 1 节允许边？——不符合先停手，记录停车场并评估是否需调整架构。
2. 是否在扩大第 3 节的既有例外？——同样先停手。
3. 被引用方是否真的被代码使用？——用后即删（死引用先例：PLC 插件→Shared.UI，T6.1）。

---

**版本**: v1.0（2026-07-28，T6.2）
