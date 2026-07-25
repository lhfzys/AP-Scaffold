# 错误处理与 Result 使用规范

> 任务来源：`docs/EVOLUTION_PLAN.md` T0.1。本文档是全仓库错误处理的**唯一权威约定**，新代码必须遵守；存量代码按演进计划逐步迁移，不得在同一次提交中顺手改造无关代码。
> 相关规范：`docs/conventions/LOGGING.md`（T0.2 建立）。

---

## 1. 核心原则

1. **通信失败是常态，不是异常**。设备掉线、读写超时、地址非法在工业现场每天都会发生，必须用**返回值**表达（Result / 质量戳），不允许用异常驱动正常业务流。
2. **异常只表达三类情况**：① 编程错误（调用方违反契约，如传 null、点表外的点名）；② 领域规则违反（业务不允许的操作）；③ 真正的意外故障（资源耗尽、未处理的外部错误）。
3. **错误必须携带结构化错误码**（`ErrorCode` 常量），不允许只有一句自由文本。日志文案给人看，错误码给程序判断。
4. **不允许 `throw new Exception(...)` 裸异常**（无类型、无错误码）。新代码抛异常必须继承 `PlatformException` 或使用具体系统异常（`ArgumentException` 等）。
5. **一个方法内不混用两种风格**：要么全程 Result，要么全程异常，不允许"部分路径返回 Fail、部分路径 throw"。

## 2. 分层规则

| 层 | 典型组件 | 失败表达方式 | 说明 |
|----|----------|--------------|------|
| 驱动层（协议插件内部） | `XxxPlcService`、`SerialPortScannerService` | 内部实现允许抛 `PlatformException` 子类；**通信库自己的异常类型不得泄出驱动边界**（必须捕获并包装） | 驱动是适配器，第三方异常（IoTClient/HslCommunication 的异常类）属于"具体库细节"，泄出即违反依赖倒置 |
| 设备访问层 | `ITagService`、采集引擎（T4.x 落地） | **返回质量戳结果**：读失败返回 `TagValue{Quality=Bad}`，写失败返回带 ErrorCode 的 Result；**不抛异常**表达通信失败 | 只有编程错误才抛：如查询点表中不存在的点名（`ArgumentException`，点表在启动时已校验，运行时不存在=调用方写错） |
| 业务服务层 | `RecipeManager`、`ReportService` 等 | **预期失败返回 `OperationResult<T>`**（校验不通过、记录不存在、权限不足）；**意外故障抛 `PlatformException` 子类** | "预期"= 业务流程内可预见、UI 需要针对性提示的情况 |
| UI 层 | ViewModel | 不制造错误，只**消费**：检查 Result.Success 并提示；调用设备访问层时按 Quality 显示；意外异常由全局兜底 | ViewModel 里允许 try/catch 包住用户操作入口，catch 后走 `ICustomDialogService` 提示并记日志，**不允许吞异常**（catch 后什么都不做） |
| 宿主兜底 | `GlobalExceptionHandler` | 最后防线，写崩溃日志 | 已具备，不变 |

### 2.1 决策速查

```
这个失败是现场常态（掉线/超时/设备忙）？        → 返回值表达（质量戳 / Result）
这个失败是调用方代码写错了？                    → ArgumentException / InvalidOperationException
这个失败是业务规则不允许（如删除默认配方）？     → OperationResult.Fail（UI 需要提示用户时）
这个失败完全出乎意料？                          → PlatformException 子类，交给上层兜底
```

## 3. OperationResult 使用约定

- 只用于**业务服务层跨插件/跨层边界的预期失败**，不用于层内私有方法。
- 失败必须填 `ErrorCode`（用 `ErrorCode` 常量，禁止手写字面量）；`Message` 面向最终用户，写中文、说人话（"配方名称不能为空"，不是"parameter invalid"）。
- 成功路径 `OperationResult.Ok(data)`，不要把"成功但为空"做成 Fail。
- `OperationResult<T>` 不适合表达"部分成功"（如批量导入 10 条成 8 条）——这种场景定义专用 DTO（成功列表+失败明细），不要硬塞进 Message。

## 4. 异常体系约定

