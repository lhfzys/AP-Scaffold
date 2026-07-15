namespace AP.Contracts.System.Services;

/// <summary>
/// 登录对话框服务
/// </summary>
public interface ILoginService
{
    /// <summary>
    /// 显示登录对话框，返回是否登录成功
    /// </summary>
    bool ShowLoginDialog();

    /// <summary>
    /// 显示修改密码对话框，返回是否修改成功
    /// </summary>
    /// <param name="userName">需要修改密码的用户名</param>
    bool ShowChangePasswordDialog(string userName);
}
