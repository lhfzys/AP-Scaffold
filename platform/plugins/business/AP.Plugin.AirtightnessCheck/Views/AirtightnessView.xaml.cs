using System.Windows.Controls;
using AP.Plugin.AirtightnessCheck.ViewModels;

namespace AP.Plugin.AirtightnessCheck.Views;

/// <summary>
/// 气密性检测业务视图
/// </summary>
public partial class AirtightnessView : UserControl
{
    public AirtightnessView(AirtightnessViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
