\# AP-Scaffold (工业自动化通用平台脚手架)



基于 .NET 8 + WPF + Prism + MediatR 的高扩展性工业软件底座。专为快速构建上位机、MES 客户端、产线监控系统设计。



\## 🚀 核心特性



\- 架构：插件化架构 (Plugin-based)，强制解耦核心与业务。

\- 通信：内置三菱 PLC (MC协议)、串口扫码枪、gRPC 服务端客户端支持。

\- 机制：

&nbsp; - MediatR：进程内消息总线，模块间 0 引用。

&nbsp; - Polly：工业级容错策略（自动重连、断路器）。

&nbsp; - Prism：MVVM 导航与依赖注入。

\- UI：Material Design 风格，内置 Loading、Dialog、Toast 等交互组件。



\## 🛠️ 快速开始 (新项目流程)



\### 1. 准备工作

&nbsp;Visual Studio 2022 (需安装 .NET 8 SDK)

&nbsp;也就是本仓库代码



\### 2. 启动配置 (`AP.Host.Desktop`)

打开 `appsettings.json`，根据现场硬件修改配置：

```json

Plugins {

&nbsp; Configuration {

&nbsp;   AP.Plugin.Plc.Mitsubishi { IpAddress 192.168.1.10, Port 6000 },

&nbsp;   AP.Plugin.Scanner { PortName COM3 }

&nbsp; }

}

