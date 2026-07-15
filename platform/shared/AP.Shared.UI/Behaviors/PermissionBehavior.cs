using System.Windows;
using AP.Contracts.Security.Abstractions;
using Prism.Ioc;

namespace AP.Shared.UI.Behaviors;

/// <summary>
/// 权限行为附加属性
/// 根据当前用户是否拥有指定权限，控制元素的 Visibility 或 IsEnabled。
/// </summary>
public static class PermissionBehavior
{
    /// <summary>
    /// 权限代码
    /// </summary>
    public static readonly DependencyProperty PermissionProperty = DependencyProperty.RegisterAttached(
        "Permission",
        typeof(string),
        typeof(PermissionBehavior),
        new PropertyMetadata(null, OnPermissionChanged));

    /// <summary>
    /// 无权限时是否隐藏元素（默认 true）；false 则禁用元素。
    /// </summary>
    public static readonly DependencyProperty HideWhenUnauthorizedProperty = DependencyProperty.RegisterAttached(
        "HideWhenUnauthorized",
        typeof(bool),
        typeof(PermissionBehavior),
        new PropertyMetadata(true, OnPermissionChanged));

    public static string? GetPermission(DependencyObject obj) => (string?)obj.GetValue(PermissionProperty);

    public static void SetPermission(DependencyObject obj, string? value) => obj.SetValue(PermissionProperty, value);

    public static bool GetHideWhenUnauthorized(DependencyObject obj) => (bool)obj.GetValue(HideWhenUnauthorizedProperty);

    public static void SetHideWhenUnauthorized(DependencyObject obj, bool value) => obj.SetValue(HideWhenUnauthorizedProperty, value);

    private static void OnPermissionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;

        element.Loaded -= OnElementLoaded;
        element.Loaded += OnElementLoaded;

        if (element.IsLoaded)
        {
            ApplyPermission(element);
        }
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            ApplyPermission(element);
        }
    }

    private static void ApplyPermission(FrameworkElement element)
    {
        var permission = GetPermission(element);
        if (string.IsNullOrWhiteSpace(permission))
        {
            element.Visibility = Visibility.Visible;
            element.IsEnabled = true;
            return;
        }

        var identityService = GetIdentityService();
        if (identityService == null)
        {
            // 无法获取权限服务时，保守处理：隐藏/禁用
            SetUnauthorized(element);
            return;
        }

        var hasPermission = identityService.HasPermission(permission);
        if (hasPermission)
        {
            element.Visibility = Visibility.Visible;
            element.IsEnabled = true;
        }
        else
        {
            SetUnauthorized(element);
        }
    }

    private static void SetUnauthorized(FrameworkElement element)
    {
        var hide = GetHideWhenUnauthorized(element);
        if (hide)
        {
            element.Visibility = Visibility.Collapsed;
        }
        else
        {
            element.Visibility = Visibility.Visible;
            element.IsEnabled = false;
        }
    }

    private static IIdentityService? GetIdentityService()
    {
        try
        {
            if (ContainerLocator.Container == null) return null;
            return ContainerLocator.Container.Resolve<IIdentityService>();
        }
        catch
        {
            return null;
        }
    }
}
