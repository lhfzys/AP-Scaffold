namespace AP.Shared.UI.Services;

public interface ICustomDialogService
{
    /// <summary>
    /// 显示提示消息 (仅确定)
    /// </summary>
    Task ShowAlertAsync(string message, string title = "提示");

    /// <summary>
    /// 显示确认消息 (确定/取消)
    /// </summary>
    /// <returns>用户点击确定返回 true</returns>
    Task<bool> ShowConfirmAsync(string message, string title = "确认操作");

    /// <summary>
    /// 显示错误消息 (红色图标)
    /// </summary>
    Task ShowErrorAsync(string message);
}