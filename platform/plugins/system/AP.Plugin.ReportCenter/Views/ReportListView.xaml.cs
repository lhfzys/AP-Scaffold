using System.Windows.Controls;
using AP.Plugin.ReportCenter.ViewModels;

namespace AP.Plugin.ReportCenter.Views;

/// <summary>
/// 报表中心视图
/// </summary>
public partial class ReportListView : UserControl
{
    public ReportListView(ReportListViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
