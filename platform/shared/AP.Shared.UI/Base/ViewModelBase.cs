using CommunityToolkit.Mvvm.ComponentModel;

namespace AP.Shared.UI.Base;

/// <summary>
/// ViewModel 基类 (集成 Prism 导航与 MVVM Toolkit)
/// </summary>
public abstract partial class ViewModelBase : ObservableObject, INavigationAware, IDestructible
{
    /// <summary>
    /// 页面标题
    /// </summary>
    [ObservableProperty] private string _title = string.Empty;

    /// <summary>
    /// 忙碌状态 (用于控制 LoadingSpinner)
    /// </summary>
    [ObservableProperty] private bool _isBusy;

    /// <summary>
    /// 忙碌时的提示文本
    /// </summary>
    [ObservableProperty] private string _busyText = "加载中...";

    // --- Prism INavigationAware 实现 ---

    /// <summary>
    /// 是否允许重用实例 (默认 true)
    /// </summary>
    public virtual bool IsNavigationTarget(NavigationContext navigationContext)
    {
        return true;
    }

    /// <summary>
    /// 导航离开时触发
    /// </summary>
    public virtual void OnNavigatedFrom(NavigationContext navigationContext)
    {
    }

    /// <summary>
    /// 导航进入时触发
    /// </summary>
    public virtual void OnNavigatedTo(NavigationContext navigationContext)
    {
    }

    /// <summary>
    /// 销毁时触发 (用于释放资源)
    /// </summary>
    public virtual void Destroy()
    {
    }
}