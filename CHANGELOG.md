# Changelog

本文件记录 AP-Scaffold 项目所有重要变更。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，
版本管理遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

---

## [Unreleased]

### 仪表板重排与实时趋势图（2026-08-03）

新增：

- **实时采集趋势图**（LiveCharts2，SkiaSharp 渲染）：Dashboard 第二行左侧新增趋势卡，取点表前 4 个数值型点，2s 采样自 `LatestTagValueStore`（仅 Good 质量入缓冲），内存环形缓冲保留近 60 分钟（1800 点/序列），X 轴 10 分钟刻度 `HH:mm`、Y 轴下限 0、首序列带渐变填充、底部图例；无数值型点时显示占位提示。时间范围切换预留 1H/24H/7D/30D 按钮位（仅 1H 可用，其余提示"需历史数据持久化（未启用）"，停车场项）
- **状态栏系统监控**：新增 `ISystemMonitorService` 实现（`SystemMonitorService`，Layout 插件内，PerformanceCounter 整机 CPU + 进程工作集内存，首次采样无效显示 `--`）；状态栏左区追加"CPU x% · 内存 xMB"（每 2s）与数据库连通探测（`select 1`，每 30s，已连接/异常/未启用），右区时间前加版本号（取入口程序集 `InformationalVersion`，`v1.0.0` 格式）
- **最近事件级别徽标**：事件条目新增级别（Info/Success/Warning/Error → 信息/成功/警告/严重），左侧 8px 级别色点 + 右侧级别文字同色；设备状态映射（Connected→成功、Connecting/Reconnecting→信息、Disabled→警告、其余→严重），Tag 质量异常→警告

变更：

- **统计卡重排**：4 张 KPI 卡由 WrapPanel 改 4 列等宽 Grid（填满整行）；卡内改为"左文字栈 + 右上 36×36 圆角图标块（主题色 12% 底 + 同色图标）"，数字 28px 加粗带小字后缀（`/2 台`、` 分钟`），底部状态徽标为色点+文字（全部在线/N 台离线/采集中/全部停止/系统刚启动，按 ok/warn/err/none 映射语义色）
- **LiveCharts2 走宿主共享库模式**（同 MaterialDesign）：宿主 `AP.Host.Desktop` 直接引用包（程序集落在输出根目录供 Default ALC 解析——插件 XAML 的 BAML 引用经此路径），`PluginLoadContext.SharedPrefixes` 新增 `LiveChartsCore`/`SkiaSharp`/`OpenTK`，`CleanDuplicateLibs` 删除插件目录私有副本（含对应原生库），避免插件 ALC 私载第二份导致 VM/BAML 类型不一致。注：2.0.0-rc6.1 曾以强名引用在插件 ALC 内侥幸自洽，2.0.4 正式版不再强名（BAML 引用为部分名）触发解析失败，故归位共享库模式；`SkiaSharp.Views.WPF` 仅提供 net462 资产（NU1701 警告，.NET 8 运行无碍）

依赖：新增 `LiveChartsCore.SkiaSharpView.WPF` 2.0.4、`System.Diagnostics.PerformanceCounter`（Layout 插件编译引用 + 宿主共享引用）

### 修复：配置写回与分层加载不一致（2026-07-31）

修复：

- **设置中心保存的配置永不生效（只存在于角色文件的节）**：配置按 `appsettings.json` + `appsettings.{Role}.json` 分层加载（角色文件优先），而 `SettingsService` 写回时写死 `appsettings.json`——扫码枪整节只在角色文件中，此前经界面保存的串口参数实际从未生效（写入了基文件被遮蔽）。现按节解析写回目标：角色文件含该节 → 写角色文件，否则写基文件（`ConfigurationHelper.ResolveTargetFileName`；宿主启动时经 `AppRuntime:RoleConfigFile` 暴露活动角色文件名）；备份同步覆盖各目标文件
- 附注：此前误写入 `bin/Release/Configuration/appsettings.json` 的遮蔽节（`Plugins:Configuration:AP.Plugin.Scanner`）随重新构建已被清理

测试：471 → 475（`ConfigurationHelperTests` 新增 4 项）

### 扫码枪可整体禁用（2026-07-31）

新增：

