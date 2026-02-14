namespace AP.Core.Capability;

/// <summary>
/// 插件能力声明 (细粒度权限控制)
/// </summary>
[Flags]
public enum PluginCapabilities
{
    None = 0,

    // --- 基础能力 ---
    ReadConfiguration = 1 << 0, // 读取配置
    WriteConfiguration = 1 << 1, // 写入配置
    AccessDatabase = 1 << 2, // 访问数据库
    AccessFileSystem = 1 << 3, // 访问文件系统
    AccessNetwork = 1 << 4, // 访问网络

    // --- 硬件能力 ---
    AccessPLC = 1 << 5, // 访问 PLC
    AccessSerialPort = 1 << 6, // 访问串口
    AccessCamera = 1 << 7, // 访问相机

    // --- UI 能力 ---
    RegisterViews = 1 << 8, // 注册视图 (Prism Region)
    ShowDialogs = 1 << 9, // 显示弹窗

    // --- 通信能力 ---
    PublishEvents = 1 << 10, // 发布事件 (MediatR)
    SubscribeEvents = 1 << 11, // 订阅事件
    CallGrpcServices = 1 << 12, // 调用 gRPC
    ProvideGrpcServices = 1 << 13, // 提供 gRPC 服务

    // --- 预定义组合 (方便开发使用) ---

    /// <summary>
    /// 只读数据访问
    /// </summary>
    ReadOnly = ReadConfiguration | AccessDatabase,

    /// <summary>
    /// 标准业务插件 (数据库 + UI + 事件)
    /// </summary>
    Standard = ReadConfiguration | AccessDatabase | PublishEvents | SubscribeEvents | RegisterViews,

    /// <summary>
    /// 硬件驱动插件
    /// </summary>
    Hardware = Standard | AccessPLC | AccessSerialPort | AccessNetwork,

    /// <summary>
    /// 全权限 (谨慎使用)
    /// </summary>
    FullAccess = ~0
}