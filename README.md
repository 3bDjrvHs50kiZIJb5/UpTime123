# Uptime123

一个基于 **.NET 8 + Blazor Server + NovaAdmin.Blazor** 的站点监控与后台管理项目。

## 功能

- 站点监控管理
- 定时轮询检测
- HTTP / Ping / SSL 状态检查
- Telegram 告警通知
- SQLite + FreeSql 数据存储

## 技术栈

- ASP.NET Core / Blazor Server
- NovaAdmin.Blazor
- FreeSql
- SQLite
- Telegram Bot API

## 运行

```bash
dotnet run
```

开发环境配置放在 `appsettings.Development.json`。

## 备注

- 监控数据表为 `uptime_monitor_site`
- 项目启动后会自动初始化后台相关功能