- **`Plugins:Configuration:AP.Plugin.Scanner:Enabled` 开关**（默认 `true`）：置 `false` 后插件不注册 `IScannerService`/`IDevice`、不发起串口连接——无扫码枪的项目不再出现启动"初始化失败"报错，状态栏/设备注册表也不再显示常驻离线设备；系统配置"扫码枪配置"页同步新增"启用扫码枪"勾选（RequiresRestart）
- 注意：禁用后点表中引用 `scanner.*` 设备的点会在启动校验时报"设备未注册"（快速失败设计），需将该类点一并移除

### 点表可视化编辑（2026-07-31，AP.Plugin.TagConfiguration）

新增：

- **点表配置插件**（第 13 个插件，`plugins/business/`）：菜单"点表配置"（Order=1100，权限 `device.config`，免登录白名单已加入）——tags.json 列表查看/新增/编辑/删除/保存，设备下拉取自 `IDeviceRegistry`，默认采集周期与按点覆盖可编辑（改名/删除自动清理孤儿覆盖项）；保存前经 `ITagTableValidator` 全量校验（**与启动加载同一规则，非法点表不落盘**），原子写入并补写头部标准注释，保存写审计日志，重启后生效；编辑窗确定时即对候选点表预检，错误留在窗内提示
- **契约 `ITagTableValidator`** + Infra `TagTableValidator` 实现：`TagTable` 的校验逻辑抽取为共用的 internal `TagTableValidation`（规则与错误文案不变，启动快速失败行为不变），编辑器与启动加载零重复

变更：

- 免登录导航白名单（`appsettings.json` / `appsettings.Standalone.json`）加入 `TagTableListView`

测试：465 → 471（新增 `TagTableValidatorTests` 6 项；`TagTable` 现有测试不动全绿）

明确不做：保存后热重载（采集引擎重建链，记停车场）；点表导入导出

### UI 一致性优化·第四批次（2026-07-29，审计筛选修复与主题归位）

修复：

- **审计日志操作类型筛选实际失效**：下拉 `ItemsSource` 误绑枚举 `Type` 对象（恒显示类型全名、选中值无法回传），改为 `ActionTypeOption` 列表（首项"全部"+ 10 个中文项），筛选与重置恢复正常
- 审计列表"操作类型"列直绑枚举显示英文，新增 `AuditActionTypeDisplayConverter` 统一中文显示（映射与筛选项共用 `AuditActionTypeDisplay`）

变更：

- **弹窗图标色归位主题**：`MaterialDialogService` 三处硬编码 `Brushes.DodgerBlue/Orange/Red` 改取 `Brush.Info/Warning/Error`（主题未加载时回退原色）
- **分隔线统一**：`MaterialDesignDivider` 13 处（7 个文件：用户/角色/配方编辑窗、设置中心两处、报表中心、Dashboard）全部改 `Brush.Outline`
- **Sidebar 选中/悬停色归位既有主题键**：选中背景 `Brush.Sidebar.Selected.Background`、选中前景 `Brush.Sidebar.Selected.Foreground`（原字面量 `White`）、悬停 `Brush.Sidebar.Hover.Background`；删除第三批次引入的重复键 `Color.NavHover/Brush.NavHover`（与 `Sidebar.Hover.Background` 同值）
- 界面文案标点全角化扫尾 9 处：设置保存失败/审计描述、扫码完成事件、用户/配方删除确认括号、头部用户名括号、扫码枪/PLC 配置示例冒号

### UI 一致性优化·第三批次（2026-07-29，头部时间位与状态栏）

新增：

- **底部状态栏 `StatusBarView`**（两个布局共用，DataContext 继承布局 VM）：左=设备在线 X/Y + 四态状态点（全在线/部分/全离线/无设备，真实数据 `IDeviceRegistry` + `PrismDeviceStateChangedEvent` 实时刷新）、中=公司名、右=当前时间；头部右上角秒级跳表移除（时间归位状态栏，工业 HMI 标准布局），免登录时头部右侧改显公司名（启用原死属性 `CompanyName`）

变更：

- **免登录匿名标识清零**：Sidebar 底部用户卡按 `Security:Enabled` 显隐（此前免登录显示 `anonymous/Administrator`，头部已隐藏此处漏改）；Dashboard 问候语 anonymous 回退"操作员"，"你好, " 半角逗号改全角，问候语去 emoji（工业统一视觉）
- 主题新增语义键 `Color.Outline/Brush.Outline`（#DDE2E8 分隔线）；`SinglePageLayoutView`/`StandardLayoutView`/`SidebarView` 四处硬编码颜色全部归位主题键（导航悬停直接使用既有 `Brush.Sidebar.Hover.Background`）

