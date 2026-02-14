using System.Windows;
using System.Windows.Controls;

namespace AP.Shared.UI.Controls.LoadingSpinner;

public partial class LoadingSpinner : UserControl
{
    public LoadingSpinner()
    {
        InitializeComponent();
    }

    // --- 依赖属性: IsLoading ---
    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(LoadingSpinner),
            new PropertyMetadata(false));

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    // --- 依赖属性: LoadingText ---
    public static readonly DependencyProperty LoadingTextProperty =
        DependencyProperty.Register(nameof(LoadingText), typeof(string), typeof(LoadingSpinner),
            new PropertyMetadata("加载中..."));

    public string LoadingText
    {
        get => (string)GetValue(LoadingTextProperty);
        set => SetValue(LoadingTextProperty, value);
    }
}