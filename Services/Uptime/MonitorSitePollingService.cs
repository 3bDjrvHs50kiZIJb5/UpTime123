using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using FreeSql;
using LinCms.Entities.Uptime;

namespace LinCms.Services.Uptime
{
    /// <summary>
    /// 每分钟轮询站点状态并写入数据库。
    /// </summary>
    public class MonitorSitePollingService : BackgroundService
    {
        private static readonly HttpClient HttpClient = new(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private readonly FreeSqlCloud _freeSqlCloud;
        private readonly ILogger<MonitorSitePollingService> _logger;
        private readonly TelegramNotifier _telegramNotifier;

        public MonitorSitePollingService(
            FreeSqlCloud freeSqlCloud,
            ILogger<MonitorSitePollingService> logger,
            TelegramNotifier telegramNotifier)
        {
            _freeSqlCloud = freeSqlCloud;
            _logger = logger;
            _telegramNotifier = telegramNotifier;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

            await RunOnceAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnceAsync(stoppingToken);
            }
        }

        private async Task RunOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                var orm = _freeSqlCloud.Use("main");
                var sites = await orm.Select<MonitorSite>()
                    .Where(a => a.IsEnabled)
                    .OrderBy(a => a.SortCode)
                    .ToListAsync(stoppingToken);

                foreach (var site in sites)
                {
                    await CheckAndStoreAsync(orm, site, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "站点轮询任务执行失败。");
            }
        }

        private async Task CheckAndStoreAsync(IFreeSql orm, MonitorSite site, CancellationToken stoppingToken)
        {
            var now = DateTime.Now;
            var result = await CheckSiteAsync(site, stoppingToken);

            site.LastCheckTime = now;
            site.PingStatus = result.PingStatus;
            site.HttpStatus = result.HttpStatus;
            site.SslStatus = result.SslStatus;
            site.LastResponseTimeMs = result.LatencyMs;
            site.LatencyMs = result.LatencyMs;
            site.SslDaysLeft = result.SslDaysLeft;
            site.LastStatus = result.IsUp ? "Up" : "Down";
            site.ConsecutiveFailures = result.IsUp ? 0 : site.ConsecutiveFailures + 1;

            if (!result.IsUp && site.ConsecutiveFailures == 10)
            {
                await _telegramNotifier.SendFailureAlertAsync(site, site.ConsecutiveFailures, result.ErrorMessage ?? result.SslErrorMessage, stoppingToken);
            }

            await orm.Update<MonitorSite>()
                .Set(a => a.LastCheckTime, site.LastCheckTime)
                .Set(a => a.PingStatus, site.PingStatus)
                .Set(a => a.HttpStatus, site.HttpStatus)
                .Set(a => a.SslStatus, site.SslStatus)
                .Set(a => a.LastResponseTimeMs, site.LastResponseTimeMs)
                .Set(a => a.LatencyMs, site.LatencyMs)
                .Set(a => a.SslDaysLeft, site.SslDaysLeft)
                .Set(a => a.LastStatus, site.LastStatus)
                .Set(a => a.ConsecutiveFailures, site.ConsecutiveFailures)
                .Where(a => a.Id == site.Id)
                .ExecuteAffrowsAsync(stoppingToken);

            _logger.LogInformation(
                "站点检测完成：{SiteName} {Url} ping={PingStatus} http={HttpStatus} ssl={SslStatus} latency={LatencyMs}ms",
                site.Name,
                site.Url,
                result.PingStatus,
                result.HttpStatus,
                result.SslStatus,
                result.LatencyMs);
        }

