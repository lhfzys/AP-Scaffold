using AP.Shared.UI.Dialogs.ViewModels;
using AP.Shared.UI.Dialogs.Views;
using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Media;
using AP.Shared.Utilities.Constants;

namespace AP.Shared.UI.Services;

public class MaterialDialogService : ICustomDialogService
{
    public async Task ShowAlertAsync(string message, string title = "提示")
    {
        var vm = new ConfirmDialogViewModel
        {
            Title = title,
            Message = message,
            IsConfirmMode = false,
            Icon = PackIconKind.Information,
            IconColor = ResolveThemeBrush("Brush.Info", Brushes.DodgerBlue)
        };
        await DialogHost.Show(new ConfirmDialogView { DataContext = vm }, AppConstants.Dialogs.RootIdentifier);
    }

    public async Task<bool> ShowConfirmAsync(string message, string title = "确认操作")
    {
        var vm = new ConfirmDialogViewModel
        {
            Title = title,
            Message = message,
            IsConfirmMode = true,
            Icon = PackIconKind.QuestionMarkCircle,
            IconColor = ResolveThemeBrush("Brush.Warning", Brushes.Orange)
        };

        var result = await DialogHost.Show(new ConfirmDialogView { DataContext = vm },
            AppConstants.Dialogs.RootIdentifier);

        if (result is bool b) return b;
        if (result is string s && bool.TryParse(s, out var parsed)) return parsed;
        return false;
    }

    public async Task ShowErrorAsync(string message)
    {
        var vm = new ConfirmDialogViewModel
        {
            Title = "错误",
            Message = message,
            IsConfirmMode = false,
            Icon = PackIconKind.AlertCircle,
            IconColor = ResolveThemeBrush("Brush.Error", Brushes.Red)
        };

        await DialogHost.Show(new ConfirmDialogView { DataContext = vm }, AppConstants.Dialogs.RootIdentifier);
    }

    /// <summary>从应用资源取主题画刷，取不到（如主题未加载）时回退到指定色。</summary>
    private static Brush ResolveThemeBrush(string resourceKey, Brush fallback)
        => Application.Current?.TryFindResource(resourceKey) as Brush ?? fallback;
}