using System.Net.Http.Json;
using LinCms.Entities.Uptime;
using Microsoft.Extensions.Configuration;

namespace LinCms.Services.Uptime
{
    /// <summary>
    /// Telegram 通知发送器。
    /// </summary>
    public class TelegramNotifier
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private readonly IConfiguration _configuration;
        private readonly ILogger<TelegramNotifier> _logger;

        public TelegramNotifier(IConfiguration configuration, ILogger<TelegramNotifier> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendFailureAlertAsync(MonitorSite site, int consecutiveFailures, string? errorMessage, CancellationToken cancellationToken = default)
        {
            var botToken = _configuration["Telegram:BotToken"];
            var chatId = _configuration["Telegram:ChatId"];

            if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
            {
                _logger.LogWarning("Telegram 配置未启用或不完整，跳过告警发送。Site={SiteName}", site.Name);
                return false;
            }

            var text = $"⚠️ 站点连续失败告警\n\n" +
                       $"站点：{site.Name}\n" +
                       $"地址：{site.Url}\n" +
                       $"连续失败次数：{consecutiveFailures}\n" +
                       $"当前状态：{site.LastStatus}\n" +
                       $"Ping：{site.PingStatus}\n" +
                       $"HTTP：{site.HttpStatus}\n" +
                       $"SSL：{site.SslStatus}\n" +
                       $"延时：{site.LatencyMs?.ToString() ?? "-"} ms\n" +
                       $"错误信息：{(string.IsNullOrWhiteSpace(errorMessage) ? "无" : errorMessage)}\n" +
                       $"时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}";

            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
            var payload = new Dictionary<string, string>
            {
                ["chat_id"] = chatId,
                ["text"] = text,
                ["disable_web_page_preview"] = "true"
            };

            try
            {
                var response = await HttpClient.PostAsync(url, new FormUrlEncodedContent(payload), cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Telegram 告警发送失败，状态码：{StatusCode}", response.StatusCode);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送 Telegram 告警失败。Site={SiteName}", site.Name);
                return false;
            }
        }
    }
}