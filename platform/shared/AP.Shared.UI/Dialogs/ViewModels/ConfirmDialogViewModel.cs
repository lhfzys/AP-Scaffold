using MaterialDesignThemes.Wpf;
using System.Windows.Media;

namespace AP.Shared.UI.Dialogs.ViewModels;

public class ConfirmDialogViewModel
{
    public string Title { get; set; } = "提示";
    public string Message { get; set; } = "";
    public bool IsConfirmMode { get; set; } = false;

    public PackIconKind Icon { get; set; } = PackIconKind.Information;
    public Brush IconColor { get; set; } = Brushes.DodgerBlue;
}