### 打包治理（方案 A：框架依赖发布，2026-07-29）

变更：

- **测试产物输出隔离**：3 个 `.Tests` 项目输出从共享的 `bin/Release` 根迁到 `bin/Tests/{项目名}/`（`Directory.Build.props` 新增覆盖规则），发布根目录不再混入 TestPlatform/xunit/coverlet 与 12 个语言卫星目录；465 测试复跑全绿
- **发布形态定为 win-x64 框架依赖**：发布目录 26MB/227 文件、安装包 7.7MB（自包含方案 217MB 否决——体积观感差）；宿主 csproj 固定 `PublishDir=bin\Release\publish\`；发布命令带 `-p:AppendRuntimeIdentifierToOutputPath=true`，RID 构建落入 `bin/Release/win-x64/` 子目录，不再污染开发目录（此前混入自包含文件导致 `bin/Release` 下 exe 无法启动）
- **`setup.iss`**：运行时检测从仅桌面运行时扩展为**桌面 + ASP.NET Core 双运行时**（宿主经 `FrameworkReference Microsoft.AspNetCore.App` 声明，只装桌面运行时会启动失败），缺失项在提示中列出；**修复检测恒判缺失的隐患**——.NET 版本是以"值"（值名=版本号）挂在注册表键下的而非子键，原脚本 `RegGetSubkeyNames` 永远数不到，已改 `RegGetValueNames` 并补 `HKLM64` 64 位视图兜底（原版即带此缺陷，本机实测复现+探针验证修复）；简体中文语言文件 `ChineseSimplified.isl` 改为随仓库携带（`installer/Languages/`，官方发行版不含中文，要求 Inno ≥ 6.5）；头部注释更新为两步构建流程
- `.gitignore` 补 `installer/Output/`；`installer/README.md` 重写（两步流程、现场装机两个运行时及静默安装命令、体积数据、禁项清单）
- 框架依赖发布产物已冒烟验证：ISCC 编译通过、publish exe 启动至主窗口存活、13 插件全部初始化、无致命异常

禁项（2026-07-29 实测踩坑，已记入 README/AGENTS）：

- 勿开 `PublishReadyToRun`：R2R 与 WPF 混合程序集 `DirectWriteForwarder` 冲突，启动即 `TypeLoadException`
- 暂勿自包含发布：`PluginLoadContext`（可回收）会把 `DirectWriteForwarder` 装入插件上下文崩溃（`Collectible type '<Module>' ... FixedAddressValueTypeAttribute`）；框架依赖因该文件不在应用目录、解析回退默认上下文而规避。如需自包含，须先修插件加载上下文框架程序集回退策略

### 架构演进（2026-07-25 ~ 2026-07-28，阶段 0~6 全部 33 个任务 + 停车场第一项）

新增：

- **Tag 系统（阶段 4）**：业务层按逻辑点名访问设备数据——Tag 模型契约（`TagValue` 质量戳/`TagDefinition`/`TagAccess`）、点表 JSON 加载+启动快速失败校验（`IAddressValidator`）、`ITagService` 按名读写（通信失败返回 Bad 质量戳不抛异常）、采集引擎+最新值表（按生效间隔分组轮询）、`TagValueChangedEvent` 变化才通知；Dashboard 全部接真实数据（设备状态/采集点/Tag 变化/真实事件流），占位数据清零
- **Device 抽象（阶段 3）**：`IDevice`/`DeviceInfo`/`DeviceType` 契约、`DeviceRegistry`（大小写不敏感）、PLC 经 `PlcDeviceAdapter` 接入、扫码枪迁入统一监督器（第四套独立重连消除）、统一 `DeviceStateChangedEvent`（旧四事件经迁移边桥接并行保留）
- **连接监督收敛（阶段 1）**：`DeviceConnectionStateMachine`（六态）+ `ConnectionSupervisor`（心跳/重连退避/监督自愈，纯事件源），三菱/西门子/欧姆龙三驱动接入并删除逐行复制的看门狗；看门狗参数配置化（`Plc` 节 3 个可选键）
- **地址领域对象（阶段 2）**：`McAddress`/`S7Address`/`FinsAddress`（解析/规范化/结构化错误/值相等），读写预检接入；地址语法只允许存在于驱动内部
- **带类型批量读（停车场 TP1~TP3）**：`IPlcTypedBatchRead` 契约 + 三品牌实现（西门子/欧姆龙真批量，三菱循环逐条按类型分发）+ 采集引擎在线 PLC 每周期一次往返（三级降级）
- **首个真实报表**：操作审计日报 `AuditDailyReportProvider`（数据源=审计日志），替换示例注册
- **规范文档（阶段 0/5/6）**：`docs/conventions/` 新增 ERROR_HANDLING / LOGGING / LAYERING / DEPENDENCIES 四份规范

修复：

- **优雅关闭死锁真因**：Dashboard 事件处理器 `Dispatcher.Invoke` 双向等待 → `BeginInvoke`；停止路径无界同步 `Close` 套 2 秒有界等待
- **启动时序缺陷**：设备注册/点表加载必须先于插件初始化

变更：

- gRPC/Server/Client 封存代码显式标注 ❄（行为不变）；三 PLC 驱动日志按 LOGGING.md 规范清理
- 删除死引用：`Contracts.Core→AP.Core`、三 PLC 插件→`AP.Shared.UI`
- 测试规模：237 → 465（3 个测试项目）

### 系统设置（2026-07-24）

修复：

- **PLC 品牌切换残留旧品牌参数**：切换驱动类型时端口/型号/心跳地址强制回填为新品牌默认值（此前仅在为空时回填，残留的心跳地址格式不匹配会导致看门狗误判掉线、反复重连）；加载已有配置仍显示保存值（`DriverType` 先赋值触发回填、保存值随后覆盖，末尾不再二次回填）
- **清理 PLC 独立配置节死配置**：移除 `appsettings.Standalone.json` 中 `Plugins:Configuration:AP.Plugin.Plc.Mitsubishi` 段（统一到 `Plc` 节后无任何代码读取；扫码枪段为活配置保留）

### 欧姆龙 PLC 驱动（2026-07-24）

新增：

- **`AP.Plugin.Plc.Omron`**：欧姆龙 FINS/TCP 驱动插件（Priority=22，Server|Standalone，Required=false），结构对齐三菱/西门子：统一 `Plc` 配置节切换（`DriverType=Omron`，端口默认 9600、心跳默认 `D0`）、IoTClient `OmronFinsClient`（字节序 CDAB）、看门狗监督重连、`IPlcDriverFactory` 注册即插即用。限制（IoTClient 未实现，代码已注释）：字符串读写抛 `NotSupportedException`；批量写入退化为逐条写入；批量读为真批量（`BatchRead` 第二参数无实际效果）

### UI 一致性优化·第二批次（2026-07-24）

修复：

- **DataGrid 单元格文本不垂直居中**：根因为 MD 5.3.0 `MaterialDesignDataGridCell` 模板的 ContentPresenter 不消费 `VerticalContentAlignment`（Grid 内默认拉伸，文本贴顶渲染），且 `DataGridAssist` 强制套用的列 ElementStyle 会屏蔽单元格资源中的隐式 TextBlock 样式——此前两条路径均无效。最终覆写单元格 Template（ContentPresenter 绑定 `VerticalContentAlignment`；水平保持 Stretch，保证编辑控件仍能填满单元格），44px 行高下所有网格内容垂直居中
- **Raised 按钮白字看不清（浅色主题下灰白底）**：新增 `RaisedButton.Primary` 样式（`App.xaml`，BasedOn `MaterialDesignRaisedButton` + `Brush.Primary` 深蓝底 + `Brush.OnPrimary` 白字），14 处 Raised 引用统一换键；主题文件补 `Color.OnPrimary #FFFFFF` / `Brush.OnPrimary` 语义键。教训：同名键 BasedOn 自引用会被静默置空丢模板，覆写样式必须另起键名
- **未登录时 Header 用户区冗余**：用户标识+退出按钮整体按 `CanLogout`（即 `Security:Enabled`）显隐，免登录不再显示"系统用户(anonymous)+置灰退出"；用户标识由 md:Chip 改为 `PrimaryHueDarkBrush` 圆角底衬 + 白色图标/文字

