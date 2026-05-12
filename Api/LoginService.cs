using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using BootstrapBlazor.Components;
using FreeScheduler;
using FreeSql;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Uptime123.API;

[ApiController]
[Route("api/login")]
[Tags("账号接口")]
public class LoginService : BaseService
{
    private static readonly ConcurrentDictionary<string, int> _limit = new();

    /// <summary>
    /// 初始化服务
    /// </summary>
    public LoginService(
        FreeSqlCloud freeSqlCloud,
        Scheduler scheduler,
        ILogger<LoginService> logger,
        AdminContext adminContext,
        IConfiguration configuration,
        IWebHostEnvironment webHostEnvironment)
        : base(freeSqlCloud, scheduler, logger, adminContext, configuration, webHostEnvironment)
    {
    }

    /// <summary>
    /// 注册新账号
    /// </summary>
    [HttpPost($"@{nameof(Register)}")]
    [AllowAnonymous]
    public async Task<ApiResult> Register([FromBody] RegisterRequest request)
    {
        var validationError = ValidateRequest(request);
        if (validationError != null)
        {
            return validationError;
        }

        var orm = CurrentOrm;
        var username = request.Username.Trim();
        if (await orm.Select<SysUser>().AnyAsync(a => a.Username == username))
        {
            return ApiResult.Error.SetMessage("用户名已存在");
        }

        var role = await orm.Select<SysRole>()
            .Where(a => !a.IsAdministrator)
            .OrderBy(a => a.Id)
            .FirstAsync();

        var user = new SysUser
        {
            Username = username,
            Nickname = string.IsNullOrWhiteSpace(request.Nickname) ? username : request.Nickname.Trim(),
            Password = request.Password,
            Description = request.Description?.Trim(),
            IsEnabled = true,
            LoginTime = DateTime.Now,
            Roles = role == null ? new List<SysRole>() : new List<SysRole> { role }
        };

        await FreeSqlAggregateRootRepositoryGlobalExtensions
            .GetAggregateRootRepository<SysUser>(orm)
            .InsertAsync(user);

        await WriteLoginLogAsync(user.Username, SysUserLoginLog.LogType.登陆成功, "API register");

        return ApiResult.Success.SetData(new UserSummaryResponse
        {
            Id = user.Id,
            Username = user.Username,
            Nickname = user.Nickname,
            IsEnabled = user.IsEnabled,
            LoginTime = user.LoginTime,
            Roles = user.Roles?.Select(a => a.Name).ToList() ?? new List<string>()
        }).SetMessage("注册成功");
    }

