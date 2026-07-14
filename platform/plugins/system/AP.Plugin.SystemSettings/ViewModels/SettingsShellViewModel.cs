using System.Collections.ObjectModel;
using System.Windows;
using AP.Plugin.SystemSettings.Services;
using AP.Shared.PluginSDK.Configuration;
using AP.Shared.UI.Base;
using AP.Shared.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AP.Plugin.SystemSettings.ViewModels;

/// <summary>
/// 配置中心外壳 ViewModel
/// </summary>
public partial class SettingsShellViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly SettingsService _settingsService;
    private readonly ICustomDialogService _dialogService;
    private readonly ILogger<SettingsShellViewModel> _logger;

    [ObservableProperty]
    private List<SettingsCategoryItem> _categories = new();

    [ObservableProperty]
    private ObservableCollection<INavigationItem> _navigationItems = new();

    [ObservableProperty]
    private SettingsContributorItem? _selectedContributor;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// 请求关闭配置对话框事件
    /// </summary>
    public event EventHandler? RequestClose;

    public SettingsShellViewModel(
        IServiceProvider serviceProvider,
        IEnumerable<ISettingsContributor> contributors,
        IConfiguration configuration,
        SettingsService settingsService,
        ICustomDialogService dialogService,
        ILogger<SettingsShellViewModel> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _settingsService = settingsService;
        _dialogService = dialogService;
        _logger = logger;

        InitializeContributors(contributors);
    }

    private void InitializeContributors(IEnumerable<ISettingsContributor> contributors)
    {
        var orderedContributors = contributors
            .OrderBy(c => c.Order)
            .ThenBy(c => c.Title)
            .ToList();

        foreach (var contributor in orderedContributors)
        {
            try
            {
                var editor = contributor.CreateViewModel(_serviceProvider);
                editor.LoadFromConfiguration(_configuration);

                var category = Categories.FirstOrDefault(c => c.Category == contributor.Category);
                if (category == null)
                {
                    category = new SettingsCategoryItem(contributor.Category);
                    Categories.Add(category);
                }

                category.Contributors.Add(new SettingsContributorItem(contributor, editor));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化配置贡献者失败: {Title}", contributor.Title);
            }
        }

        // 构建左侧扁平导航列表（分类标题 + 贡献者），保持原有分类顺序
        var navigationItems = new ObservableCollection<INavigationItem>();
        foreach (var category in Categories)
        {
            navigationItems.Add(new SettingsCategoryHeaderItem(category.Category));
            foreach (var contributor in category.Contributors)
            {
                navigationItems.Add(contributor);
            }
        }

        NavigationItems = navigationItems;

        // 默认选中第一个
        SelectedContributor = Categories
            .SelectMany(c => c.Contributors)
            .FirstOrDefault();
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (IsBusy) return;

        var editors = Categories
            .SelectMany(c => c.Contributors)
            .Select(c => (c.Contributor, c.Editor))
            .ToList();

        IsBusy = true;
        try
        {
            var result = _settingsService.SaveSettings(editors);

            if (!result.Success)
            {
                await _dialogService.ShowErrorAsync($"保存失败：\n{string.Join("\n", result.Errors)}");
                return;
            }

            var message = "配置已保存。";
            if (result.RequiresRestart)
                message += "\n\n部分配置需要重启应用或重新连接设备后才能生效。";

            await _dialogService.ShowAlertAsync(message, "保存成功");

            _logger.LogInformation("配置保存成功，备份：{BackupPath}", result.BackupPath);

            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CancelChanges()
    {
        foreach (var contributor in Categories.SelectMany(c => c.Contributors))
        {
            contributor.Editor.LoadFromConfiguration(_configuration);
        }

        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
