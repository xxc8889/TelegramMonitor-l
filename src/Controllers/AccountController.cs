using Microsoft.AspNetCore.Mvc;

namespace TelegramMonitor.Controllers;

/// <summary>
/// 账号管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly MultiAccountManager _multiAccountManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(MultiAccountManager multiAccountManager, ILogger<AccountController> logger)
    {
        _multiAccountManager = multiAccountManager;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有账号状态
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetAccounts()
    {
        try
        {
            var accounts = await _multiAccountManager.GetAccountStatusesAsync();
            return Ok(new { succeeded = true, data = accounts });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取账号列表失败");
            return BadRequest(new { succeeded = false, message = "获取账号列表失败" });
        }
    }

    /// <summary>
    /// 添加账号
    /// </summary>
    [HttpPost("add")]
    public async Task<IActionResult> AddAccount([FromBody] AddAccountRequest request)
    {
        try
        {
            var account = new AccountConfig
            {
                Id = GenerateAccountId(),
                Name = request.Name,
                ApiId = request.ApiId,
                ApiHash = request.ApiHash,
                PhoneNumber = request.PhoneNumber,
                IsEnabled = true
            };

            var success = await _multiAccountManager.AddAccountAsync(account);
            if (success)
            {
                return Ok(new { succeeded = true, message = "账号添加成功", data = account });
            }
            
            return BadRequest(new { succeeded = false, message = "账号添加失败" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加账号失败");
            return BadRequest(new { succeeded = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 删除账号
    /// </summary>
    [HttpDelete("delete/{accountId}")]
    public async Task<IActionResult> DeleteAccount(int accountId)
    {
        try
        {
            var success = await _multiAccountManager.RemoveAccountAsync(accountId);
            if (success)
            {
                return Ok(new { succeeded = true, message = "账号删除成功" });
            }
            
            return BadRequest(new { succeeded = false, message = "账号不存在" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除账号失败");
            return BadRequest(new { succeeded = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 登录账号
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> LoginAccount([FromBody] LoginAccountRequest request)
    {
        try
        {
            var result = await _multiAccountManager.LoginAccountAsync(request.AccountId, request.LoginInfo);
            
            return Ok(new { 
                succeeded = true, 
                data = new { 
                    loginState = result,
                    message = GetLoginStateMessage(result)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "账号登录失败");
            return BadRequest(new { succeeded = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 获取账号会话列表
    /// </summary>
    [HttpGet("{accountId}/dialogs")]
    public async Task<IActionResult> GetDialogs(int accountId)
    {
        try
        {
            var dialogs = await _multiAccountManager.GetDialogsAsync(accountId);
            return Ok(new { succeeded = true, data = dialogs });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取会话列表失败");
            return BadRequest(new { succeeded = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 设置目标群组
    /// </summary>
    [HttpPost("target")]
    public async Task<IActionResult> SetTarget([FromBody] SetTargetRequest request)
    {
        try
        {
            await _multiAccountManager.SetTargetChatIdAsync(request.ChatId);
            return Ok(new { succeeded = true, message = "目标群组设置成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置目标群组失败");
            return BadRequest(new { succeeded = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 启动多账号监控
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> StartMonitoring()
    {
        try
        {
            var result = await _multiAccountManager.StartMonitoringAsync();
            
            return Ok(new { 
                succeeded = result == MonitorStartResult.Started, 
                data = new { 
                    result = result,
                    message = GetMonitorStartResultMessage(result)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动监控失败");
            return BadRequest(new { succeeded = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 停止监控
    /// </summary>
    [HttpPost("stop")]
    public async Task<IActionResult> StopMonitoring()
    {
        try
        {
            await _multiAccountManager.StopMonitoringAsync();
            return Ok(new { succeeded = true, message = "监控已停止" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止监控失败");
            return BadRequest(new { succeeded = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 获取监控状态
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        try
        {
            var accounts = await _multiAccountManager.GetAccountStatusesAsync();
            var status = new
            {
                IsMonitoring = _multiAccountManager.IsMonitoring,
                Accounts = accounts
            };
            
            return Ok(new { succeeded = true, data = status });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取监控状态失败");
            return BadRequest(new { succeeded = false, message = ex.Message });
        }
    }

    private int GenerateAccountId()
    {
        return (int)(DateTime.Now.Ticks % int.MaxValue);
    }

    private string GetLoginStateMessage(LoginState state) => state switch
    {
        LoginState.LoggedIn => "登录成功",
        LoginState.WaitingForVerificationCode => "请输入验证码",
        LoginState.WaitingForPassword => "请输入两步验证密码",
        LoginState.NotLoggedIn => "登录失败",
        _ => "未知状态"
    };

    private string GetMonitorStartResultMessage(MonitorStartResult result) => result switch
    {
        MonitorStartResult.Started => "监控启动成功",
        MonitorStartResult.AlreadyRunning => "监控已在运行",
        MonitorStartResult.MissingTarget => "未设置目标群组",
        MonitorStartResult.NoAccounts => "无可用账号",
        MonitorStartResult.Error => "启动失败",
        _ => "未知状态"
    };
}

/// <summary>
/// 添加账号请求
/// </summary>
public class AddAccountRequest
{
    public string Name { get; set; } = string.Empty;
    public int ApiId { get; set; }
    public string ApiHash { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

/// <summary>
/// 登录账号请求
/// </summary>
public class LoginAccountRequest
{
    public int AccountId { get; set; }
    public string LoginInfo { get; set; } = string.Empty;
}

/// <summary>
/// 设置目标群组请求
/// </summary>
public class SetTargetRequest
{
    public long ChatId { get; set; }
}