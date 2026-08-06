# 设备访问分层防线

> 任务来源：`docs/EVOLUTION_PLAN.md` T5.1。本文档定义"谁可以碰设备"的**唯一权威边界**。
> 相关规范：`ERROR_HANDLING.md`（失败语义）、`LOGGING.md`（日志纪律）。
> 目标：业务层永远不依赖具体通信库，通信库永远是可替换插件。

---

## 1. 核心原则

1. **业务/UI 只面向逻辑点名与设备抽象**，永远不见协议地址、不见通信库类型。
2. **协议语法只允许存在于两个地方**：`Configuration/tags.json`（配置）与各驱动插件内部（Address Object）。
3. **通信库（IoTClient/HslCommunication/S7NetPlus/官方 SDK）只允许被驱动插件引用**，替换通信库 = 替换驱动插件，其余代码零改动。

## 2. 分层规则

| 层 | 允许注入/使用 | 禁止 |
|----|--------------|------|
| **UI / 业务插件**（含 ViewModel、业务服务） | `ITagService`（按点名读写，返回 `TagValue` 质量戳结果）、`IDeviceRegistry`（设备列表/状态）、事件订阅（`DeviceStateChangedEvent` / `TagValueChangedEvent` 及其 Prism 桥） | `IPlcService`、`IPlcBatchReadWrite`、`IPlcTypedBatchRead`、`IPlcDriverFactory`、一切驱动/协议/通信库类型；不得出现协议地址字符串字面量 |
| **DeviceRuntime 组件**（`AP.Infra.Hardware` 内部：TagService、采集引擎、适配器等） | 上述全部——它们是防线的实现者 | 不得把协议类型泄出到公共签名（Address Object 一律不透明 `object`） |
| **驱动插件**（hardware/ 下） | 通信库 + 契约接口（实现侧） | 其 internal 类型（地址对象、验证器、客户端）不得被其他程序集引用（仅测试经 `InternalsVisibleTo` 例外） |
| **宿主**（AP.Host.Desktop） | 全部（组装与启动编排） | 不含业务逻辑 |

## 3. 地址纪律

- 业务代码需要设备数据 → **在 `tags.json` 里配点，按点名读 `ITagService`**。不允许把 `"D100"`、`"DB1.0.0"`、`"M0.0"` 写进任何 `.cs` 文件（驱动自身与 `tags.json` 除外）。
- 代码评审中出现协议地址字面量 = 直接打回，没有例外。
- 新点名的命名建议：`<域>.<对象>.<属性>`（如 `Line1.Oven.Temperature`），与现有示例一致。

## 4. 现状审计（2026-07-28）

grep 全仓库 `IPlcService|IPlcBatchReadWrite|IPlcTypedBatchRead`：命中仅 PLC 驱动插件自身（服务/工厂/插件主类/连接命令处理器）与 Infra 实现，**业务/UI 插件零越界**。防线为既成事实，本文档将其明文化。

## 5. 评审清单（新增/修改代码时逐条回答）

1. 这个类型/调用方是否知道"设备用什么协议、什么品牌、什么通信库"？——知道即越界。
2. 这段代码里是否出现了协议地址字符串？——出现即越界（去 tags.json 配点）。
3. 这个插件是否引用了通信库或其他插件的内部实现（而非契约接口）？——引用即越界。

## 6. 防线被突破时的处理

1. **先停手**：不在当前任务里"顺便"越界，记录到 `EVOLUTION_PLAN.md` 停车场。
2. 评估是否属于"合理的新通道"（如新型设备需要新的访问语义）——是则单独立项设计契约；否则回退越界代码。
3. 历史既有通道（`IScannerService` 的扫码事件流、`IPlcService` 的存量直连）沿用至 T5.3 统一退役评估，不新增调用点。

## 7. 与旧接口的关系

- `IPlcService` / `IPlcBatchReadWrite` 现为 **DeviceRuntime 内部实现细节**（TagService/ActivePlcService/审计装饰器使用），不再是对业务开放的 API；对外语义由 `ITagService` 承载。
- 旧接口的 Obsolete/退役处理见 `EVOLUTION_PLAN.md` T5.3，届时专项评审。

## 8. 插件与 Infra 程序集的隔离纪律（2026-08-06 实战教训）

- **插件禁止注入/引用 Infra 层具体类型**（如 `TagAcquisitionEngine`、`LatestTagValueStore`）。插件在独立 `AssemblyLoadContext` 加载；Infra DLL 不在插件输出目录时会经 `PluginLoadContext` 根目录兜底**二次装载进插件 ALC**，与宿主注册的同名类型不恒等 → DryIoc 把构造参数的具体类当未注册类型自动瞬态化 → 插件拿到的是**从未 Start 的新实例**。
- 实战案例（2026-08-06）：`DashboardViewModel` 注入 `TagAcquisitionEngine`/`LatestTagValueStore` 具体类型，导致采集徽标恒显"全部停止"、趋势图永远空白（引擎单例其实在正常运行）。
- **正确做法**：插件需要 Infra 运行时状态/数据时，在契约层定义**只读视图接口**（如 `ILatestTagValueStore`、`ITagAcquisitionStatus`，位于 `AP.Contracts.Hardware/DeviceRuntime/`），Infra 实现并在 DI 中注册转发，插件只依赖契约。
- **既有防护**：`PluginLoadContext.SharedPrefixes` 含 `"AP.Infra"`（误引用时共享宿主副本而非双载）；`Directory.Build.props` 的 `CleanDuplicateLibs` 会删除插件输出的 `AP.Infra.*` 副本。
- **连带坑点**：往 `SharedPrefixes` 新增前缀时，该程序集公开签名中的第三方类型前缀也必须已共享——`AP.Infra.Resilience` 公开方法返回 `Polly.ResiliencePipeline`，只共享 AP.Infra 而不共享 Polly 会导致跨 ALC 方法签名不匹配（`MissingMethodException`）。

---

**版本**: v1.1（2026-08-06，新增第 8 节插件/Infra 隔离纪律）
