using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Infrastructure.Security;
using SuperBuilder_AI.Models.ModelAccount;

namespace SuperBuilder_AI.Application.ModelAccounts;

/// <summary>模型账号摘要 DTO（掩码，绝不含明文 Key）。</summary>
public sealed record ModelAccountSummary(
	long Id,
	long TenantId,
	string Provider,
	string ModelId,
	string DisplayName,
	string MaskedKey,
	string? Note,
	bool IsDefault);

/// <summary>创建模型账号请求体。</summary>
public sealed record CreateModelAccountRequest(
	long TenantId,
	string Provider,
	string ModelId,
	string ApiKey,
	string? DisplayName = null,
	string? Note = null);

/// <summary>更新模型账号请求体（仅传入需变更的字段）。</summary>
public sealed record UpdateModelAccountRequest(
	string? ApiKey = null,
	string? DisplayName = null,
	string? Note = null);

/// <summary>
/// 模型账号（BYO）领域服务（M7-07）。
///
/// <para>
/// 负责租户级模型密钥的加密落库、掩码展示、默认唯一性与服务端解密。
/// 所有密钥操作经 <see cref="ISecretStore"/>；明文仅在服务端 <see cref="ResolvePlaintextKeyAsync"/> 取出，
/// 供未来 LLM 客户端注入，任何对外 DTO 都只携带 <see cref="ModelAccountSummary.MaskedKey"/>。
/// </para>
/// </summary>
public sealed class ModelAccountService
{
	private readonly SuperBIContext _db;
	private readonly ISecretStore _secrets;

	public ModelAccountService(SuperBIContext db, ISecretStore secrets)
	{
		_db = db;
		_secrets = secrets;
	}

	/// <summary>列出当前租户的全部模型账号（掩码）。</summary>
	public async Task<List<ModelAccountSummary>> ListAsync(long tenantId, CancellationToken ct)
	{
		return await _db.ModelAccounts.AsNoTracking()
			.Where(m => m.TenantId == tenantId)
			.OrderBy(m => m.Provider).ThenBy(m => m.ModelId)
			.Select(m => new ModelAccountSummary(m.Id, m.TenantId, m.Provider, m.ModelId, m.DisplayName, m.MaskedKey, m.Note, m.IsDefault))
			.ToListAsync(ct);
	}

	/// <summary>按 Id 取单个模型账号（掩码）；不存在返回 null。</summary>
	public async Task<ModelAccountSummary?> GetAsync(long tenantId, long id, CancellationToken ct)
	{
		var m = await _db.ModelAccounts.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
		return m is null ? null : ToSummary(m);
	}

	/// <summary>创建绑定：加密 API Key 后落库；首个绑定自动成为租户默认。</summary>
	public async Task<ModelAccountSummary> CreateAsync(CreateModelAccountRequest req, CancellationToken ct)
	{
		if (req.TenantId <= 0)
			throw new SuperBuilderException(ErrorCodes.BadRequest, "租户 Id 必须 > 0。", 400);
		if (string.IsNullOrWhiteSpace(req.Provider))
			throw new SuperBuilderException(ErrorCodes.BadRequest, "供应商标识不能为空。", 400);
		if (string.IsNullOrWhiteSpace(req.ModelId))
			throw new SuperBuilderException(ErrorCodes.BadRequest, "模型标识不能为空。", 400);
		if (string.IsNullOrWhiteSpace(req.ApiKey))
			throw new SuperBuilderException(ErrorCodes.BadRequest, "API Key 不能为空。", 400);

		var duplicate = await _db.ModelAccounts
			.AnyAsync(m => m.TenantId == req.TenantId && m.Provider == req.Provider && m.ModelId == req.ModelId, ct);
		if (duplicate)
			throw new SuperBuilderException(ErrorCodes.Conflict, $"该模型已绑定：{req.Provider}/{req.ModelId}。", 409);

		var isFirst = !await _db.ModelAccounts.AnyAsync(m => m.TenantId == req.TenantId, ct);
		var entity = new ModelAccount
		{
			TenantId = req.TenantId,
			Provider = req.Provider,
			ModelId = req.ModelId,
			DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? $"{req.Provider}/{req.ModelId}" : req.DisplayName,
			EncryptedKey = _secrets.Protect(req.ApiKey),
			MaskedKey = Mask(req.ApiKey),
			Note = req.Note,
			IsDefault = isFirst,
		};
		_db.ModelAccounts.Add(entity);
		await _db.SaveChangesAsync(ct);
		return ToSummary(entity);
	}

	/// <summary>更新绑定（可仅改备注/名称，或连同轮换 API Key）。</summary>
	public async Task<ModelAccountSummary> UpdateAsync(long tenantId, long id, UpdateModelAccountRequest req, CancellationToken ct)
	{
		var m = await _db.ModelAccounts.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
		if (m is null)
			throw new SuperBuilderException(ErrorCodes.NotFound, "模型账号不存在。", 404);
		if (!string.IsNullOrWhiteSpace(req.ApiKey))
		{
			m.EncryptedKey = _secrets.Protect(req.ApiKey);
			m.MaskedKey = Mask(req.ApiKey);
		}
		if (req.DisplayName is not null) m.DisplayName = req.DisplayName;
		if (req.Note is not null) m.Note = req.Note;
		await _db.SaveChangesAsync(ct);
		return ToSummary(m);
	}

	/// <summary>删除绑定。</summary>
	public async Task DeleteAsync(long tenantId, long id, CancellationToken ct)
	{
		var m = await _db.ModelAccounts.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
		if (m is null)
			throw new SuperBuilderException(ErrorCodes.NotFound, "模型账号不存在。", 404);
		_db.ModelAccounts.Remove(m);
		await _db.SaveChangesAsync(ct);
	}

	/// <summary>将指定绑定设为租户默认（同时取消其它默认）。</summary>
	public async Task SetDefaultAsync(long tenantId, long id, CancellationToken ct)
	{
		var m = await _db.ModelAccounts.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
		if (m is null)
			throw new SuperBuilderException(ErrorCodes.NotFound, "模型账号不存在。", 404);
		foreach (var other in _db.ModelAccounts.Where(x => x.TenantId == tenantId && x.IsDefault))
			other.IsDefault = false;
		m.IsDefault = true;
		await _db.SaveChangesAsync(ct);
	}

	/// <summary>
	/// 服务端解析明文 Key（供未来 LLM 客户端注入）；绝不下发客户端。
	/// 找不到绑定返回 null。
	/// </summary>
	public async Task<string?> ResolvePlaintextKeyAsync(long tenantId, string provider, string modelId, CancellationToken ct)
	{
		var m = await _db.ModelAccounts.AsNoTracking()
			.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Provider == provider && x.ModelId == modelId, ct);
		if (m is null) return null;
		return _secrets.Unprotect(m.EncryptedKey);
	}

	/// <summary>密钥掩码：保留前缀 6 位与后缀 4 位，中间以 *** 替代（如 sk-abc***1234）。</summary>
	private static string Mask(string key)
	{
		if (string.IsNullOrWhiteSpace(key)) return "***";
		var head = key.Length <= 6 ? key : key.Substring(0, 6);
		var tail = key.Length <= 4 ? "" : key.Substring(key.Length - 4);
		return $"{head}***{tail}";
	}

	private static ModelAccountSummary ToSummary(ModelAccount m) =>
		new(m.Id, m.TenantId, m.Provider, m.ModelId, m.DisplayName, m.MaskedKey, m.Note, m.IsDefault);
}