        private static async Task<MonitorSiteCheckResult> CheckSiteAsync(MonitorSite site, CancellationToken stoppingToken)
        {
            var uri = new Uri(site.Url);
            var pingStatus = "Unknown";
            var httpStatus = "Down";
            var sslStatus = uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                ? "Unknown"
                : "NotApplicable";
            var httpStatusCode = 0;
            var latencyMs = 0;
            var sslDaysLeft = default(int?);
            var sslHoursLeft = default(double?);
            string errorMessage = null;
            string sslErrorMessage = null;

            try
            {
                var host = uri.Host;
                var ping = new Ping();
                var pingReply = await ping.SendPingAsync(host, 3000);
                pingStatus = pingReply.Status == IPStatus.Success ? "Up" : "Down";
            }
            catch (Exception ex)
            {
                pingStatus = "Down";
                errorMessage = ex.Message;
            }

            try
            {
                var start = DateTime.UtcNow;
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, stoppingToken);

                latencyMs = (int)Math.Max(0, (DateTime.UtcNow - start).TotalMilliseconds);
                httpStatusCode = (int)response.StatusCode;
                httpStatus = response.IsSuccessStatusCode ? "Up" : "Down";
            }
            catch (Exception ex)
            {
                httpStatus = "Down";
                errorMessage = string.IsNullOrWhiteSpace(errorMessage) ? ex.Message : $"{errorMessage}; {ex.Message}";
                latencyMs = latencyMs <= 0 ? 0 : latencyMs;
            }

            if (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var sslResult = await CheckSslAsync(uri, stoppingToken);
                    sslStatus = sslResult.Status;
                    sslDaysLeft = sslResult.DaysLeft;
                    sslHoursLeft = sslResult.HoursLeft;
                    sslErrorMessage = sslResult.ErrorMessage;
                }
                catch (Exception ex)
                {
                    sslStatus = "Down";
                    sslErrorMessage = ex.Message;
                }
            }

            var isUp = pingStatus == "Up" && httpStatus == "Up" && (sslStatus is "Up" or "NotApplicable");

            return new MonitorSiteCheckResult
            {
                PingStatus = pingStatus,
                HttpStatus = httpStatus,
                HttpStatusCode = httpStatusCode,
                SslStatus = sslStatus,
                LatencyMs = latencyMs,
                SslDaysLeft = sslDaysLeft,
                SslHoursLeft = sslHoursLeft,
                ErrorMessage = errorMessage,
                SslErrorMessage = sslErrorMessage,
                IsUp = isUp
            };
        }

        private static async Task<SslCheckResult> CheckSslAsync(Uri uri, CancellationToken stoppingToken)
        {
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 443;

            using var tcpClient = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            await tcpClient.ConnectAsync(host, port, timeoutCts.Token);

            using var sslStream = new SslStream(tcpClient.GetStream(), false, (_, _, _, _) => true);
            await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = host,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, timeoutCts.Token);

            var certificate = sslStream.RemoteCertificate is null
                ? null
                : new X509Certificate2(sslStream.RemoteCertificate);

            if (certificate is null)
            {
                return new SslCheckResult
                {
                    Status = "Down",
                    ErrorMessage = "未获取到SSL证书"
                };
            }

            var now = DateTime.Now;
            var remain = certificate.NotAfter - now;

            return new SslCheckResult
            {
                Status = "Up",
                DaysLeft = (int)Math.Floor(remain.TotalDays),
                HoursLeft = remain.TotalHours
            };
        }

        private sealed class MonitorSiteCheckResult
        {
            public string PingStatus { get; set; }
            public string HttpStatus { get; set; }
            public int HttpStatusCode { get; set; }
            public string SslStatus { get; set; }
            public int LatencyMs { get; set; }
            public int? SslDaysLeft { get; set; }
            public double? SslHoursLeft { get; set; }
            public string ErrorMessage { get; set; }
            public string SslErrorMessage { get; set; }
            public bool IsUp { get; set; }
        }

        private sealed class SslCheckResult
        {
            public string Status { get; set; }
            public int? DaysLeft { get; set; }
            public double? HoursLeft { get; set; }
            public string ErrorMessage { get; set; }
        }
    }
}