变更：

- 配方管理 DataGrid「更新时间」列加宽（140→160），时间完整显示无需手动拉宽

### UI 一致性优化（2026-07-22）

变更：

- **Dashboard 移除快捷入口**：导航职责统一归 Sidebar（快捷入口此前无权限过滤，无权限用户点击静默无反应）；「最近事件」卡片整行布局；硬编码分隔色 `#E5E7EB` → `MaterialDesignDivider`
- **编辑窗口统一 Owner+CenterOwner**：用户/角色/配方编辑窗挂接主窗口并随其居中，与 Login/Settings 窗口行为一致
- **视图级权限防线统一**：UserEdit/RoleEdit 保存按钮补 `user.manage`/`role.manage` 门控；PLC/应用设置编辑器根元素补 `system.settings` 门控；SettingsShell「取消」按钮移除门控（此前无权限用户连取消都被禁用）
- **加载反馈收敛**：`LoadingSpinner` 改浅色遮罩（`Brush.Surface` 0.85 + `TextStyle.Body`）并成为唯一遮罩控件，替换 6 处手写遮罩副本（5 个列表页 + MainWindow）；设置保存、用户/角色/配方保存、登录、改密补遮罩反馈与 BusyText 文案；报表打开/导出补 `IsBusy`
- **DataGrid 样式全局收敛**：5 项公共属性并入 `App.xaml` 隐式样式（修复各页显式 `Style=` 将虚拟化 4 项设置屏蔽为死配置的缺陷）；6 个 DataGrid 声明删除重复抄写，只留差异属性
- **Splash**：深色启动页作为主题例外刻意保留；移除英文公司行（"Industrial Automation"），软件名居中布局

