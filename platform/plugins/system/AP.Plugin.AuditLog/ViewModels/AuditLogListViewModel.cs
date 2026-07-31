#region

using System.Collections.ObjectModel;
using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Audit;
using AP.Plugin.AuditLog.Models;
using AP.Shared.UI.Base;
using AP.Shared.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

#endregion

namespace AP.Plugin.AuditLog.ViewModels;

/// <summary>
/// 审计日志列表 ViewModel
/// </summary>
public partial class AuditLogListViewModel : ViewModelBase
{
    private readonly IAuditService _auditService;
    private readonly IIdentityService _identityService;
    private readonly ICustomDialogService _dialogService;
    private readonly ILogger<AuditLogListViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<AuditLogEntry> _logs = new();

    [ObservableProperty]
    private DateTime? _startTime;

    [ObservableProperty]
    private DateTime? _endTime;

    [ObservableProperty]
    private string _searchUserName = string.Empty;

    /// <summary>操作类型筛选项（首项为"全部"）。</summary>
    public IReadOnlyList<ActionTypeOption> ActionTypeOptions => ActionTypeOption.All;

    [ObservableProperty]
    private ActionTypeOption _selectedActionTypeOption = ActionTypeOption.All[0];

    [ObservableProperty]
    private int _pageIndex = 1;

    [ObservableProperty]
    private int _pageSize = 50;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private bool _canGoPrevious;

    [ObservableProperty]
    private bool _canGoNext;

    [ObservableProperty]
    private string _pageInfo = string.Empty;

    public AuditLogListViewModel(
        IAuditService auditService,
        IIdentityService identityService,
        ICustomDialogService dialogService,
        ILogger<AuditLogListViewModel> logger)
    {
        _auditService = auditService;
        _identityService = identityService;
        _dialogService = dialogService;
        _logger = logger;

        Title = "审计日志";

        // 默认查询最近 7 天
        StartTime = DateTime.Today.AddDays(-6);
        EndTime = DateTime.Today.AddDays(1).AddTicks(-1);
    }

    public override async void OnNavigatedTo(NavigationContext navigationContext)
    {
        await LoadLogsAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        PageIndex = 1;
        await LoadLogsAsync();
    }

    [RelayCommand]
    private async Task ResetAsync()
    {
        StartTime = DateTime.Today.AddDays(-6);
        EndTime = DateTime.Today.AddDays(1).AddTicks(-1);
        SearchUserName = string.Empty;
        SelectedActionTypeOption = ActionTypeOption.All[0];
        PageIndex = 1;
        await LoadLogsAsync();
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (PageIndex <= 1) return;
        PageIndex--;
        await LoadLogsAsync();
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (PageIndex * PageSize >= TotalCount) return;
        PageIndex++;
        await LoadLogsAsync();
    }

    private async Task LoadLogsAsync()
    {
        if (!_identityService.HasPermission("audit.view"))
        {
            await _dialogService.ShowErrorAsync("您没有查看审计日志的权限");
            return;
        }

        IsBusy = true;
        try
        {
            var skip = (PageIndex - 1) * PageSize;
            var actionType = SelectedActionTypeOption?.Value;
            var logs = await _auditService.QueryAsync(
                StartTime,
                EndTime,
                string.IsNullOrWhiteSpace(SearchUserName) ? null : SearchUserName.Trim(),
                actionType,
                skip,
                PageSize);

            TotalCount = await _auditService.CountAsync(
                StartTime,
                EndTime,
                string.IsNullOrWhiteSpace(SearchUserName) ? null : SearchUserName.Trim(),
                actionType);

            Logs = new ObservableCollection<AuditLogEntry>(logs);
            UpdatePageState();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载审计日志失败");
            await _dialogService.ShowErrorAsync("加载审计日志失败：" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdatePageState()
    {
        CanGoPrevious = PageIndex > 1;
        CanGoNext = PageIndex * PageSize < TotalCount;

        var totalPages = (int)Math.Ceiling((double)TotalCount / PageSize);
        totalPages = totalPages < 1 ? 1 : totalPages;
        PageInfo = $"第 {PageIndex} / {totalPages} 页，共 {TotalCount} 条";
    }
}
