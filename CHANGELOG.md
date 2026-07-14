# Changelog

本文件记录 AP-Scaffold 项目所有重要变更。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，
版本管理遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

---

## [Unreleased]

### 新增

- **安全模块**：新增 `AP.Contracts.Security` / `AP.Infra.Security`
  - 本地用户/角色/权限体系（`IIdentityService`、`IUserRepository`、`IPasswordHasher`）
  - PBKDF2 + 随机盐值密码哈希
  - 审计日志服务 `IAuditService`
  - 启动时自动初始化默认角色、权限和 `admin` 账号
- **配方管理**：新增 `AP.Contracts.Recipe` / `AP.Infra.Recipe`
  - 配方增删改查、版本控制、默认配方、配方切换
  - 首次启动自动创建 `DEFAULT` 默认配方
- **启动画面**：`SplashWindow` 显示启动进度，各初始化阶段实时更新
- **系统托盘**：`TrayIconManager` 支持最小化到托盘、显示主窗口、重启、退出
- **安装包**：新增 `installer/setup.iss`（Inno Setup 脚本）及构建说明
- **全局异常保护**：移除弹窗，改为仅写入崩溃日志 `logs/crash-yyyyMMdd.log`
- **API 文档**：新增 `docs/ARCHITECTURE.md` 架构设计文档
  - 完整的分层架构概览与调用链说明
  - 核心框架（Core）各模块详细文档：插件框架、生命周期、状态机、能力声明、事件总线
  - 基础设施层各模块详细文档：数据库、gRPC、日志、容错、报表
  - 启动宿主启动流程图、运行时硬件通信数据流
  - 扩展点文档：添加新插件和新基础设施模块的步骤

- **测试文档**：新增 `docs/TESTING.md` 测试指南
  - 测试技术栈说明（xUnit + NSubstitute + FluentAssertions）
  - 测试编写规范（AAA 模式、命名约定、断言风格）
  - 各模块测试指南（状态机、生命周期、插件接口、事件总线、Attribute）
  - 测试运行命令、CI/CD 集成示例、覆盖率目标

- **状态机增强**：新增 `Frozen`（已冻结）和 `Deprecated`（已废弃）状态
  - 完整的状态枚举定义（13 个状态）
  - 对应更新 `StateTransitionValidator` 转换规则

### 变更

- 初始项目结构搭建完成
- 完善测试项目结构：AP.Core.Tests、AP.Shared.Tests、AP.Infra.Tests
- 测试覆盖核心框架关键路径
- 修复 `PluginLifecycleManager.RegisterPlugins` 未按优先级排序的问题
- 修复 `ConfigurationHelper.UpdateAppSetting` 空 section 未抛异常的问题
- 修复测试项目 CPM 版本管理配置不一致的问题
- 安全模块改为可选：`Security:Enabled` 配置开关，关闭时跳过用户/角色/权限表初始化并注入匿名实现
- 配置界面改为独立模态弹窗：`SettingsDialogWindow` + `ISettingsDialogService`，替代原右侧抽屉模式
- 修复 `ConfigurationHelperTests` 使用 .NET 9 才有的 `Type.IsStatic` 导致 net8.0 编译失败的问题

### 技术栈

- .NET 8（目标框架），使用 .NET 10 SDK 构建
- WPF + Prism + DryIoc
- FreeSql (SQLite / PostgreSQL)
- gRPC (ASP.NET Core gRPC)
- MediatR (事件总线)
- Serilog (结构化日志)
- Polly (容错策略)
- MiniExcel (报表生成)
- xUnit + NSubstitute + FluentAssertions (测试)

---

## [0.1.0] - 2026-07-13

### 新增

- **项目初始化**：创建解决方案 `AP-Automation.Platform.slnx`
- **核心框架 (AP.Core)**
  - 插件框架：`IPlugin` 接口、`PluginMetadataAttribute`、`RequiresCapabilitiesAttribute`
  - 插件加载：`PluginLoader` 扫描加载、`PluginAssemblyLoadContext` 隔离上下文
  - 状态机：`PluginStateMachine`、`StateTransitionValidator`、14 种状态
  - 生命周期管理器：`PluginLifecycleManager`（注册/初始化/启动/停止）
  - 事件总线：`IEventBus` 接口 + MediatR 实现
  - 能力声明：`PluginCapabilities` 位标志枚举（14 项能力 + 4 种预定义组合）
  - 应用角色：`AppRole` 位标志枚举（Client / Server / Standalone）
  - DI 扩展方法：`AddCoreServices`
- **共享库 (AP.Shared)**
  - `PluginBase` 基类
  - UI 控件：`LoadingSpinner`、`MaterialDialog`、`Toast`、主题样式
  - 工具类：`SerializationHelper`、`ConfigurationHelper`
- **契约层 (AP.Contracts)**
  - `AP.Contracts.Core`：核心事件、错误模型
  - `AP.Contracts.Hardware`：硬件接口、设备事件
  - `AP.Contracts.Communication`：gRPC 消息协定
  - `AP.Contracts.System`：系统服务接口
- **基础设施层 (AP.Infra)**
  - `AP.Infra.Database`：FreeSql Repository 实现（SQLite + PostgreSQL）
  - `AP.Infra.Grpc`：gRPC Server/Client、`StreamBroadcaster`、`LoggingInterceptor`
  - `AP.Infra.Logging`：Serilog 结构化日志配置
  - `AP.Infra.Resilience`：Polly 策略工厂（可配置重试）
  - `AP.Infra.Report`：报表框架（生成/归档/清理）
- **启动宿主 (AP.Host.Desktop)**
  - 角色感知启动（Server / Client / Standalone）
  - Prism + DryIoc DI 容器配置
  - 插件自动扫描加载
- **插件**
  - `AP.Plugin.Plc.Mitsubishi`：三菱 PLC MC 协议驱动
  - `AP.Plugin.Scanner`：串口扫码枪驱动
  - `AP.Plugin.AirtightnessCheck`：气密性检测业务
  - `AP.Plugin.DeviceConfiguration`：设备参数配置
  - `AP.Plugin.Layout`：布局管理
- **测试项目**
  - `AP.Core.Tests`：状态机、生命周期、事件总线、插件框架测试
  - `AP.Shared.Tests`：PluginBase、工具类测试
  - `AP.Infra.Tests`：报表、容错策略测试
- **文档**
  - `docs/GETTING_STARTED.md`：使用指南

---

## 格式说明

- `新增` 新功能
- `变更` 已有功能的变更
- `废弃` 即将移除的功能
- `移除` 已移除的功能
- `修复` 问题修复
- `安全` 安全修复

---

**最后更新**: 2026-07-13