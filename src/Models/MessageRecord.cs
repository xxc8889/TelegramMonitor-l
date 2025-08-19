namespace TelegramMonitor;

/// <summary>
/// 消息记录，用于去重
/// </summary>
public class MessageRecord
{
    /// <summary>
    /// 主键
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// 消息内容哈希
    /// </summary>
    public string MessageHash { get; set; } = string.Empty;
    
    /// <summary>
    /// 发送者ID
    /// </summary>
    public long SenderId { get; set; }
    
    /// <summary>
    /// 源群组ID
    /// </summary>
    public long SourceChatId { get; set; }
    
    /// <summary>
    /// 消息时间
    /// </summary>
    public DateTime MessageTime { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; } = DateTime.Now;
}