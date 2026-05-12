using FreeSql.DataAnnotations;

namespace LinCms.Entities.Uptime
{
    /// <summary>
    /// 监控站点
    /// </summary>
    [Table(Name = "uptime_monitor_site")]
    public class MonitorSite : EntityAudited
    {
        /// <summary>
        /// 站点名称
        /// </summary>
        [Column(StringLength = 100)]
        public string Name { get; set; }

        /// <summary>
        /// 站点地址
        /// </summary>
        [Column(StringLength = 400)]
        public string Url { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 检查间隔（秒）
        /// </summary>
        public int CheckIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// 连续失败阈值
        /// </summary>
        public int FailureThreshold { get; set; } = 3;

        /// <summary>
        /// 关键字检测内容
        /// </summary>
        [Column(StringLength = 500)]
        public string Keywords { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        [Column(StringLength = 500)]
        public string Remark { get; set; }

        /// <summary>
        /// 排序
        /// </summary>
        public int SortCode { get; set; }

        /// <summary>
        /// 最近检查时间
        /// </summary>
        public DateTime? LastCheckTime { get; set; }

        /// <summary>
        /// 最近响应时间（毫秒）
        /// </summary>
        public int? LastResponseTimeMs { get; set; }

        /// <summary>
        /// 延时状态（毫秒）
        /// </summary>
        public int? LatencyMs { get; set; }

        /// <summary>
        /// 连续失败次数
        /// </summary>
        public int ConsecutiveFailures { get; set; }

        /// <summary>
        /// SSL证书剩余天数
        /// </summary>
        public int? SslDaysLeft { get; set; }

        /// <summary>
        /// Ping状态
        /// </summary>
        [Column(StringLength = 20)]
        public string PingStatus { get; set; }

        /// <summary>
        /// Http状态
        /// </summary>
        [Column(StringLength = 20)]
        public string HttpStatus { get; set; }

        /// <summary>
        /// SSL状态
        /// </summary>
        [Column(StringLength = 20)]
        public string SslStatus { get; set; }

        /// <summary>
        /// 最近状态
        /// </summary>
        [Column(StringLength = 20)]
        public string LastStatus { get; set; }

    }
}
