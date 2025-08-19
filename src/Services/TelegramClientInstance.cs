using WTelegram;
using TL;

namespace TelegramMonitor;

/// <summary>
/// 单个Telegram客户端实例
/// </summary>
public sealed class TelegramClientInstance : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly Func<TelegramMessage, Task> _messageHandler;
    private Client _client;
    private UpdateManager _manager;
    private volatile bool _isMonitoring;
    
    public AccountConfig Account { get; }
    public bool IsLoggedIn => _client is { Disconnected: false } && _client.User != null;
    public bool IsMonitoring => _isMonitoring && IsLoggedIn;

    private readonly Dictionary<long, User> _users = new();
    private readonly Dictionary<long, ChatBase> _chats = new();

    public TelegramClientInstance(AccountConfig account, ILogger logger, Func<TelegramMessage, Task> messageHandler)
    {
        Account = account;
        _logger = logger;
        _messageHandler = messageHandler;
        
        CreateClient();
    }

    /// <summary>
    /// 创建客户端
    /// </summary>
    private void CreateClient()
    {
        var sessionPath = Path.Combine(
            Path.GetDirectoryName(TelegramMonitorConstants.SessionPath) ?? "",
            "sessions",
            $"account_{Account.Id}_{Account.PhoneNumber}.session");
        
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        
        _client = new Client(Account.ApiId, Account.ApiHash, sessionPath);
        _logger.LogInformation("为账号 {Name} 创建客户端，会话文件: {SessionPath}", 
            Account.Name, sessionPath);
    }

    /// <summary>
    /// 登录
    /// </summary>
    public async Task<LoginState> LoginAsync(string loginInfo = "")
    {
        try
        {
            var phoneNumber = Account.PhoneNumber.Replace(" ", "").Trim();
            if (!phoneNumber.IsE164Phone())
            {
                throw new ArgumentException($"账号 {Account.Name} 的手机号格式不正确");
            }

            var firstArg = string.IsNullOrWhiteSpace(loginInfo) ? phoneNumber : loginInfo;
            var result = await _client.Login(firstArg);

            while (result is "name")
                result = await _client.Login($"TelegramMonitor_{Account.Name}");

            var loginState = result switch
            {
                "verification_code" => LoginState.WaitingForVerificationCode,
                "password" => LoginState.WaitingForPassword,
                null => IsLoggedIn ? LoginState.LoggedIn : LoginState.NotLoggedIn,
                _ => LoginState.NotLoggedIn
            };

            if (loginState == LoginState.LoggedIn)
            {
                Account.LastLoginTime = DateTime.Now;
                Account.IsLoggedIn = true;
                _logger.LogInformation("账号 {Name} 登录成功", Account.Name);
            }

            return loginState;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "账号 {Name} 登录失败", Account.Name);
            Account.IsLoggedIn = false;
            return LoginState.NotLoggedIn;
        }
    }

    /// <summary>
    /// 获取会话列表
    /// </summary>
    public async Task<List<DisplayDialogs>> GetDialogsAsync()
    {
        if (!IsLoggedIn)
        {
            throw new InvalidOperationException($"账号 {Account.Name} 未登录");
        }

        var dialogs = await _client.Messages_GetAllDialogs();
        dialogs.CollectUsersChats(_users, _chats);

        var availableChats = dialogs.chats.Values
            .Where(c => c.IsActive && CanSendMessagesFast(c))
            .ToList();

        return availableChats.Select(c => new DisplayDialogs
        {
            Id = c.ID,
            DisplayTitle = $"[{GetChatType(c)}]{(string.IsNullOrEmpty(c.MainUsername) ? "" : $"(@{c.MainUsername})")}{c.Title}",
        }).ToList();
    }

    /// <summary>
    /// 启动监控
    /// </summary>
    public async Task<MonitorStartResult> StartMonitoringAsync()
    {
        if (!IsLoggedIn)
        {
            _logger.LogWarning("账号 {Name} 未登录，无法启动监控", Account.Name);
            return MonitorStartResult.Error;
        }

        if (_isMonitoring)
        {
            return MonitorStartResult.AlreadyRunning;
        }

        try
        {
            _manager = _client.WithUpdateManager(HandleUpdateAsync, 
                collector: new MyCollector(_users, _chats));
            
            var dialogs = await _client.Messages_GetAllDialogs();
            dialogs.CollectUsersChats(_users, _chats);

            _isMonitoring = true;
            _logger.LogInformation("账号 {Name} 监控启动成功", Account.Name);
            return MonitorStartResult.Started;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "账号 {Name} 监控启动失败", Account.Name);
            _isMonitoring = false;
            return MonitorStartResult.Error;
        }
    }

    /// <summary>
    /// 停止监控
    /// </summary>
    public async Task StopMonitoringAsync()
    {
        _isMonitoring = false;
        
        if (_manager != null)
        {
            await _client.DisposeAsync();
            _manager = null;
            CreateClient();
            await LoginAsync();
        }
        
        _logger.LogInformation("账号 {Name} 监控已停止", Account.Name);
    }

    /// <summary>
    /// 处理更新
    /// </summary>
    private async Task HandleUpdateAsync(Update update)
    {
        try
        {
            if (update is UpdateNewMessage unm && unm.message is Message message)
            {
                await HandleMessageAsync(message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "账号 {Name} 处理更新时发生异常", Account.Name);
        }
    }

    /// <summary>
    /// 处理消息
    /// </summary>
    private async Task HandleMessageAsync(Message message)
    {
        try
        {
            if (message.Peer is null) return;

            // 解析发送者和群组信息
            if (!TryResolvePeer(message.Peer, out var fromId, out var fromTitle, out _, out _))
            {
                return;
            }

            long sendId; string sendTitle;
            if (message.From is null)
            {
                // 频道消息或自己发送的消息
                sendId = fromId;
                sendTitle = fromTitle;
            }
            else if (!TryResolvePeer(message.From, out sendId, out sendTitle, out _, out _))
            {
                return;
            }

            // 创建消息对象
            var telegramMessage = new TelegramMessage
            {
                AccountId = Account.Id,
                SenderId = sendId,
                SenderName = sendTitle,
                SourceChatId = fromId,
                SourceChatName = fromTitle,
                Content = message.message ?? "",
                MessageTime = message.Date
            };

            // 传递给消息处理器
            await _messageHandler(telegramMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "账号 {Name} 处理消息时发生异常", Account.Name);
        }
    }

    /// <summary>
    /// 解析Peer信息
    /// </summary>
    private bool TryResolvePeer(Peer peer, out long id, out string title, 
        out string mainUserName, out IEnumerable<string> allUserNames)
    {
        id = 0; title = ""; mainUserName = ""; allUserNames = Enumerable.Empty<string>();
        
        if (peer is null) return false;

        if (peer is PeerUser pu)
        {
            if (_users.TryGetValue(pu.user_id, out var u))
            {
                id = u.ID;
                title = u.DisplayName();
                mainUserName = u.MainUsername ?? "";
                allUserNames = u.ActiveUsernames ?? Enumerable.Empty<string>();
                return true;
            }
            return false;
        }
        
        if (peer is PeerChat pc)
        {
            if (_chats.TryGetValue(pc.chat_id, out var chat))
            {
                id = chat.ID;
                title = chat.Title;
                mainUserName = chat.MainUsername ?? "";
                allUserNames = chat.MainUsername != null 
                    ? new[] { chat.MainUsername } 
                    : Enumerable.Empty<string>();
                return true;
            }
            return false;
        }
        
        if (peer is PeerChannel pch)
        {
            if (_chats.TryGetValue(pch.channel_id, out var channel))
            {
                id = channel.ID;
                title = channel.Title;
                mainUserName = channel.MainUsername ?? "";
                allUserNames = channel is Channel ch 
                    ? (ch.ActiveUsernames ?? Enumerable.Empty<string>())
                    : (channel.MainUsername != null 
                        ? new[] { channel.MainUsername } 
                        : Enumerable.Empty<string>());
                return true;
            }
            return false;
        }
        
        return false;
    }

    private static string GetChatType(ChatBase chat) => chat switch
    {
        Chat => "Chat",
        Channel ch when ch.IsChannel => "Channel",
        Channel => "Group",
        _ => "Unknown"
    };

    private static bool CanSendMessagesFast(ChatBase chat) => chat switch
    {
        Chat small => !small.IsBanned(ChatBannedRights.Flags.send_messages),
        Channel ch when ch.IsChannel => !ch.flags.HasFlag(Channel.Flags.left) && 
                                       (ch.flags.HasFlag(Channel.Flags.creator) || 
                                        ch.admin_rights?.flags.HasFlag(ChatAdminRights.Flags.post_messages) == true),
        Channel ch => !ch.flags.HasFlag(Channel.Flags.left) && 
                     (ch.flags.HasFlag(Channel.Flags.creator) || 
                      ch.admin_rights?.flags != 0 || 
                      !ch.IsBanned(ChatBannedRights.Flags.send_messages)),
        _ => false
    };

    public async ValueTask DisposeAsync()
    {
        _isMonitoring = false;
        if (_client != null)
        {
            await _client.DisposeAsync();
        }
        _manager = null;
    }
}