namespace TelegramMonitor;

/// <summary>
/// 系统配置
/// </summary>
public class SystemConfig
{
    /// <summary>
    /// 配置键
    /// </summary>
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// 配置值
    /// </summary>
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdateTime { get; set; } = DateTime.Now;
}