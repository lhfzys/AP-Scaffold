using AP.Plugin.TagConfiguration.ViewModels;

namespace AP.Plugin.TagConfiguration.Views;

/// <summary>
/// 点表配置列表视图
/// </summary>
public partial class TagTableListView
{
    public TagTableListView(TagTableListViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
