# 菜单导航重构计划

> **状态**：方案已确认，待执行代码修改。  
> **选择方案**：声明式导航贡献者模式（方案 A）。

## 背景与问题

当前菜单导航存在两个核心问题：

1. **关闭登录/权限系统后菜单显示不全**  
   当 `Security:Enabled=false` 时，`SidebarViewModel` 中每条菜单的可见性都写成：
   ```csharp
   var canViewRecipe = securityEnabled && _identityService.HasPermission("recipe.view");
   ```
   这导致 `securityEnabled=false` 时所有业务菜单（配方、报表、审计、用户/角色）的 `IsVisible` 都被强制设为 `false`，只剩下默认未设置 `IsVisible` 的"系统配置"。

   而系统实际上已经提供了 `AnonymousIdentityService`，其 `HasPermission` 固定返回 `true`，本可以正确处理"无权限系统"场景。

2. **菜单硬编码，扩展困难**  
   `SidebarViewModel` 直接写死了全部导航项（图标、标题、目标视图、权限）。新增 Dashboard/首页/业务模块时，必须修改 Layout 插件，违反"插件自治"原则。

## 目标

- 修复 `Security:Enabled=false` 时业务菜单被错误隐藏的问题。
- 引入声明式导航贡献者机制，让插件自己决定提供哪些菜单项。
- 支持通过配置指定默认首页/仪表盘，便于真实项目落地。
- 保持对现有插件和权限行为的最小侵入。

## 方案详情

### 步骤 1：修复权限判断 bug

- **文件**：`platform/plugins/system/AP.Plugin.Layout/ViewModels/SidebarViewModel.cs`
- **修改**：移除 `securityEnabled &&` 前缀，直接调用 `_identityService.HasPermission(...)`。
- **原因**：当 `Security:Enabled=false` 时，`IIdentityService` 由 `AnonymousIdentityService` 实现，所有权限检查返回 `true`，菜单自然全部可见。

### 步骤 2：引入 `INavigationContributor` 接口

- **新增文件**：`platform/shared/AP.Shared.PluginSDK/Navigation/INavigationContributor.cs`
- 与 `ISettingsContributor` 并列，保持插件 SDK 扩展风格一致：
  ```csharp
  public interface INavigationContributor
  {
      IEnumerable<NavigationMenuItem> GetMenuItems();
  }
  ```

### 步骤 3：统一 `NavigationMenuItem` 模型

- **新增文件**：`platform/shared/AP.Shared.PluginSDK/Navigation/NavigationMenuItem.cs`
- 字段包括：
  - `Label`：显示文本
  - `IconKind`：Material Design 图标
  - `NavigationTarget`：目标视图名
  - `Order`：排序权重（越小越靠前）
  - `Permission`：可选权限码
  - `Category`：分组（可选，未来支持二级菜单/抽屉分组）
  - `NavigationParameters`：导航参数（可选）
  - `IsDefault`：是否作为启动默认页

### 步骤 4：改造 `SidebarViewModel`

- **文件**：`platform/plugins/system/AP.Plugin.Layout/ViewModels/SidebarViewModel.cs`
- **修改点**：
  - 注入 `IEnumerable<INavigationContributor>`。
  - 收集所有贡献者的菜单项，按 `Order` 排序，以 `NavigationTarget` 去重。
  - 根据 `IIdentityService.HasPermission(Permission)` 决定 `IsVisible`。
  - 默认选中逻辑：优先 `IsDefault=true` 的项，其次读取 `AppConfiguration:DefaultNavigationTarget`，最后回退到第一个可见项。
  - 保留 `OnSelectedItemChanged` 中的权限二次校验。

### 步骤 5：各插件实现 `INavigationContributor`

以下插件需要在其 `*Plugin.cs` 中实现 `INavigationContributor`：

| 插件 | 菜单项 | 目标视图 | 建议权限 | 建议 Order |
|------|--------|----------|----------|------------|
| AP.Plugin.SystemSettings | 系统配置 | `SettingsShellView` | `system.view` | 1000 |
| AP.Plugin.RecipeManagement | 配方管理 | `RecipeListView` | `recipe.view` | 2000 |
| AP.Plugin.ReportCenter | 报表中心 | `ReportListView` | `report.view` | 3000 |
| AP.Plugin.DeviceConfiguration | 设备配置 | `DeviceConfigurationShellView`（如存在）或合并到系统配置 | `device.config` | 2500 |
| AP.Plugin.UserManagement | 用户管理 | `UserListView` | `user.manage` | 4000 |
| AP.Plugin.RoleManagement | 角色管理 | `RoleListView` | `role.manage` | 4100 |
| AP.Plugin.AuditLog | 审计日志 | `AuditLogListView` | `audit.view` | 4200 |

插件仍负责在 `InitializeAsync` 中把对应视图注册到 `ContentRegion`。

### 步骤 6：新增 Dashboard/首页插件（示例）

- **可选新增项目**：`AP.Plugin.Dashboard`
- 实现 `INavigationContributor`，提供"首页/Dashboard"菜单项，目标视图 `DashboardView`。
- 在 `appsettings.Standalone.json` / `appsettings.json` 中可配置默认页：
  ```json
  "AppConfiguration": {
    "DefaultNavigationTarget": "DashboardView"
  }
  ```

### 步骤 7：测试覆盖

- 更新/新增 `AP.Shared.Tests` 中导航相关测试：
  - `Security:Enabled=false` 时所有菜单可见。
  - 权限过滤正确（无权限时隐藏）。
  - 多个 `INavigationContributor` 按 `Order` 排序。
  - 默认首页选中逻辑。

## 实施范围清单

### 修改文件

- `platform/plugins/system/AP.Plugin.Layout/ViewModels/SidebarViewModel.cs`
- `platform/plugins/system/AP.Plugin.Layout/Models/NavigationItem.cs`（可复用或标记为弃用）
- 各系统插件 `*Plugin.cs`
- `platform/hosts/AP.Host.Desktop/Configuration/appsettings*.json`：补充 `DefaultNavigationTarget` 示例

### 新增文件

- `platform/shared/AP.Shared.PluginSDK/Navigation/INavigationContributor.cs`
- `platform/shared/AP.Shared.PluginSDK/Navigation/NavigationMenuItem.cs`
- 可选：`platform/plugins/system/AP.Plugin.Dashboard/` 首页插件示例

### 测试

- 更新/新增 `platform/tests/AP.Shared.Tests/` 中导航相关单元测试。
- 手动验证：
  1. `Security:Enabled=false` 时，系统配置、配方、报表、用户/角色/审计全部可见。
  2. `Security:Enabled=true` 时，按登录用户权限正确显示/隐藏。
  3. 新增 Dashboard 插件后，菜单自动出现并可选为默认页。

## 验证命令

全部修改完成后，运行：

```bash
dotnet build AP-Automation.Platform.slnx -c Release
dotnet test platform/tests/AP.Core.Tests/AP.Core.Tests.csproj -c Release --no-build
dotnet test platform/tests/AP.Shared.Tests/AP.Shared.Tests.csproj -c Release --no-build
dotnet test platform/tests/AP.Infra.Tests/AP.Infra.Tests.csproj -c Release --no-build
```

测试通过后提交并推送到 `origin main`。
