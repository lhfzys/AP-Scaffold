#region

using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using AP.Contracts.Hardware.DeviceRuntime;
using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Audit;
using AP.Plugin.TagConfiguration.Models;
using AP.Plugin.TagConfiguration.Services;
using AP.Plugin.TagConfiguration.Views;
using AP.Shared.UI.Base;
using AP.Shared.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#endregion

namespace AP.Plugin.TagConfiguration.ViewModels;

/// <summary>
/// 点表配置列表 ViewModel：tags.json 可视化编辑（保存后重启生效）。
/// 保存前经 <see cref="ITagTableValidator"/> 全量校验（与启动加载同一规则），非法点表不落盘。
/// </summary>
public partial class TagTableListViewModel : ViewModelBase
{
    private readonly ITagTableValidator _tagTableValidator;
    private readonly IAuditService _auditService;
    private readonly IIdentityService _identityService;
    private readonly ICustomDialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TagTableListViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<TagEditRow> _rows = new();

    [ObservableProperty]
    private TagEditRow? _selectedRow;

    [ObservableProperty]
    private string _defaultIntervalText = "1000";

    public TagTableListViewModel(
        ITagTableValidator tagTableValidator,
        IAuditService auditService,
        IIdentityService identityService,
        ICustomDialogService dialogService,
        IServiceProvider serviceProvider,
        ILogger<TagTableListViewModel> logger)
    {
        _tagTableValidator = tagTableValidator;
        _auditService = auditService;
        _identityService = identityService;
        _dialogService = dialogService;
        _serviceProvider = serviceProvider;
        _logger = logger;

        Title = "点表配置";
    }

    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        LoadFromFile();
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadFromFile();
    }

    [RelayCommand]
    private void AddTag()
    {
        var window = _serviceProvider.GetRequiredService<TagEditWindow>();
        if (window.DataContext is TagEditDialogViewModel vm)
        {
            vm.InitializeForCreate(Rows);
        }

        window.Owner = Application.Current.MainWindow;
        window.ShowDialog();

        if (window.DataContext is TagEditDialogViewModel editVm && editVm is { IsSaved: true, ResultRow: not null })
        {
            Rows.Add(editVm.ResultRow);
            SelectedRow = editVm.ResultRow;
        }
    }

    [RelayCommand]
    private void EditTag()
    {
        if (SelectedRow == null) return;

        var window = _serviceProvider.GetRequiredService<TagEditWindow>();
        if (window.DataContext is TagEditDialogViewModel vm)
        {
            vm.InitializeForEdit(SelectedRow, Rows);
        }

        window.Owner = Application.Current.MainWindow;
        window.ShowDialog();

        if (window.DataContext is TagEditDialogViewModel editVm && editVm is { IsSaved: true, ResultRow: not null })
        {
            var index = Rows.IndexOf(Rows.First(r =>
                string.Equals(r.Name, SelectedRow.Name, StringComparison.OrdinalIgnoreCase)));
            if (index >= 0)
            {
                Rows[index] = editVm.ResultRow;
                SelectedRow = editVm.ResultRow;
            }
        }
    }

    [RelayCommand]
    private async Task DeleteTagAsync()
    {
        if (SelectedRow == null) return;

        var confirm = await _dialogService.ShowConfirmAsync(
            $"确定要删除点 {SelectedRow.Name} 吗？（保存后生效）",
            "删除确认");
        if (!confirm) return;

        Rows.Remove(SelectedRow);
        SelectedRow = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!int.TryParse(DefaultIntervalText.Trim(), out var defaultInterval) || defaultInterval <= 0)
        {
            await _dialogService.ShowErrorAsync("默认采集周期必须为正整数（毫秒）");
            return;
        }

        var definitions = Rows.Select(r => r.ToDefinition()).ToList();
        var errors = _tagTableValidator.Validate(definitions);
        if (errors.Count > 0)
        {
            await _dialogService.ShowErrorAsync(
                $"点表校验未通过，未保存：{Environment.NewLine} - {string.Join($"{Environment.NewLine} - ", errors)}");
            return;
        }

        IsBusy = true;
        BusyText = "正在保存点表...";
        try
        {
            var data = new TagTableFileData
            {
                Acquisition = new TagAcquisitionConfig
                {
                    DefaultIntervalMs = defaultInterval,
                    Overrides = Rows
                        .Where(r => r.IntervalOverrideMs is > 0)
                        .ToDictionary(r => r.Name, r => r.IntervalOverrideMs!.Value, StringComparer.OrdinalIgnoreCase)
                },
                Tags = definitions
            };
            TagTableFileStore.Save(data);

            await LogAuditAsync(true, $"更新点表（{definitions.Count} 点）");
            await _dialogService.ShowAlertAsync("点表已保存，重启应用后生效。", "保存成功");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            _logger.LogError(ex, "保存点表失败");
            await LogAuditAsync(false, ex.Message);
            await _dialogService.ShowErrorAsync("保存点表失败：" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadFromFile()
    {
        try
        {
            var data = TagTableFileStore.Load();
            DefaultIntervalText = data.Acquisition.DefaultIntervalMs.ToString();

            var rows = data.Tags.Select(def => new TagEditRow
            {
                Name = def.Name,
                DeviceId = def.DeviceId,
                Address = def.Address,
                DataType = def.DataType,
                Access = def.Access,
                Description = def.Description,
                Group = def.Group,
                Unit = def.Unit,
                IntervalOverrideMs = data.Acquisition.Overrides.TryGetValue(def.Name, out var interval) ? interval : null
            });
            Rows = new ObservableCollection<TagEditRow>(rows);
            SelectedRow = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            _logger.LogError(ex, "读取点表文件失败");
            _dialogService.ShowErrorAsync("读取点表文件失败：" + ex.Message).ConfigureAwait(false);
        }
    }

    private async Task LogAuditAsync(bool succeeded, string description)
    {
        try
        {
            await _auditService.LogAsync(new AuditLogEntry
            {
                Timestamp = DateTime.Now,
                UserName = _identityService.CurrentUser?.UserName ?? "unknown",
                ActionType = AuditActionType.Update,
                ActionName = "更新点表",
                TargetId = "tags.json",
                Succeeded = succeeded,
                Description = description
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "记录审计日志失败");
        }
    }
}
