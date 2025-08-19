using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace TelegramMonitor;

/// <summary>
/// 多账号管理器
/// </summary>
public sealed class MultiAccountManager : ISingleton, IAsyncDisposable
{
    private readonly ILogger<MultiAccountManager> _logger;
    private readonly SystemCacheServices _systemCacheServices;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ISqlSugarClient _db;
    private readonly ConcurrentDictionary<int, TelegramClientInstance> _clients = new();
    private readonly ConcurrentDictionary<string, DateTime> _messageHashCache = new();
    private long _targetChatId;
    private volatile bool _isMonitoring;
    private string BotToken => _configuration["BotSettings:BotToken"] ?? "";

    public bool IsMonitoring => _isMonitoring;

    public MultiAccountManager(ILogger<MultiAccountManager> logger, SystemCacheServices systemCacheServices, IConfiguration configuration, ISqlSugarClient db)
    {
        _logger = logger;
        _systemCacheServices = systemCacheServices;
        _configuration = configuration;
        _db = db;
        _httpClient = new HttpClient();
        
        // 启动时加载现有账号和配置
        LoadExistingAccountsAsync();
        Task.Run(LoadTargetChatIdAsync);
        
        // 定期清理消息缓存（避免内存过大）
        Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromHours(1));
                CleanupMessageCache();
            }
        });
    }

    /// <summary>
    /// 加载现有账号
    /// </summary>
    private async void LoadExistingAccountsAsync()
    {
        try
        {
            var accounts = await _db.Queryable<AccountConfig>().ToListAsync();
            foreach (var account in accounts.Where(a => a.IsEnabled))
            {
                var client = new TelegramClientInstance(account, _logger, HandleMessageAsync);
                _clients[account.Id] = client;
                _logger.LogInformation("加载账号: {Name} (ID:{Id})", account.Name, account.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载现有账号失败");
        }
    }

    /// <summary>
    /// 添加账号
    /// </summary>
    public async Task<bool> AddAccountAsync(AccountConfig account)
    {
        try
        {
            // 保存到数据库
            await _db.Insertable(account).ExecuteCommandAsync();
            
            var client = new TelegramClientInstance(account, _logger, HandleMessageAsync);
            _clients[account.Id] = client;
            
            _logger.LogInformation("账号 {Name} (ID:{Id}) 已添加", account.Name, account.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加账号失败: {Name}", account.Name);
            return false;
        }
    }

    /// <summary>
    /// 移除账号
    /// </summary>
    public async Task<bool> RemoveAccountAsync(int accountId)
    {
        try
        {
            // 从数据库删除
            await _db.Deleteable<AccountConfig>().Where(a => a.Id == accountId).ExecuteCommandAsync();
            
            if (_clients.TryRemove(accountId, out var client))
            {
                await client.DisposeAsync();
                _logger.LogInformation("账号 ID:{Id} 已移除", accountId);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "移除账号失败: ID:{Id}", accountId);
            return false;
        }
    }

    /// <summary>
    /// 获取所有账号状态
    /// </summary>
    public async Task<List<AccountStatus>> GetAccountStatusesAsync()
    {
        try
        {
            var dbAccounts = await _db.Queryable<AccountConfig>().ToListAsync();
            return dbAccounts.Select(account => new AccountStatus
            {
                AccountId = account.Id,
                Name = account.Name,
                PhoneNumber = account.PhoneNumber,
                IsLoggedIn = _clients.TryGetValue(account.Id, out var client) && client.IsLoggedIn,
                IsEnabled = account.IsEnabled,
                LastLoginTime = account.LastLoginTime
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取账号状态失败");
            return new List<AccountStatus>();
        }
    }

    /// <summary>
    /// 登录指定账号
    /// </summary>
    public async Task<LoginState> LoginAccountAsync(int accountId, string loginInfo = "")
    {
        if (!_clients.TryGetValue(accountId, out var client))
        {
            throw new ArgumentException($"账号 ID:{accountId} 不存在");
        }

        return await client.LoginAsync(loginInfo);
    }

    /// <summary>
    /// 设置目标群组
    /// </summary>
    public async Task SetTargetChatIdAsync(long chatId)
    {
        try
        {
            // 保存到数据库
            var existing = await _db.Queryable<SystemConfig>()
                .Where(c => c.Key == "TargetChatId")
                .FirstAsync();
            
            if (existing != null)
            {
                existing.Value = chatId.ToString();
                existing.UpdateTime = DateTime.Now;
                await _db.Updateable(existing).ExecuteCommandAsync();
            }
            else
            {
                var config = new SystemConfig
                {
                    Key = "TargetChatId",
                    Value = chatId.ToString(),
                    Description = "目标群组ID"
                };
                await _db.Insertable(config).ExecuteCommandAsync();
            }
            
            _targetChatId = chatId;
            _logger.LogInformation("目标群组已设置为: {ChatId}", chatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置目标群组失败: {ChatId}", chatId);
            throw;
        }
    }

    /// <summary>
    /// 加载目标群组配置
    /// </summary>
    private async Task LoadTargetChatIdAsync()
    {
        try
        {
            var config = await _db.Queryable<SystemConfig>()
                .Where(c => c.Key == "TargetChatId")
                .FirstAsync();
            
            if (config != null && long.TryParse(config.Value, out var chatId))
            {
                _targetChatId = chatId;
                _logger.LogInformation("加载目标群组: {ChatId}", chatId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载目标群组配置失败");
        }
    }

    /// <summary>
    /// 获取指定账号的会话列表
    /// </summary>
    public async Task<List<DisplayDialogs>> GetDialogsAsync(int accountId)
    {
        if (!_clients.TryGetValue(accountId, out var client))
        {
            throw new ArgumentException($"账号 ID:{accountId} 不存在");
        }

        return await client.GetDialogsAsync();
    }

    /// <summary>
    /// 启动所有账号监控
    /// </summary>
    public async Task<MonitorStartResult> StartMonitoringAsync()
    {
        if (_targetChatId == 0)
        {
            return MonitorStartResult.MissingTarget;
        }

        if (_isMonitoring)
        {
            return MonitorStartResult.AlreadyRunning;
        }

        var enabledClients = _clients.Values.Where(c => c.Account.IsEnabled).ToList();
        if (!enabledClients.Any())
        {
            return MonitorStartResult.NoAccounts;
        }

        var startedCount = 0;
        foreach (var client in enabledClients)
        {
            try
            {
                var result = await client.StartMonitoringAsync();
                if (result == MonitorStartResult.Started)
                {
                    startedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动账号 {Name} 监控失败", client.Account.Name);
            }
        }

        if (startedCount > 0)
        {
            _isMonitoring = true;
            _logger.LogInformation("多账号监控已启动，成功启动 {Count}/{Total} 个账号", 
                startedCount, enabledClients.Count);
            return MonitorStartResult.Started;
        }

        return MonitorStartResult.Error;
    }

    /// <summary>
    /// 停止所有监控
    /// </summary>
    public async Task StopMonitoringAsync()
    {
        _isMonitoring = false;
        
        var tasks = _clients.Values.Select(c => c.StopMonitoringAsync()).ToArray();
        await Task.WhenAll(tasks);
        
        _logger.LogInformation("所有账号监控已停止");
    }

    /// <summary>
    /// 处理消息（去重逻辑）
    /// </summary>
    private async Task HandleMessageAsync(TelegramMessage message)
    {
        try
        {
            // 生成消息哈希用于去重
            var messageHash = GenerateMessageHash(message);
            
            // 检查是否已处理过此消息
            if (_messageHashCache.ContainsKey(messageHash))
            {
                _logger.LogDebug("消息已处理过，跳过: {Hash}", messageHash);
                return;
            }

            // 添加到缓存
            _messageHashCache[messageHash] = DateTime.Now;

            // 处理消息转发逻辑
            await ProcessMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理消息时发生异常");
        }
    }

    /// <summary>
    /// 生成消息哈希
    /// </summary>
    private string GenerateMessageHash(TelegramMessage message)
    {
        var content = $"{message.SenderId}_{message.SourceChatId}_{message.Content}_{message.MessageTime:yyyyMMddHHmmss}";
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// 处理消息转发
    /// </summary>
    private async Task ProcessMessageAsync(TelegramMessage message)
    {
        try
        {
            if (_targetChatId == 0)
            {
                _logger.LogWarning("未设置目标群组，跳过消息处理");
                return;
            }

            var keywords = await _systemCacheServices.GetKeywordsAsync() ?? new List<KeywordConfig>();
            
            // 检查用户ID是否被排除
            var matchedUserKeywords = KeywordMatchExtensions.MatchUser(
                message.SenderId,
                new List<string>(), // 这里需要从消息中获取用户名信息
                keywords);
            
            if (matchedUserKeywords.Any(k => k.KeywordAction == KeywordAction.Exclude))
            {
                _logger.LogInformation("用户 (ID:{UserId}) 在排除列表内，跳过", message.SenderId);
                return;
            }
            
            // 检查用户名称关键词匹配
            var matchedUserNameKeywords = KeywordMatchExtensions.MatchUserName(
                message.SenderName,
                keywords);
            
            if (matchedUserNameKeywords.Any(k => k.KeywordAction == KeywordAction.Exclude))
            {
                _logger.LogInformation("用户名称 '{UserName}' 包含屏蔽关键词，跳过", message.SenderName);
                return;
            }
            
            // 合并所有匹配的监控关键词
            var allMatchedMonitorKeywords = new List<KeywordConfig>();
            
            // 添加用户ID监控
            if (matchedUserKeywords.Any(k => k.KeywordAction == KeywordAction.Monitor))
            {
                allMatchedMonitorKeywords.AddRange(matchedUserKeywords.Where(k => k.KeywordAction == KeywordAction.Monitor));
            }
            
            // 添加用户名称监控
            if (matchedUserNameKeywords.Any(k => k.KeywordAction == KeywordAction.Monitor))
            {
                allMatchedMonitorKeywords.AddRange(matchedUserNameKeywords.Where(k => k.KeywordAction == KeywordAction.Monitor));
            }
            
            // 如果有用户相关的监控关键词，直接发送
            if (allMatchedMonitorKeywords.Count > 0)
            {
                await SendMessageToTargetAsync(message, allMatchedMonitorKeywords);
                return;
            }
            
            // 检查消息内容关键词匹配
            var matchedKeywords = KeywordMatchExtensions.MatchText(message.Content, keywords);
            
            if (matchedKeywords.Any(k => k.KeywordAction == KeywordAction.Exclude))
            {
                _logger.LogInformation("消息包含排除关键词，跳过处理");
                return;
            }
            
            matchedKeywords = matchedKeywords
                .Where(k => k.KeywordAction == KeywordAction.Monitor)
                .ToList();
            
            if (matchedKeywords.Count == 0)
            {
                _logger.LogInformation("无匹配关键词，跳过");
                return;
            }
            
            await SendMessageToTargetAsync(message, matchedKeywords);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理消息转发时发生异常");
        }
    }
    
    /// <summary>
    /// 发送消息到目标群组
    /// </summary>
    private async Task SendMessageToTargetAsync(TelegramMessage message, List<KeywordConfig> matchedKeywords)
    {
        try
        {
            if (string.IsNullOrEmpty(BotToken))
            {
                _logger.LogWarning("Bot Token未配置，无法发送消息");
                return;
            }

            // 构造转发消息内容
            var content = $"📢 **监控消息**\n\n" +
                         $"👤 **发送者**: {message.SenderName}\n" +
                         $"🏢 **来源群组**: {message.SourceChatName}\n" +
                         $"🎯 **匹配关键词**: {string.Join(", ", matchedKeywords.Select(k => k.KeywordContent))}\n" +
                         $"⏰ **时间**: {message.MessageTime:yyyy-MM-dd HH:mm:ss}\n\n" +
                         $"📝 **消息内容**:\n{message.Content}";

            // 发送到Telegram Bot API
            var botApiUrl = $"https://api.telegram.org/bot{BotToken}/sendMessage";
            var payload = new
            {
                chat_id = _targetChatId,
                text = content,
                parse_mode = "Markdown"
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var httpContent = new System.Net.Http.StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(botApiUrl, httpContent);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "✅ 消息已转发: 账号ID:{AccountId} 发送者:{SenderName} 匹配关键词:{Keywords}",
                    message.AccountId,
                    message.SenderName,
                    string.Join(", ", matchedKeywords.Select(k => k.KeywordContent))
                );
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "❌ Bot API发送失败: Status={Status} Error={Error}",
                    response.StatusCode, error
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送消息到目标群组失败");
        }
    }

    /// <summary>
    /// 清理消息缓存
    /// </summary>
    private void CleanupMessageCache()
    {
        var cutoffTime = DateTime.Now.AddHours(-24); // 保留24小时内的消息哈希
        var keysToRemove = _messageHashCache
            .Where(kvp => kvp.Value < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _messageHashCache.TryRemove(key, out _);
        }

        _logger.LogDebug("清理了 {Count} 个过期消息哈希", keysToRemove.Count);
    }

    public async ValueTask DisposeAsync()
    {
        _isMonitoring = false;
        
        var disposeTasks = _clients.Values.Select(c => c.DisposeAsync().AsTask()).ToArray();
        await Task.WhenAll(disposeTasks);
        
        _clients.Clear();
        _messageHashCache.Clear();
        _httpClient?.Dispose();
    }
}

/// <summary>
/// 账号状态
/// </summary>
public class AccountStatus
{
    public int AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsLoggedIn { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? LastLoginTime { get; set; }
}

/// <summary>
/// Telegram消息
/// </summary>
public class TelegramMessage
{
    public int AccountId { get; set; }
    public long SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public long SourceChatId { get; set; }
    public string SourceChatName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime MessageTime { get; set; }
}

