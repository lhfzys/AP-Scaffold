#region

using System.Collections.ObjectModel;
using AP.Contracts.Recipe.Abstractions;
using AP.Contracts.Recipe.Models;
using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Audit;
using AP.Shared.UI.Base;
using AP.Shared.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

#endregion

namespace AP.Plugin.RecipeManagement.ViewModels;

/// <summary>
/// 配方编辑窗口 ViewModel
/// </summary>
public partial class RecipeEditViewModel : ViewModelBase
{
    private readonly IRecipeManager _recipeManager;
    private readonly IIdentityService _identityService;
    private readonly IAuditService _auditService;
    private readonly ICustomDialogService _dialogService;
    private readonly ILogger<RecipeEditViewModel> _logger;

    [ObservableProperty]
    private string _title = "新增配方";

    [ObservableProperty]
    private long _recipeId;

    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private ObservableCollection<RecipeParameter> _parameters = new();

    [ObservableProperty]
    private RecipeParameter? _selectedParameter;

    public bool IsSaved { get; private set; }

    private bool _isEdit;

    public RecipeEditViewModel(
        IRecipeManager recipeManager,
        IIdentityService identityService,
        IAuditService auditService,
        ICustomDialogService dialogService,
        ILogger<RecipeEditViewModel> logger)
    {
        _recipeManager = recipeManager;
        _identityService = identityService;
        _auditService = auditService;
        _dialogService = dialogService;
        _logger = logger;
    }

    public Task InitializeForCreateAsync()
    {
        IsSaved = false;
        _isEdit = false;
        Title = "新增配方";
        RecipeId = 0;
        Code = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
        IsEnabled = true;
        Parameters = new ObservableCollection<RecipeParameter>();
        return Task.CompletedTask;
    }

    public Task InitializeForEditAsync(RecipeInfo recipe)
    {
        IsSaved = false;
        _isEdit = true;
        Title = "编辑配方";
        RecipeId = recipe.Id;
        Code = recipe.Code;
        Name = recipe.Name;
        Description = recipe.Description ?? string.Empty;
        IsEnabled = recipe.IsEnabled;
        Parameters = new ObservableCollection<RecipeParameter>(recipe.Parameters);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void AddParameter()
    {
        Parameters.Add(new RecipeParameter { Name = "新参数", Value = "0" });
    }

    [RelayCommand]
    private void RemoveParameter()
    {
        if (SelectedParameter == null) return;
        Parameters.Remove(SelectedParameter);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Code))
        {
            await _dialogService.ShowErrorAsync("配方编码不能为空");
            return;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            await _dialogService.ShowErrorAsync("配方名称不能为空");
            return;
        }

        BusyText = "正在保存配方...";
        IsBusy = true;
        try
        {
            var recipe = new RecipeInfo
            {
                Code = Code.Trim(),
                Name = Name.Trim(),
                Description = Description.Trim(),
                IsEnabled = IsEnabled,
                Parameters = Parameters.ToList()
            };

            if (_isEdit)
            {
                var updated = await _recipeManager.UpdateAsync(RecipeId, recipe);
                if (updated == null)
                {
                    await _dialogService.ShowErrorAsync("配方不存在或已被删除");
                    return;
                }

                await LogAuditAsync(AuditActionType.Update, recipe.Code, true, "编辑配方");
            }
            else
            {
                var existing = await _recipeManager.GetByCodeAsync(recipe.Code);
                if (existing != null)
                {
                    await _dialogService.ShowErrorAsync("配方编码已存在");
                    return;
                }

                await _recipeManager.CreateAsync(recipe);
                await LogAuditAsync(AuditActionType.Create, recipe.Code, true, "新增配方");
            }

            IsSaved = true;
            OnRequestClose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存配方失败");
            await _dialogService.ShowErrorAsync("保存配方失败：" + ex.Message);
            await LogAuditAsync(_isEdit ? AuditActionType.Update : AuditActionType.Create, Code, false, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        IsSaved = false;
        OnRequestClose();
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
