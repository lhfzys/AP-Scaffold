#region

using System.Collections.ObjectModel;
using System.Windows;
using AP.Contracts.Recipe.Abstractions;
using AP.Contracts.Recipe.Models;
using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Audit;
using AP.Shared.UI.Base;
using AP.Shared.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#endregion

namespace AP.Plugin.RecipeManagement.ViewModels;

/// <summary>
/// 配方列表 ViewModel
/// </summary>
public partial class RecipeListViewModel : ViewModelBase
{
    private readonly IRecipeManager _recipeManager;
    private readonly IIdentityService _identityService;
    private readonly IAuditService _auditService;
    private readonly ICustomDialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RecipeListViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<RecipeInfo> _recipes = new();

    [ObservableProperty]
    private RecipeInfo? _selectedRecipe;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public RecipeListViewModel(
        IRecipeManager recipeManager,
        IIdentityService identityService,
        IAuditService auditService,
        ICustomDialogService dialogService,
        IServiceProvider serviceProvider,
        ILogger<RecipeListViewModel> logger)
    {
        _recipeManager = recipeManager;
        _identityService = identityService;
        _auditService = auditService;
        _dialogService = dialogService;
        _serviceProvider = serviceProvider;
        _logger = logger;

        Title = "配方管理";
    }

    public override async void OnNavigatedTo(NavigationContext navigationContext)
    {
        await LoadRecipesAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = LoadRecipesAsync();
    }

    [RelayCommand]
    private async Task LoadRecipesAsync()
    {
        IsBusy = true;
        try
        {
            var all = await _recipeManager.GetAllAsync();
            var filtered = all.Where(r =>
                string.IsNullOrWhiteSpace(SearchText) ||
                r.Code.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                r.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

            Recipes = new ObservableCollection<RecipeInfo>(filtered);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载配方列表失败");
            await _dialogService.ShowErrorAsync("加载配方列表失败：" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddRecipeAsync()
    {
        if (!EnsurePermission("recipe.edit")) return;

        var window = _serviceProvider.GetRequiredService<Views.RecipeEditWindow>();
        if (window.DataContext is RecipeEditViewModel vm)
        {
            await vm.InitializeForCreateAsync();
        }

        window.Owner = Application.Current.MainWindow;
        window.ShowDialog();
        if (window.DataContext is RecipeEditViewModel editVm && editVm.IsSaved)
        {
            await LoadRecipesAsync();
        }
    }

    [RelayCommand]
    private async Task EditRecipeAsync()
    {
        if (!EnsurePermission("recipe.edit")) return;
        if (SelectedRecipe == null) return;

        var window = _serviceProvider.GetRequiredService<Views.RecipeEditWindow>();
        if (window.DataContext is RecipeEditViewModel vm)
        {
            await vm.InitializeForEditAsync(SelectedRecipe);
        }

        window.Owner = Application.Current.MainWindow;
        window.ShowDialog();
        if (window.DataContext is RecipeEditViewModel editVm && editVm.IsSaved)
        {
            await LoadRecipesAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteRecipeAsync()
    {
        if (!EnsurePermission("recipe.edit")) return;
        if (SelectedRecipe == null) return;

        if (SelectedRecipe.IsDefault)
        {
            await _dialogService.ShowErrorAsync("默认配方不能删除");
            return;
        }

        var confirm = await _dialogService.ShowConfirmAsync(
            $"确定要删除配方 {SelectedRecipe.Name}({SelectedRecipe.Code}) 吗？",
            "删除确认");
        if (!confirm) return;

        IsBusy = true;
        try
        {
            await _recipeManager.DeleteAsync(SelectedRecipe.Id);
            await LogAuditAsync(AuditActionType.Delete, SelectedRecipe.Code, true, "删除配方");
            await LoadRecipesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除配方失败");
            await _dialogService.ShowErrorAsync("删除配方失败：" + ex.Message);
            await LogAuditAsync(AuditActionType.Delete, SelectedRecipe.Code, false, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SetDefaultAsync()
    {
        if (!EnsurePermission("recipe.switch")) return;
        if (SelectedRecipe == null) return;

        IsBusy = true;
        try
        {
            var success = await _recipeManager.SetDefaultAsync(SelectedRecipe.Id);
            if (success)
            {
                await LogAuditAsync(AuditActionType.Update, SelectedRecipe.Code, true, "设为默认配方");
                await LoadRecipesAsync();
            }
            else
            {
                await _dialogService.ShowErrorAsync("设置默认配方失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置默认配方失败");
            await _dialogService.ShowErrorAsync("设置默认配方失败：" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SwitchRecipeAsync()
    {
        if (!EnsurePermission("recipe.switch")) return;
        if (SelectedRecipe == null) return;

        IsBusy = true;
        try
        {
            var success = await _recipeManager.SwitchAsync(SelectedRecipe.Code);
            if (success)
            {
                await LogAuditAsync(AuditActionType.SwitchRecipe, SelectedRecipe.Code, true, "切换配方");
                await _dialogService.ShowAlertAsync($"已切换到配方：{SelectedRecipe.Name}", "切换成功");
            }
            else
            {
                await _dialogService.ShowErrorAsync("切换配方失败，配方不存在或未启用");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换配方失败");
            await _dialogService.ShowErrorAsync("切换配方失败：" + ex.Message);
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

    private async Task LogAuditAsync(AuditActionType actionType, string recipeCode, bool succeeded, string description)
    {
        try
        {
            await _auditService.LogAsync(new AuditLogEntry
            {
                Timestamp = DateTime.Now,
                UserName = _identityService.CurrentUser?.UserName ?? "unknown",
                ActionType = actionType,
                ActionName = description,
                TargetId = recipeCode,
                Succeeded = succeeded
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "记录审计日志失败");
        }
    }
}
