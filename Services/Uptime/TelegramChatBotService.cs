using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace LinCms.Services.Uptime
{
    /// <summary>
    /// Telegram 聊天机器人：收到消息后返回 chat id。
    /// </summary>
    public class TelegramChatBotService : BackgroundService
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private readonly IConfiguration _configuration;
        private readonly ILogger<TelegramChatBotService> _logger;

        public TelegramChatBotService(IConfiguration configuration, ILogger<TelegramChatBotService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var botToken = _configuration["Telegram:BotToken"];
            if (string.IsNullOrWhiteSpace(botToken))
            {
                _logger.LogWarning("Telegram BotToken 未配置，聊天机器人不启动。");
                return;
            }

            var offset = 0;
            _logger.LogInformation("Telegram 聊天机器人已启动。");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var updates = await GetUpdatesAsync(botToken, offset, stoppingToken);
                    foreach (var update in updates)
                    {
                        if (update.UpdateId >= offset)
                        {
                            offset = update.UpdateId + 1;
                        }

                        var message = update.Message?.Text;
                        var chatId = update.Message?.Chat?.Id;
                        if (chatId is null)
                        {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(message))
                        {
                            await SendMessageAsync(botToken, chatId.Value.ToString(), $"你的 ChatID 是：{chatId}", stoppingToken);
                            continue;
                        }

                        var reply = $"你的 ChatID 是：{chatId}";
                        await SendMessageAsync(botToken, chatId.Value.ToString(), reply, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Telegram 聊天机器人轮询失败。");
                }

                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }

        private static async Task<List<TelegramUpdate>> GetUpdatesAsync(string botToken, int offset, CancellationToken cancellationToken)
        {
            var url = $"https://api.telegram.org/bot{botToken}/getUpdates?timeout=20&offset={offset}";
            using var response = await HttpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"Telegram getUpdates 失败：{(int)response.StatusCode} {response.ReasonPhrase}，{errorText}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<TelegramApiResponse<TelegramUpdate[]>>(json);
            return result?.Ok == true && result.Result is not null ? result.Result.ToList() : [];
        }

        private static async Task<bool> SendMessageAsync(string botToken, string chatId, string text, CancellationToken cancellationToken)
        {
            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
            var payload = new Dictionary<string, string>
            {
                ["chat_id"] = chatId,
                ["text"] = text,
                ["disable_web_page_preview"] = "true"
            };

            using var response = await HttpClient.PostAsync(url, new FormUrlEncodedContent(payload), cancellationToken);
            return response.IsSuccessStatusCode;
        }

        private sealed class TelegramApiResponse<T>
        {
            [JsonPropertyName("ok")]
            public bool Ok { get; set; }

            [JsonPropertyName("result")]
            public T? Result { get; set; }
        }

        private sealed class TelegramUpdate
        {
            [JsonPropertyName("update_id")]
            public int UpdateId { get; set; }

            [JsonPropertyName("message")]
            public TelegramMessage? Message { get; set; }
        }

        private sealed class TelegramMessage
        {
            [JsonPropertyName("message_id")]
            public long MessageId { get; set; }

            [JsonPropertyName("chat")]
            public TelegramChat? Chat { get; set; }

            [JsonPropertyName("text")]
            public string? Text { get; set; }
        }

        private sealed class TelegramChat
        {
            [JsonPropertyName("id")]
            public long Id { get; set; }

            [JsonPropertyName("type")]
            public string? Type { get; set; }
        }
    }
}