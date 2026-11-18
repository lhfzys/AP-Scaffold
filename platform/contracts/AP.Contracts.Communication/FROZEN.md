# ❄ 已冻结：gRPC 通信契约

本目录（`AP.Contracts.Communication`）已从 `AP-Automation.Platform.slnx` 主解决方案移除，不再被宿主引用、不再参与构建/发布/安装包。

- 冻结日期：2026-11-18（DH-6）
- 原因：项目范围收敛为 Standalone 单机模式；保留 proto 仅作未来解冻参考。
- 解冻条件：出现明确的多机/Server-Client 需求，需专项评审安全、部署与版本兼容后再恢复到解决方案。
- 注意：源文件保留在仓库中，但**不要**把它加回主解决方案或 Host 引用，除非执行上述评审。
