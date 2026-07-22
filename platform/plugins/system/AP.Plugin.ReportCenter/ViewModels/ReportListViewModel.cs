#region

using System.Collections.ObjectModel;
using System.IO;
using AP.Contracts.Report.Abstractions;
using AP.Contracts.Report.Models;
using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Audit;
using AP.Shared.UI.Base;
using AP.Shared.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

#endregion

namespace AP.Plugin.ReportCenter.ViewModels;

/// <summary>
/// 报表中心列表 ViewModel
/// </summary>
public partial class ReportListViewModel : ViewModelBase
{
    private readonly IReportCenterService _reportCenterService;
    private readonly IIdentityService _identityService;
    private readonly IAuditService _auditService;
    private readonly ICustomDialogService _dialogService;
    private readonly ILogger<ReportListViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<ReportArchiveDto> _archives = new();

    [ObservableProperty]
    private ObservableCollection<ReportTypeInfo> _reportTypes = new();

    [ObservableProperty]
    private ReportArchiveDto? _selectedArchive;

    [ObservableProperty]
    private ReportTypeInfo? _selectedReportType;

    [ObservableProperty]
    private DateTime? _startDate;

    [ObservableProperty]
    private DateTime? _endDate;

    public ReportListViewModel(
        IReportCenterService reportCenterService,
        IIdentityService identityService,
        IAuditService auditService,
        ICustomDialogService dialogService,
        ILogger<ReportListViewModel> logger)
    {
        _reportCenterService = reportCenterService;
        _identityService = identityService;
        _auditService = auditService;
        _dialogService = dialogService;
        _logger = logger;

        Title = "报表中心";

        StartDate = DateTime.Today.AddDays(-6);
        EndDate = DateTime.Today;
    }

    public override async void OnNavigatedTo(NavigationContext navigationContext)
    {
        await LoadReportTypesAsync();
        await LoadArchivesAsync();
    }

    [RelayCommand]
    private async Task LoadReportTypesAsync()
    {
        try
        {
            var types = await _reportCenterService.GetReportTypesAsync();
            ReportTypes = new ObservableCollection<ReportTypeInfo>(types);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载报表类型失败");
        }
    }

    [RelayCommand]
    private async Task LoadArchivesAsync()
    {
        IsBusy = true;
        try
        {
            var reportType = SelectedReportType?.ReportType;
            var archives = await _reportCenterService.GetArchivesAsync(
                StartDate,
                EndDate,
                reportType);

            Archives = new ObservableCollection<ReportArchiveDto>(archives);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载报表归档失败");
            await _dialogService.ShowErrorAsync("加载报表归档失败：" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (!EnsurePermission("report.export")) return;
        if (SelectedReportType == null)
        {
            await _dialogService.ShowErrorAsync("请选择报表类型");
            return;
        }

        IsBusy = true;
        try
        {
            var path = await _reportCenterService.GenerateAsync(SelectedReportType.ReportType, DateTime.Today);
            await LogAuditAsync(AuditActionType.ExportReport, SelectedReportType.ReportType, true, "生成报表");
            await _dialogService.ShowAlertAsync($"报表已生成：{path}", "生成成功");
            await LoadArchivesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成报表失败");
            await _dialogService.ShowErrorAsync("生成报表失败：" + ex.Message);
            await LogAuditAsync(AuditActionType.ExportReport, SelectedReportType?.ReportType ?? string.Empty, false, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (!EnsurePermission("report.view")) return;
        if (SelectedArchive == null) return;

        BusyText = "正在打开报表...";
        IsBusy = true;
        try
        {
            await _reportCenterService.OpenAsync(SelectedArchive.Id);
            await LogAuditAsync(AuditActionType.ExportReport, SelectedArchive.ReportType, true, "打开报表");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打开报表失败");
            await _dialogService.ShowErrorAsync("打开报表失败：" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (!EnsurePermission("report.export")) return;
        if (SelectedArchive == null) return;

        BusyText = "正在导出报表...";
        IsBusy = true;
        try
        {
            var destDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Reports");
            var path = await _reportCenterService.ExportAsync(SelectedArchive.Id, destDir);
            await LogAuditAsync(AuditActionType.ExportReport, SelectedArchive.ReportType, true, "导出报表");
            await _dialogService.ShowAlertAsync($"报表已导出到：{path}", "导出成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出报表失败");
            await _dialogService.ShowErrorAsync("导出报表失败：" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool EnsurePermission(string permissionCode)
    {
        if (_identityService.HasPermission(permissionCode)) return true;

        _dialogService.ShowErrorAsync("您没有执行该操作的权限").ConfigureAwait(false);
        return false;
    }

    private async Task LogAuditAsync(AuditActionType actionType, string reportType, bool succeeded, string description)
    {
        try
        {
            await _auditService.LogAsync(new AuditLogEntry
            {
                Timestamp = DateTime.Now,
                UserName = _identityService.CurrentUser?.UserName ?? "unknown",
                ActionType = actionType,
                ActionName = description,
                TargetId = reportType,
                Succeeded = succeeded
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "记录审计日志失败");
        }
    }
}
