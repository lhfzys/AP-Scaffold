using System.Windows.Controls;
using AP.Plugin.AuditLog.ViewModels;

namespace AP.Plugin.AuditLog.Views;

/// <summary>
/// 审计日志列表视图
/// </summary>
public partial class AuditLogListView : UserControl
{
    public AuditLogListView(AuditLogListViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
