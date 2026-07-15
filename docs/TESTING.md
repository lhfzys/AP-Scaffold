# AP-Scaffold 测试指南

本文档描述 AP-Scaffold 项目的测试架构、编写规范和运行方式。

---

## 目录

- [测试架构概览](#测试架构概览)
- [测试技术栈](#测试技术栈)
- [测试项目结构](#测试项目结构)
- [测试编写规范](#测试编写规范)
  - [命名约定](#命名约定)
  - [结构约定 (AAA)](#结构约定-aaa)
  - [断言风格](#断言风格)
- [各模块测试指南](#各模块测试指南)
  - [状态机测试](#状态机测试)
  - [生命周期管理器测试](#生命周期管理器测试)
  - [插件接口测试](#插件接口测试)
  - [事件总线测试](#事件总线测试)
  - [Attribute 测试](#attribute-测试)
- [运行测试](#运行测试)
  - [运行全部测试](#运行全部测试)
  - [运行特定项目测试](#运行特定项目测试)
  - [运行特定测试](#运行特定测试)
  - [生成测试报告](#生成测试报告)
- [CI/CD 集成](#cicd-集成)
- [测试覆盖目标](#测试覆盖目标)
- [常见问题](#常见问题)

---

## 测试架构概览

```
platform/tests/
├── AP.Core.Tests/       # 核心框架单元测试
│   ├── StateMachine/    # 状态机相关测试
│   ├── Capability/      # 能力声明相关测试
│   ├── EventBus/        # 事件总线测试
│   ├── Lifecycle/       # 生命周期管理器测试
│   └── PluginFramework/ # 插件框架测试（Metadata、Capabilities、接口）
├── AP.Shared.Tests/     # 共享库测试
│   ├── PluginSDK/       # PluginBase 测试
│   └── Utilities/       # 工具类测试
└── AP.Infra.Tests/      # 基础设施层测试
    ├── Report/          # 报表框架测试
    └── Resilience/      # 容错策略测试
```

### 分层测试策略

| 层 | 测试类型 | 目标 |
|----|---------|------|
| **AP.Core.Tests** | 纯单元测试 | 验证核心框架逻辑（状态机、生命周期、事件总线） |
| **AP.Shared.Tests** | 纯单元测试 | 验证工具类和基类功能 |
| **AP.Infra.Tests** | 单元 + 少量集成 | 验证配置解析、选项验证等 |

> 集成测试和端到端测试将在后续版本中添加。

---

## 测试技术栈

| 工具 | 用途 | 版本约束 |
|------|------|---------|
| **xUnit** | 测试框架 | v2.9.0+ |
| **NSubstitute** | Mock 框架 | v5.0.0+ |
| **FluentAssertions** | 断言库 | v6.0.0+ |
| **Microsoft.NET.Test.Sdk** | 测试 SDK | v17.0.0+ |
| **xunit.runner.visualstudio** | VS 测试运行器 | v2.0.0+ |
| **coverlet.collector** | 代码覆盖率收集 | v6.0.0+ |

## 测试项目结构

每个测试项目对应一个被测试项目：

| 测试项目 | 被测试项目 |
|----------|-----------|
| `AP.Core.Tests` | `AP.Core` |
| `AP.Shared.Tests` | `AP.Shared` |
| `AP.Infra.Tests` | `AP.Infra` |

测试项目目录结构与源项目保持一致，方便定位：

```
AP.Core.Tests/
├── StateMachine/
│   ├── StateTransitionValidatorTests.cs
│   └── PluginStateMachineTests.cs
├── Capability/
│   └── PluginCapabilitiesTests.cs
├── EventBus/
│   └── EventBusTests.cs
├── Lifecycle/
│   └── PluginLifecycleManagerTests.cs
└── PluginFramework/
    ├── PluginMetadataAttributeTests.cs
    ├── RequiresCapabilitiesAttributeTests.cs
    └── PluginInterfaceTests.cs
```

---

## 测试编写规范

### 命名约定

测试方法名遵循 `{MethodName}_{Scenario}_{ExpectedResult}` 命名模式：

```csharp
// ✅ 推荐
[Fact]
public void Constructor_InitializesEmptyState()
[Fact]
public void RegisterPlugins_LoadedPlugin_IsAdded()
[Fact]
public async Task InitializePluginsAsync_PluginFails_StateIsFailed()

// ❌ 避免
[Fact]
public void Test1()
[Fact]
public void CheckState()
```

**规范说明**：
- **MethodName**: 被测试的方法名（如 `RegisterPlugins`、`TransitionTo`）
- **Scenario**: 测试场景（如 `LoadedPlugin`、`PluginFails`、`UnknownPlugin`）
- **ExpectedResult**: 期望的结果（如 `IsAdded`、`StateIsFailed`、`ReturnsNull`）

### 结构约定 (AAA)

每个测试方法遵循 Arrange-Act-Assert 模式，使用空行分隔三个阶段：

```csharp
[Fact]
public void RegisterPlugins_LoadedPlugin_IsAdded()
{
    // Arrange
    var instance = Substitute.For<IPlugin>();
    var descriptor = CreateDescriptor("AP.Plugin.Test", isLoaded: true, instance: instance);

    // Act
    _manager.RegisterPlugins(new[] { descriptor });

    // Assert
    _manager.GetLoadedPlugins().Should().ContainSingle()
        .Which.Metadata.Id.Should().Be("AP.Plugin.Test");
}
```

### 断言风格

统一使用 **FluentAssertions** 链式断言：

```csharp
// ✅ 推荐
state.Should().Be(PluginState.Loaded);
list.Should().ContainSingle();
list.Should().HaveCount(2);
list.Should().BeEmpty();
result.Should().NotBeNull();

// ❌ 避免使用 xUnit 原生断言
Assert.Equal(PluginState.Loaded, state);
Assert.Single(list);
```

**常用断言模式**：

| 用途 | 示例 |
|------|------|
| 相等判断 | `result.Should().Be(expected)` |
| 包含判断 | `list.Should().ContainSingle()` |
| 数量判断 | `list.Should().HaveCount(3)` |
| 为空判断 | `list.Should().BeEmpty()` |
| 异常捕获 | `act.Should().ThrowAsync<InvalidOperationException>()` |
| 布尔判断 | `result.Should().BeTrue()` |

### Mock 使用规范

使用 **NSubstitute** 进行 Mock 和 Stub：

```csharp
// 创建 Mock
var logger = Substitute.For<ILogger<PluginLifecycleManager>>();
var plugin = Substitute.For<IPlugin>();

// 配置方法返回值
plugin.InitializeAsync(Arg.Any<IServiceProvider>(), Arg.Any<CancellationToken>())
    .Returns(Task.FromException(new InvalidOperationException("Init failed")));

// 验证调用
await plugin.Received(1).StartAsync(Arg.Any<CancellationToken>());
await plugin.DidNotReceive().StopAsync(Arg.Any<CancellationToken>());
```

---

## 各模块测试指南

### 状态机测试

状态机测试位于 `AP.Core.Tests/StateMachine/`，主要验证：

1. **状态转换合法性**：验证 `StateTransitionValidator` 的 CanTransition 方法
2. **状态转换流程**：验证 `PluginStateMachine.TransitionTo` 的正确性和异常行为
3. **事件通知**：验证状态变更时正确触发 `StateChanged` 事件
4. **线程安全**：验证并发条件下的状态一致性

**关键测试场景**：

```csharp
// 合法转换
[Fact]
public void CanTransition_UnloadedToDiscovered_ReturnsTrue()
{
    var result = StateTransitionValidator.CanTransition(PluginState.Unloaded, PluginState.Discovered);
    result.Should().BeTrue();
}

// 非法转换
[Fact]
public void TransitionTo_SkipState_ThrowsInvalidOperationException()
{
    var machine = new PluginStateMachine("test", _logger);
    var act = () => machine.TransitionTo(PluginState.Running);
    act.Should().Throw<InvalidOperationException>();
}

// 事件通知
[Fact]
public void TransitionTo_ValidTransition_InvokesStateChanged()
{
    // Arrange & Act
    machine.TransitionTo(PluginState.Discovered);
    // Assert
    // 验证 StateChanged 事件是否正确触发
}
```

### 生命周期管理器测试

生命周期管理器测试位于 `AP.Core.Tests/Lifecycle/`，主要验证：

1. **插件注册**：验证已加载/未加载插件的注册行为
2. **初始化流程**：验证按优先级顺序初始化、失败处理
3. **启动/停止流程**：验证完整生命周期链
4. **查询方法**：验证 GetLoadedPlugins、GetRunningPlugins、GetFailedPlugins

**测试数据准备**：

使用辅助方法创建测试用的 `PluginDescriptor`：

```csharp
private static PluginDescriptor CreateDescriptor(
    string id,
    bool isLoaded,
    IPlugin? instance,
    int priority = 100)
{
    var metadata = new PluginMetadataAttribute(id)
    {
        Name = $"Test Plugin {id}",
        Version = "1.0.0",
        Priority = priority,
    };
    var descriptor = new PluginDescriptor(
        metadata,
        typeof(IPlugin),
        null!, // PluginLoadContext - null for testing
        typeof(IPlugin).Assembly);

    descriptor.IsLoaded = isLoaded;
    descriptor.Instance = instance;
    return descriptor;
}
```

### 插件接口测试

插件接口测试位于 `AP.Core.Tests/PluginFramework/`，主要验证：

1. **插件接口方法签名**：验证 `IPlugin` 接口的方法定义
2. **生命周期方法调用**：验证 `InitializeAsync`、`StartAsync`、`StopAsync` 的正常和异常路径
3. **CancellationToken 传播**：验证取消令牌的正确传递

**异步测试注意事项**：

```csharp
// 测试方法返回 Task 或 ValueTask
[Fact]
public async Task InitializeAsync_CompletesSuccessfully()
{
    // ...
    await manager.InitializePluginsAsync(serviceProvider);
    // ...
}

// 使用 Task.FromException 模拟失败
instance.InitializeAsync(Arg.Any<IServiceProvider>(), Arg.Any<CancellationToken>())
    .Returns(Task.FromException(new InvalidOperationException("Init failed")));
```

### 事件总线测试

事件总线测试位于 `AP.Core.Tests/EventBus/`，主要验证：

1. **事件发布/订阅**：验证事件正确分发到所有订阅者
2. **命令发送/处理**：验证命令发送和响应
3. **异常处理**：验证发布者/订阅者异常不会影响其他处理
4. **取消令牌**：验证 CancellationToken 的正确传递

### Attribute 测试

Attribute 测试位于 `AP.Core.Tests/PluginFramework/`，主要验证：

**PluginMetadataAttribute 测试**：
- 构造函数正确设置 Id
- 默认值检查（Name、Version、Priority 等）
- 属性设置和获取

**RequiresCapabilitiesAttribute 测试**：
- 构造函数正确设置 RequiredCapabilities
- 位运算组合验证
- 空值/默认值检查

---

## 运行测试

### 运行全部测试

```bash
# 从解决方案根目录运行
dotnet test AP-Automation.Platform.slnx

# 或直接从 tests 目录
dotnet test platform/tests/AP.Core.Tests
```

### 运行特定项目测试

```bash
# 仅运行 Core 测试
dotnet test platform/tests/AP.Core.Tests

# 仅运行 Shared 测试
dotnet test platform/tests/AP.Shared.Tests

# 仅运行 Infra 测试
dotnet test platform/tests/AP.Infra.Tests
```

### 运行特定测试

```bash
# 按类名筛选
dotnet test platform/tests/AP.Core.Tests --filter "FullyQualifiedName~PluginLifecycleManagerTests"

# 按方法名筛选
dotnet test platform/tests/AP.Core.Tests --filter "FullyQualifiedName~RegisterPlugins_LoadedPlugin_IsAdded"

# 按类别筛选（需要添加 Trait 属性）
dotnet test platform/tests/AP.Core.Tests --filter "Category=StateMachine"
```

### 生成测试报告

```bash
# 运行测试并收集覆盖率
dotnet test platform/tests/AP.Core.Tests /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# 生成 HTML 报告（需要安装 ReportGenerator）
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:platform/tests/AP.Core.Tests/coverage.cobertura.xml -targetdir:coverage-report
```

---

## CI/CD 集成

在 CI/CD 流水线中添加测试步骤：

```yaml
# GitHub Actions 示例
- name: Run tests
  run: |
    dotnet test platform/AP-Automation.Platform.slnx `
      --logger "trx;LogFileName=test-results.trx" `
      /p:CollectCoverage=true `
      /p:CoverletOutputFormat=opencover

- name: Upload test results
  uses: actions/upload-artifact@v4
  with:
    name: test-results
    path: "**/TestResults/**"

- name: Upload coverage report
  uses: actions/upload-artifact@v4
  with:
    name: coverage-report
    path: "**/coverage.opencover.xml"
```

---

## 测试覆盖目标

| 模块 | 行覆盖率目标 | 分支覆盖率目标 | 当前状态 |
|------|-------------|---------------|---------|
| AP.Core.StateMachine | ≥90% | ≥85% | ✅ |
| AP.Core.Lifecycle | ≥90% | ≥85% | ✅ |
| AP.Core.EventBus | ≥85% | ≥80% | ✅ |
| AP.Core.PluginFramework | ≥90% | ≥85% | ✅ |
| AP.Shared.Utilities | ≥80% | ≥75% | ✅ |
| AP.Infra.Report | ≥80% | ≥75% | ✅ |
| AP.Infra.Resilience | ≥80% | ≥75% | ✅ |

---

## 常见问题

### 测试运行缓慢

- 使用 `--filter` 仅运行需要的测试
- 避免在测试中使用 `Task.Delay`，使用 `CancellationTokenSource` 进行超时控制

### Mock 接口时出现 NullReferenceException

- 确保所有构造函数参数都被 Mock 或提供有效值
- 检查 `PluginDescriptor` 构造函数参数不需要 null

### 异步测试超时

- 检查是否有死锁（如同步阻塞异步方法）
- 确保 `CancellationToken` 正确传递

### 测试不通过，但与代码行为一致

1. 确认测试场景与被测试代码的行为匹配
2. 如果代码行为正确，更新测试而不是代码
3. 记录行为变更的原因和影响范围

---

## 参考资料

- [架构设计文档](ARCHITECTURE.md) — 理解各模块的内部逻辑
- [使用指南](GETTING_STARTED.md) — 环境准备和配置
- [xUnit 文档](https://xunit.net/docs/getting-started/netcore/cmdline)
- [NSubstitute 文档](https://nsubstitute.github.io/help/getting-started/)
- [FluentAssertions 文档](https://fluentassertions.com/introduction)

---

**最后更新**: 2026-07-14