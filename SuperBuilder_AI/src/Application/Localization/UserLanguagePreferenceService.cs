using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Interfaces.Localization;
using SuperBuilder_AI.Models.Localization;

namespace SuperBuilder_AI.Services.Localization;

/// <summary>
/// 用户级界面语言偏好服务（M3-G0「用户语言恢复」）。
/// <para>读取/写入 <see cref="UserLanguagePreference"/>，写入时强制校验目标文化属于租户可用语言范围，越界则回退租户默认并写审计。</para>
/// </summary>
public sealed class UserLanguagePreferenceService : IUserLanguagePreferenceService
{
    private readonly SuperBIContext _db;
    private readonly IAuditLogService _audit;

    public UserLanguagePreferenceService(SuperBIContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<string?> GetAsync(long tenantId, long userId, CancellationToken ct = default)
    {
        var row = await _db.UserLanguagePreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId, ct);
        return row?.Culture;
    }

    public async Task<string> SetAsync(long tenantId, long userId, string culture, CancellationToken ct = default)
    {
        var available = await GetAvailableCulturesAsync(tenantId, ct);
        var requested = (culture ?? string.Empty).Trim();
        var effective = available.Contains(requested, StringComparer.OrdinalIgnoreCase)
            ? requested
            : available[0];

        var row = await _db.UserLanguagePreferences
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId, ct);
        if (row is null)
        {
            _db.UserLanguagePreferences.Add(new UserLanguagePreference
            {
                TenantId = tenantId,
                UserId = userId,
                Culture = effective,
                CreatedBy = userId.ToString(),
            });
        }
        else
        {
            row.Culture = effective;
            row.UpdatedBy = userId.ToString();
            row.UpdatedTime = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditLogEntry(
            tenantId, "user.language.set", "UserLanguagePreference",
            UserId: userId, Actor: "system", EntityId: userId.ToString(),
            Result: "success",
            Message: $"用户语言偏好已设为 {effective}（请求值 {requested}）。"));

        return effective;
    }

    private async Task<List<string>> GetAvailableCulturesAsync(long tenantId, CancellationToken ct)
    {
        var settings = await _db.TenantSettings.AsNoTracking()
            .Where(s => s.TenantId == tenantId && (s.Key == "localization:availableCultures" || s.Key == "localization:defaultCulture"))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        List<string> available;
        try
        {
            available = JsonSerializer.Deserialize<List<string>>(settings.GetValueOrDefault("localization:availableCultures") ?? "[]") ?? new();
        }
        catch (JsonException)
        {
            available = new();
        }
        available = available.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (available.Count == 0) available.Add("zh-CN");
        return available;
    }
}