### 默认配置变更（2026-07-22）

变更：

- **`Security:Enabled` 默认改为 `false`（免登录）**：面向单机外包场景，默认不弹登录窗、直接进入主界面；`Security:Audit:Enabled` 显式保留 `true`，PLC 写操作/配置修改等审计在免登录下继续工作（操作人记匿名/`system`）；审计表改由 `AuditService` 构造函数幂等自建（`SyncStructure<AuditLog>`），不再依赖仅 Security 启用时注册的初始化器

### 阶段二加固（2026-07-22）

修复：

- **关闭流程卡死、进程残留**：`App.OnExit` 改为线程池执行优雅关闭 + 15s 硬上限（消除 UI 线程 sync-over-async 卡死）；`PluginLifecycleManager` 单插件停止增加 5s 独立超时，单个插件卡死不再拖死整个关闭序列；托盘双击在窗口已关闭时不再抛异常；清理 `bin/Release/plugins` 中气密残留插件与 `_wpftmp` 构建垃圾（残留插件此前仍被运行时加载）
- **PLC 看门狗异常退出后自动重连永久失效**：三菱/西门子看门狗改由监督者循环托管，异常退出 5s 后自动重启；心跳读到已释放客户端（重连换实例竞态）判定掉线走重连，不再退出看门狗
- **扫码枪串口断开完全不重连**：订阅 `ErrorReceived` + 5s 周期重连监控，USB 拔出检测（`GetPortNames`）后关闭残留句柄等待重插，设备恢复或出错后自动重开；初始化失败由监控持续重试

变更：

- **韧性管道接线**：三条管道（`Database-Retry`/`PLC-Retry`/`Grpc-CircuitBreaker`）在 `AddPlatformResilience` 中直接登记到 Registry，移除误导性 `ResiliencePipeline.Empty` 瞬态注册；`FreeSqlRepository` 五个方法接入 `Database-Retry`
- **`IReportDataProvider`/`ReportData` 移至 `AP.Contracts.Report`**：契约前缀强制共享 ALC，业务插件 Provider 可被 Host 收集（插件经 `AddSingleton<IReportDataProvider, T>()` 注册）
- **审计覆盖**：新增 `AuditingPlcServiceDecorator`（`IPlcService` 实际解析类型），PLC Write/WriteBatch 自动留痕（操作人/地址/值/结果，未登录记 `system`）；`SettingsService` 配置保存成功/失败均写审计日志
- **单实例与托盘重启**：启动时以命名互斥体做单实例检查（重复启动弹提示退出）；托盘重启新进程带 `--restart`，等待旧实例释放互斥体后接管启动
- 测试规模：237 个测试（新增韧性注册 5 例、DB 仓储 2 例、PLC 审计装饰器 6 例）

### 阶段一排雷（2026-07-22）

修复：