- 基类：`PlatformException`（携带 `ErrorCode`），位于 `AP.Contracts.Core`。
- 规划中的子类（随演进任务按需新增，新增即契约，**先规划后实现**）：
  | 异常 | 用途 | 落地任务 |
  |------|------|----------|
  | `DeviceException` | 设备/通信层意外故障（连接彻底失败、驱动内部错误） | T3.x/T4.x |
  | `DeviceConfigurationException` | 设备/点表配置错误（启动校验失败） | T4.2 |
  | 其余按领域需要增补，增补前先更新本表 | | |
- 抛异常时 `ErrorCode` 必填；需要保留底层原因时用带 `innerException` 的构造。
- **禁止** catch 后 `throw ex;`（丢堆栈），重新抛出用 `throw;`。

## 5. ErrorCode 现状与扩充规划

现状（`AP.Contracts.Core/Errors/ErrorCode.cs`，共 9 个 + `None`）：

`SystemError`、`InvalidParameter`、`NotFound`、`Timeout`、`Unauthorized`、`DeviceNotConnected`、`DeviceReadFailed`、`DeviceWriteFailed`。

扩充规划（**新增为增量、不改现有常量值**；按演进任务落地，不一次性全加）：

| 错误码 | 用途 | 落地任务 |
|--------|------|----------|
| `DEVICE_CONNECTION_FAILED` | 连接尝试失败（区分"未连接"与"连不上"） | T1.x |
| `DEVICE_ADDRESS_INVALID` | 地址语法非法（驱动解析器预检） | T2.x |
| `DEVICE_NOT_SUPPORTED` | 驱动不支持的能力（替代部分 `NotSupportedException` 裸抛） | T2.x |
| `TAG_NOT_FOUND` | 点名不在点表中（编程错误场景也用于消息） | T4.2 |
| `TAG_QUALITY_BAD` | 读取成功但质量戳为坏（需要错误码表达时） | T4.3 |
| `CONFIG_INVALID` | 配置校验失败 | T4.2 |
| `DB_OPERATION_FAILED` | 数据库操作失败（重试耗尽后） | 按需 |

命名规则：`领域_具体原因`，全大写下划线；同类错误码聚拢注释分组。

## 6. 与日志的配合

- **谁处理谁记录**：异常/失败在"被处理的那一层"记一次日志；向上抛出或包装时**不再重复记**（避免同一故障刷 N 条）。
- 设备访问层把通信失败转成质量戳/Result 时记 Warning（含设备、地址/点名、原因）；UI 层消费 Result 时一般不再记日志（除非追加上下文）。
- 兜底层（GlobalExceptionHandler）记 Fatal/Error。
- 日志格式遵守 `LOGGING.md`。

## 7. 存量代码迁移约定

- 本规范自 T0.1 完成起**对全部新代码生效**。
- 存量代码不批量改造；仅在以下时机按演进计划任务迁移：驱动层随 T1.3~T2.3（各品牌接入时）、业务服务层随 T5.2（调用点迁移时）。
- 已知的主要存量差距（登记备查，不在本任务处理）：
  - 三个 PLC 驱动 `throw new Exception("读取失败...")` 裸异常（→ T1.3/T1.4/T1.5 接入 ConnectionSupervisor 时统一为 `DeviceException` 包装）。
  - `OperationResult` 仅 `ConnectDeviceCommand` 一条链路在用；`PlatformException` 零处抛出。
  - `ChangePasswordAsync` 返回元组、`LoginResult` 自定义 DTO 等非标返回风格（→ 随对应业务模块演进时评估）。

## 8. 反模式清单（代码评审直接打回）

1. `throw new Exception("...")` 裸异常。
2. `catch (Exception) { }` 空 catch 吞异常。
3. 同一故障在驱动、服务、UI 各记一条重复日志。
4. 用异常做正常流程分支（如用 try/catch 判断设备是否在线——应查状态/质量戳）。
5. Result.Fail 的 Message 写英文技术细节或堆栈。
6. 把第三方通信库的异常类型直接抛给上层。
7. `throw ex;` 重置堆栈。

---

**版本**: v1.0（2026-07-25，T0.1）
