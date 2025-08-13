namespace TelegramMonitor;

public static class TelegramMonitorConstants
{
    public const string MonitorApi = "https://raw.githubusercontent.com/Riniba/TelegramMonitor/refs/heads/main/ad/ad.txxt";
    public const int ApiId = 23319500;
    public const string ApiHash = "814ac0dd67f660119b9b990d514c9a47";
    public static readonly string SessionPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "session");
}