- **报表后台任务不运行**：`ReportScheduler` / `ReportCleanupService` 由 `Bootstrapper` 显式启动，恢复定时归档/定期清理；注册改为"单例 + `AddHostedService` 转发"，消除双注册多实例隐患
- **启动异常卡死 Splash**：`OnInitialized` 最外层异常兜底关闭 Splash 并弹出错误提示
- **主题缺键**：补齐 `Brush.Overlay.Background`，修复 `LoadingSpinner` 使用处潜在的 XamlParseException
- **配置写回静默失败**：`ConfigurationHelper.UpdateAppSetting` 改为临时文件+替换原子写入，IO/JSON/权限错误如实抛出（设置中心可正确提示保存失败）
- **安装包升级覆盖现场配置**：`appsettings*.json` 改为仅首次安装写入；.NET 运行时检测改为 8.x 主版本精确匹配；AppMutex 防止应用运行中安装
- **插件语义未执行**：落地 `Required`/`Dependencies`/重复 ID——重复 ID 全部拒绝加载并中止启动；依赖缺失拒绝加载并级联；Required 插件失败中止启动并明确提示
- **业务残留**：移除气密示例插件 `AP.Plugin.AirtightnessCheck`；标题/软件名/工位名中性化；Dashboard 占位数据加 TODO 标注
- **构建警告清零**：修复全部 Nullable 警告（全量 Rebuild 0 警告 0 错误）

变更：

- 插件元数据审计：11 个功能/硬件插件改为 `Required = false`（仅 Login/Layout 保持必需），外设缺失或功能插件故障不再中止系统启动
- 范围决策：仅聚焦 Standalone + SQLite，Server/Client（gRPC）与 PostgreSQL/SQL Server 冻结，README/GETTING_STARTED/AGENTS 已同步标注
- 测试规模：18 个测试文件 / 222 个测试（新增 `PluginGraphValidatorTests` 7 例 + `ConfigurationHelperTests` 2 例）

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
  - 完整的状态枚举定义（14 个状态，值 0-13）
  - 对应更新 `StateTransitionValidator` 转换规则
- **登录认证插件**：新增 `AP.Plugin.Login`
  - 启动时弹出登录窗口（`Security:Enabled=true`）
  - 默认账号 `admin/admin123` 首次登录强制改密
  - 主界面顶部显示当前用户、支持退出登录后重新登录
  - 登录/登出/改密记录审计日志
  - `ILoginService` 接口定义在 `AP.Contracts.System` 中，宿主与插件解耦
- **用户管理插件**：新增 `AP.Plugin.UserManagement`
  - 用户列表、搜索、新增/编辑/删除/重置密码
  - 仅对拥有 `user.manage` 权限的用户可见
  - 用户操作（增删改、重置密码）记录审计日志
- **布局优化**：启用 `SidebarRegion` 作为系统管理导航菜单
  - 左侧边栏显示系统配置、用户管理、角色管理（预留）、审计日志（预留）入口
  - 用户管理入口按 `user.manage` 权限动态显示/隐藏
- **角色管理插件**：新增 `AP.Plugin.RoleManagement`
  - 角色列表、权限分配、新增/编辑/删除
  - 仅对拥有 `role.manage` 权限的用户可见
  - 角色操作记录审计日志
- **审计日志可视化插件**：新增 `AP.Plugin.AuditLog`
  - 审计日志列表查询、筛选、分页
  - 仅对拥有 `audit.view` 权限的用户可见
- **配方管理插件**：新增 `AP.Plugin.RecipeManagement`
  - 配方列表、编辑、参数维护、设为默认、切换配方
  - 按 `recipe.view` / `recipe.edit` / `recipe.switch` 控制界面
- **报表中心插件**：新增 `AP.Contracts.Report` / `AP.Plugin.ReportCenter`
  - 报表归档查询、手动生成、打开、导出
  - 按 `report.view` 权限显示
- **PLC 驱动抽象与西门子支持**
  - 新增 `AP.Contracts.Hardware` 抽象：`IPlcDriverFactory`、`PlcOptions`
  - 新增 `AP.Infra.Hardware`：`PlcDriverRegistry`、`ActivePlcService`
  - 新增 `AP.Plugin.Plc.Siemens` 西门子 PLC 驱动插件（基于 IoTClient.SiemensClient）
  - 改造 `AP.Plugin.Plc.Mitsubishi`：由直接注册 `IPlcService` 改为注册 `IPlcDriverFactory`
  - `AP.Host.Desktop` 通过 `AddPlcHardware` 统一注册 `IPlcService`
  - 业务代码通过统一 `IPlcService` 无感知切换三菱/西门子/欧姆龙
  - 新增 `AP.Plugin.SystemSettings` 统一 PLC 配置编辑器
  - 移除三菱插件专用的 `MitsubishiPlcConfigurationContributor`，统一由系统设置中的 PLC 配置管理
  - `appsettings.json` 新增 `Plc` 配置节