    /// <summary>
    /// 获取在线用户
    /// </summary>
    [HttpGet($"@{nameof(GetWhoIsUsingList)}")]
    public async Task<ApiResult> GetWhoIsUsingList([FromQuery, Range(1, 24)] int limit = 12)
    {
        var users = await CurrentOrm.Select<SysUser>()
            .Where(a => a.IsEnabled)
            .OrderByDescending(a => a.LoginTime)
            .Limit(limit)
            .ToListAsync();

        var items = users.Select(a => new UserCardResponse
        {
            Id = a.Id,
            Username = a.Username,
            DisplayName = DisplayName(a),
            LastLoginTime = a.LoginTime,
            Description = a.Description ?? string.Empty
        }).ToList();

        return ApiResult.Success.SetData(new UserCardListResponse
        {
            TotalCount = items.Count,
            Items = items
        });
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    [HttpPost($"@{nameof(Login)}")]
    [AllowAnonymous]
    public async Task<ApiResult> Login([FromBody] LoginRequest request)
    {
        var validationError = ValidateRequest(request);
        if (validationError != null)
        {
            return validationError;
        }

        var ip = GetClientIpAddress();
        if (_limit.TryGetValue(ip, out var count) && count >= 5)
        {
            return ApiResult.Error.SetMessage($"{ip} 操作频率过高，请稍后再试...");
        }

        var orm = CurrentOrm;
        var user = await orm.Select<SysUser>()
            .IncludeMany(a => a.Roles)
            .Where(a => a.Username == request.Username)
            .FirstAsync();

        if (user == null || user.Password != request.Password)
        {
            count = _limit.AddOrUpdate(ip, 1, (_, oldValue) => oldValue + 1);
            _scheduler.AddTempTask(TimeSpan.FromSeconds(60), () => _limit.TryRemove(ip, out _));
            await WriteLoginLogAsync(request.Username, SysUserLoginLog.LogType.登陆失败, $"failed:{count}");
            return ApiResult.Error.SetMessage($"用户名或密码错误，当前限制次数：{count}");
        }

        if (!user.IsEnabled)
        {
            await WriteLoginLogAsync(user.Username, SysUserLoginLog.LogType.登陆失败, "disabled");
            return ApiResult.Error.SetMessage("账户已被禁用");
        }

        user.LoginTime = DateTime.Now;
        await orm.Update<SysUser>()
            .Where(a => a.Id == user.Id)
            .Set(a => a.LoginTime, user.LoginTime)
            .ExecuteAffrowsAsync();

        await WriteLoginLogAsync(user.Username, SysUserLoginLog.LogType.登陆成功);

        return ApiResult.Success.SetData(new LoginResponse
        {
            Token = BuildToken(user),
            User = new UserSummaryResponse
            {
                Id = user.Id,
                Username = user.Username,
                Nickname = user.Nickname,
                IsEnabled = user.IsEnabled,
                LoginTime = user.LoginTime,
                Roles = user.Roles?.Select(a => a.Name).ToList() ?? new List<string>()
            }
        }).SetMessage("登录成功");
    }

    /// <summary>
    /// 退出登录
    /// </summary>
    [HttpGet($"@{nameof(Logout)}")]
    public Task<ApiResult> Logout()
    {
        HttpContext.Response.Cookies.Delete(_adminContext.CookieName);
        return Task.FromResult(ApiResult.Success.SetData(true).SetMessage("已退出登录"));
    }

    /// <summary>
    /// 校验登录态
    /// </summary>
    [HttpGet($"@{nameof(Check)}")]
    public async Task<ApiResult> Check([FromQuery] string? token)
    {
        var user = await GetCurrentUserAsync(token);
        if (user == null)
        {
            return ApiResult.Error.SetCode(401).SetMessage("未登录或登录已过期");
        }

        return ApiResult.Success.SetData(new UserDetailResponse
        {
            Id = user.Id,
            Username = user.Username,
            Nickname = user.Nickname,
            IsEnabled = user.IsEnabled,
            LoginTime = user.LoginTime,
            Description = user.Description ?? string.Empty,
            OrgId = user.OrgId,
            Roles = user.Roles?.Select(a => a.Name).ToList() ?? new List<string>()
        });
    }

    /// <summary>
    /// 更新用户信息
    /// </summary>
    [HttpPost($"@{nameof(UpdateMemberInfo)}")]
    public async Task<ApiResult> UpdateMemberInfo([FromBody] UpdateMemberInfoRequest request, [FromQuery] string? token = null)
    {
        var user = await GetCurrentUserAsync(token);
        if (user == null)
        {
            return ApiResult.Error.SetMessage("未登录或登录已过期");
        }

        user.Nickname = request.Nickname?.Trim() ?? user.Nickname;
        user.Description = request.Description?.Trim() ?? user.Description;

        await CurrentOrm.Update<SysUser>()
            .Where(a => a.Id == user.Id)
            .Set(a => a.Nickname, user.Nickname)
            .Set(a => a.Description, user.Description)
            .ExecuteAffrowsAsync();

        return ApiResult.Success.SetMessage("用户信息更新成功");
    }

    /// <summary>
    /// 修改登录密码
    /// </summary>
    [HttpPost($"@{nameof(ChangePassword)}")]
    public async Task<ApiResult> ChangePassword([FromBody] ChangePasswordRequest request, [FromQuery] string? token = null)
    {
        var validationError = ValidateRequest(request);
        if (validationError != null)
        {
            return validationError;
        }

        var user = await GetCurrentUserAsync(token);
        if (user == null)
        {
            return ApiResult.Error.SetMessage("未登录或登录已过期");
        }

        if (!string.Equals(user.Password, request.OldPassword, StringComparison.Ordinal))
        {
            return ApiResult.Error.SetMessage("旧密码错误，请重新输入");
        }

        if (string.Equals(request.OldPassword, request.NewPassword, StringComparison.Ordinal))
        {
            return ApiResult.Error.SetMessage("新密码不能与旧密码相同");
        }

        await CurrentOrm.Update<SysUser>()
            .Where(a => a.Id == user.Id)
            .Set(a => a.Password, request.NewPassword)
            .Set(a => a.LoginTime, DateTime.Now)
            .ExecuteAffrowsAsync();

        return ApiResult.Success.SetMessage("密码修改成功");
    }

    /// <summary>
    /// 注销当前账户
    /// </summary>
    [HttpPost($"@{nameof(DeleteAccount)}")]
    public async Task<ApiResult> DeleteAccount([FromBody] DeleteAccountRequest request, [FromQuery] string? token = null)
    {
        var validationError = ValidateRequest(request);
        if (validationError != null)
        {
            return validationError;
        }

        var user = await GetCurrentUserAsync(token);
        if (user == null)
        {
            return ApiResult.Error.SetMessage("未登录或登录已过期");
        }

        if (!string.Equals(user.Password, request.Password, StringComparison.Ordinal))
        {
            return ApiResult.Error.SetMessage("密码错误，无法注销账户");
        }

        if (user.IsSystem)
        {
            return ApiResult.Error.SetMessage("系统用户不允许注销账户");
        }

        await CurrentOrm.Update<SysUser>()
            .Where(a => a.Id == user.Id)
            .Set(a => a.IsEnabled, false)
            .ExecuteAffrowsAsync();

        await WriteLoginLogAsync(user.Username, SysUserLoginLog.LogType.登陆失败, "account-disabled");
        return ApiResult.Success.SetMessage("账户已停用");
    }

    /// <summary>
    /// 上传用户头像
    /// </summary>
    [HttpPost($"@{nameof(UploadAvatar)}")]
    public Task<ApiResult> UploadAvatar([FromBody] UploadAvatarRequest request, [FromQuery] string? token = null) =>
        Task.FromResult(ApiResult.Error.SetCode(501).SetMessage("当前项目的 SysUser 未提供头像字段，接口未启用"));

    /// <summary>
    /// 上传胸卡照片
    /// </summary>
    [HttpPost($"@{nameof(UploadBadgePhoto)}")]
    public Task<ApiResult> UploadBadgePhoto([FromBody] UploadBadgePhotoRequest request, [FromQuery] string? token = null) =>
        Task.FromResult(ApiResult.Error.SetCode(501).SetMessage("当前项目未提供胸卡照片字段，接口未启用"));

    /// <summary>
    /// 发送重置码
    /// </summary>
    [HttpPost($"@{nameof(SendResetPasswordCode)}")]
    [AllowAnonymous]
    public Task<ApiResult> SendResetPasswordCode([FromBody] SendResetPasswordCodeRequest request) =>
        Task.FromResult(ApiResult.Error.SetCode(501).SetMessage("当前项目未集成短信验证码能力"));

    /// <summary>
    /// 重置登录密码
    /// </summary>
    [HttpPost($"@{nameof(ResetPassword)}")]
    [AllowAnonymous]
    public Task<ApiResult> ResetPassword([FromBody] ResetPasswordRequest request) =>
        Task.FromResult(ApiResult.Error.SetCode(501).SetMessage("当前项目未启用短信找回密码流程"));

    /// <summary>
    /// 设置报警等级
    /// </summary>
    [HttpPost($"@{nameof(SetAIAlarmLevel)}")]
    public Task<ApiResult> SetAIAlarmLevel([FromBody] SetAIAlarmLevelRequest request, [FromQuery] string? token = null) =>
        Task.FromResult(ApiResult.Error.SetCode(501).SetMessage("当前项目没有 AI 报警等级业务"));

    /// <summary>
    /// 校验请求参数
    /// </summary>
    private static ApiResult? ValidateRequest(object request)
    {
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        if (Validator.TryValidateObject(request, validationContext, validationResults, true))
        {
            return null;
        }

        return ApiResult.Error.SetMessage(string.Join(", ", validationResults.Select(a => a.ErrorMessage)));
    }
}

public sealed class RegisterRequest
{
    [Required]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Nickname { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }
}

public sealed class LoginRequest
{
    [Required]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Password { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [Required]
    [StringLength(50)]
    public string OldPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class UpdateMemberInfoRequest
{
    [StringLength(50)]
    public string? Nickname { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }
}

public sealed class DeleteAccountRequest
{
    [Required]
    [StringLength(50)]
    public string Password { get; set; } = string.Empty;
}

public sealed class UploadAvatarRequest
{
    public string? Base64 { get; set; }
}

public sealed class UploadBadgePhotoRequest
{
    public string? Base64 { get; set; }
}

public sealed class SendResetPasswordCodeRequest
{
    public string? Phone { get; set; }
}

public sealed class ResetPasswordRequest
{
    public string? Phone { get; set; }
    public string? Code { get; set; }
    public string? NewPassword { get; set; }
}

public sealed class SetAIAlarmLevelRequest
{
    public int Level { get; set; }
}

public sealed class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public UserSummaryResponse User { get; set; } = new();
}

public class UserSummaryResponse
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime LoginTime { get; set; }
    public List<string> Roles { get; set; } = new();
}

public sealed class UserDetailResponse : UserSummaryResponse
{
    public long OrgId { get; set; }
    public string Description { get; set; } = string.Empty;
}

public sealed class UserCardResponse
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime LastLoginTime { get; set; }
}

public sealed class UserCardListResponse
{
    public int TotalCount { get; set; }
    public List<UserCardResponse> Items { get; set; } = new();
}
