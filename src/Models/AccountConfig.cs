namespace TelegramMonitor;

/// <summary>
/// 账号配置
/// </summary>
public class AccountConfig
{
    /// <summary>
    /// 账号ID
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// 账号名称（备注）
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// API ID
    /// </summary>
    public int ApiId { get; set; }
    
    /// <summary>
    /// API Hash
    /// </summary>
    public string ApiHash { get; set; } = string.Empty;
    
    /// <summary>
    /// 手机号
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 最后登录时间
    /// </summary>
    public DateTime? LastLoginTime { get; set; }
    
    /// <summary>
    /// 登录状态
    /// </summary>
    public bool IsLoggedIn { get; set; }
}