- **用户/角色管理视图修复**
  - `RoleListView` 改为构造函数注入 `RoleListViewModel`，移除 `AutoWireViewModel`
  - 统一所有插件视图采用构造函数注入 ViewModel 模式
  - `UserManagementPlugin` / `RoleManagementPlugin` 按 `Security:Enabled` 和当前权限条件注册 Region 视图
- **报表初始化修复**
  - 在 `AP.Host.Desktop` 中直接引用 `AP.Infra.Report` 和 `AP.Contracts.Report`，解决启动时 `ReflectionTypeLoadException`
  - 在 `Bootstrapper.OnInitialized` 中手动调用 `ReportDatabaseInitializer.StartAsync`，解决 `report_archives` 表缺失
- **声明式导航贡献者模式**
  - 新增 `AP.Shared.PluginSDK/Navigation/`：`INavigationContributor`、`NavigationMenuItem`、`NavigationMenuItemBuilder`
  - 插件实现接口即出现在 Sidebar，菜单统一去重/排序/按权限过滤
  - `Bootstrapper` 桥接 DryIoc 时自动注册插件实例的 `INavigationContributor` 接口
  - 支持 `AppConfiguration:DefaultNavigationTarget` 指定默认页，默认选中延迟到 UI 就绪后导航
  - `Security:Enabled=false` 时按 `AppConfiguration:NavigationWhenSecurityDisabled` 白名单过滤菜单，安全类插件跳过视图注册
- **设置贡献者模式**
  - 新增 `ISettingsContributor` / `ISettingsEditorViewModel`（`AP.Shared.PluginSDK/Configuration/`）
  - 系统配置中心自动收集贡献者按分类分组，统一校验/备份/写回
  - `AP.Plugin.DeviceConfiguration` 通过该模式提供"扫码枪配置"设置页
- **UI 主题与修复**
  - `Industrial.Teal.MD3.xaml` 重构为全浅色 Material Design 3 主题（主色深蓝 `#1E3A5F`、强调色青蓝 `#0891B2`）
  - 用户管理“角色”列、角色管理“权限”列的 Chip 文字颜色由白色改为 `MaterialDesign.Brush.OnSurface`，解决浅色背景下看不清的问题
  - 修复扫码枪"换行符"默认值在设置页显示为空的问题
- **文档更新**
  - 新增 `AGENTS.md`（AI 交接文档）
  - 新增 `docs/PROJECT_STATUS.md`（项目状态与工作计划）
  - 2026-07-21：全面核对实际代码（36 个项目），重写 `AGENTS.md`、`README.md`、`docs/ARCHITECTURE.md`、`docs/GETTING_STARTED.md`、`docs/PROJECT_STATUS.md`、`docs/TESTING.md`
  - 修正：权限清单（12 个种子权限）、导航/设置贡献者模式、统一 `Plc` 配置节、`Resilience` 扁平配置键、gRPC `ServerUrl` 配置键、数据库嵌套配置结构、测试数量（17 文件 / 213 测试）、状态机 14 态、移除不存在的 Toast 组件说明

### 变更

- 初始项目结构搭建完成
- 完善测试项目结构：AP.Core.Tests、AP.Shared.Tests、AP.Infra.Tests
- 测试覆盖核心框架关键路径
- 修复 `PluginLifecycleManager.RegisterPlugins` 未按优先级排序的问题
- 修复 `ConfigurationHelper.UpdateAppSetting` 空 section 未抛异常的问题
- 修复测试项目 CPM 版本管理配置不一致的问题
- 安全模块改为可选：`Security:Enabled` 配置开关，关闭时跳过用户/角色/权限表初始化并注入匿名实现
- 配置界面改为独立模态弹窗：`SettingsDialogWindow` + `ISettingsDialogService`，替代原右侧抽屉模式
- `UserInfo` 增加 `MustChangePassword` 属性，`IdentityService` 改密成功后自动清除该标志
- `ViewModelBase` 增加 `RequestClose` 事件，统一模态窗口关闭机制
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

**最后更新**: 2026-07-21