using System.Windows.Controls;

namespace AP.Plugin.Layout.Views;

/// <summary>
/// 底部状态栏（设备状态 / 公司名 / 当前时间），DataContext 继承布局的 LayoutViewModel
/// </summary>
public partial class StatusBarView : UserControl
{
    public StatusBarView()
    {
        InitializeComponent();
    }
}
