using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Npgsql;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Infrastructure.Security;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 当前用户可访问的数据源枚举（供前端 Ask 等场景的数据源下拉选择使用）。
///
/// <para>
/// 仅返回该用户经显式授权（用户/角色授权）且已启用、属于本租户的数据源摘要
/// （<see cref="DataSourceSummaryDto"/>：id + 名称 + 类型）。无授权时返回空数组（HTTP 200），
/// 避免前端在尚未配置授权时误判为错误而闪烁。
/// </para>
///
/// <para>
/// 只读 GET 端点，不触碰 Golden 依赖文件，不影响 Golden 18/18 行为契约。租户隔离由令牌
/// <c>tid</c> 声明驱动；查询复用与 <see cref="DataSourceAuthorizationService"/> 相同的授权判定逻辑。
/// </para>
/// </summary>
[ApiController]
[Route("api/data-sources")]
public sealed class DataSourcesController : ControllerBase
{
	private readonly SuperBIContext _db;
	private readonly IIdentityService _identity;
	private readonly ISecretStore _secrets;

	public DataSourcesController(SuperBIContext db, IIdentityService identity, ISecretStore secrets)
	{
		_db = db;
		_identity = identity;
		_secrets = secrets;
	}

	/// <summary>列出当前用户已授权且启用的数据源（摘要）。</summary>
	[HttpGet]
	public async Task<IActionResult> List(CancellationToken cancellationToken)
	{
		var tenantId = ResolveTenantId();
		var userId = ResolveUserId();
		if (tenantId <= 0 || userId <= 0)
			return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" });

		if (!await _identity.HasPermissionAsync(tenantId, userId, IdentityPermissions.DashboardView, cancellationToken))
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 dashboard:view 权限。" });

		var roleIds = await _db.UserRoles.AsNoTracking()
			.Where(x => x.TenantId == tenantId && x.UserId == userId)
			.Select(x => x.RoleId).ToListAsync(cancellationToken);
		var canManageMetadata = await _identity.HasPermissionAsync(tenantId, userId, IdentityPermissions.MetadataEdit, cancellationToken);
		if (canManageMetadata)
		{
			var managed = await _db.DataSources.AsNoTracking().Where(x => x.TenantId == tenantId && x.Enabled == true)
				.OrderBy(x => x.Id).Select(x => new DataSourceSummaryDto(x.Id, x.Name ?? ("数据源 " + x.Id), x.DbType ?? "")).ToListAsync(cancellationToken);
			return Ok(managed);
		}

		var raw = await (
			from grant in _db.DataSourceAccessGrants.AsNoTracking()
			join source in _db.DataSources.AsNoTracking() on grant.DataSourceId equals source.Id
			where grant.TenantId == tenantId && source.TenantId == tenantId && source.Enabled == true &&
				  ((grant.SubjectType == DataSourceGrantSubjectType.User && grant.SubjectId == userId) ||
				   (grant.SubjectType == DataSourceGrantSubjectType.Role && roleIds.Contains(grant.SubjectId)))
			select new { source.Id, Name = source.Name, DbType = source.DbType }
		).Distinct().OrderBy(x => x.Id).ToListAsync(cancellationToken);

		var summaries = raw
			.Select(x => new DataSourceSummaryDto(x.Id, x.Name ?? ("数据源 " + x.Id), x.DbType ?? ""))
			.ToList();
		return Ok(summaries);
	}

	/// <summary>租户管理视图：列出本租户全部数据源及已扫描的元数据数量。</summary>
	[HttpGet("manage")]
	public async Task<IActionResult> Manage(CancellationToken cancellationToken)
	{
		var tenantId = ResolveTenantId();
		var userId = ResolveUserId();
		if (tenantId <= 0 || userId <= 0)
			return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" });
		if (!await _identity.HasPermissionAsync(tenantId, userId, IdentityPermissions.MetadataView, cancellationToken))
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 metadata:view 权限。" });

		var rows = await _db.DataSources.AsNoTracking()
			.Where(ds => ds.TenantId == tenantId)
			.OrderBy(ds => ds.Id)
			.Select(ds => new
			{
				ds.Id,
				ds.Name,
				ds.DbType,
				ds.Enabled,
				ds.LastTestStatus,
				ds.LastScanAt,
				TableCount = _db.MetadataTables.Count(t => t.TenantId == tenantId && t.DataSourceId == ds.Id),
				ColumnCount = _db.MetadataColumns.Count(c => c.MetadataTable != null &&
					c.MetadataTable.TenantId == tenantId && c.MetadataTable.DataSourceId == ds.Id),
			})
			.ToListAsync(cancellationToken);
		return Ok(rows);
	}

	/// <summary>读取一个本租户数据源的元数据表与字段。</summary>
	[HttpGet("{id:long}/metadata")]
	public async Task<IActionResult> Metadata(long id, CancellationToken cancellationToken)
	{
		var tenantId = ResolveTenantId();
		var userId = ResolveUserId();
		if (tenantId <= 0 || userId <= 0)
			return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" });
		if (!await _identity.HasPermissionAsync(tenantId, userId, IdentityPermissions.MetadataView, cancellationToken))
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 metadata:view 权限。" });
		if (!await _db.DataSources.AsNoTracking().AnyAsync(ds => ds.Id == id && ds.TenantId == tenantId, cancellationToken))
			return NotFound(new ApiError { Code = ErrorCodes.BadRequest, Message = "数据源不存在或不属于当前租户。" });

		var tables = await _db.MetadataTables.AsNoTracking()
			.Where(t => t.TenantId == tenantId && t.DataSourceId == id)
			.OrderBy(t => t.TableName)
			.Select(t => new
			{
				t.Id,
				t.TableName,
				t.TableComment,
				t.BusinessDomain,
				t.EmbeddingModel,
				t.VectorDimension,
				Columns = t.Columns.OrderBy(c => c.Id).Select(c => new
				{
					c.Id,
					c.ColumnName,
					c.ColumnComment,
					c.DataType,
					c.Length,
					c.IsNullable,
					c.IsPrimaryKey,
					c.BusinessKey,
					c.VectorId,
					Semantic = c.Semantic == null ? null : new
					{
						c.Semantic.Id,
						c.Semantic.MetadataColumnId,
						c.Semantic.BusinessMeaning,
						c.Semantic.BusinessDomain,
						c.Semantic.Confidence,
						c.Semantic.Source,
						c.Semantic.VectorId,
						c.Semantic.EmbeddingModel,
						c.Semantic.VectorDimension,
					},
				}).ToList(),
			})
			.ToListAsync(cancellationToken);
		return Ok(tables);
	}

	/// <summary>元数据关系详情：列、语义及向量点均返回可核验的上下游数据。</summary>
	[HttpGet("metadata/entities/{type}/{entityId}")]
	public async Task<IActionResult> MetadataEntity(string type, string entityId, CancellationToken cancellationToken)
	{
		var tenantId = ResolveTenantId(); var userId = ResolveUserId();
		if (tenantId <= 0 || userId <= 0) return Unauthorized();
		if (!await _identity.HasPermissionAsync(tenantId, userId, IdentityPermissions.MetadataView, cancellationToken)) return Forbid();
		if (type.Equals("column", StringComparison.OrdinalIgnoreCase) && long.TryParse(entityId, out var columnId))
		{
			var row = await _db.MetadataColumns.AsNoTracking().Where(c => c.Id == columnId && c.MetadataTable != null && c.MetadataTable.TenantId == tenantId)
				.Select(c => new { EntityType="MetadataColumn", c.Id, c.ColumnName, c.ColumnComment, c.DataType, c.Length, c.IsNullable, c.IsPrimaryKey, c.BusinessKey, c.SearchText, c.VectorId,
					Table = new { c.MetadataTableId, c.MetadataTable!.TableName, c.MetadataTable.DataSourceId },
					Semantic = c.Semantic == null ? null : new { c.Semantic.Id, c.Semantic.BusinessMeaning, c.Semantic.VectorId } }).FirstOrDefaultAsync(cancellationToken);
			return row is null ? NotFound() : Ok(row);
		}
		if (type.Equals("semantic", StringComparison.OrdinalIgnoreCase) && long.TryParse(entityId, out var semanticId))
		{
			var row = await _db.MetadataSemantics.AsNoTracking().Where(s => s.Id == semanticId && s.MetadataColumn != null && s.MetadataColumn.MetadataTable != null && s.MetadataColumn.MetadataTable.TenantId == tenantId)
				.Select(s => new { EntityType="MetadataSemantic", s.Id, s.MetadataColumnId, s.BusinessMeaning, s.Keywords, s.Synonyms, s.ExampleQuestions, s.BusinessDomain, s.Confidence, s.Source, s.SearchText, s.VectorId, s.EmbeddingModel, s.VectorDimension,
					Column = new { s.MetadataColumn!.Id, s.MetadataColumn.ColumnName, s.MetadataColumn.MetadataTableId }, Table = new { s.MetadataColumn!.MetadataTable!.Id, s.MetadataColumn.MetadataTable.TableName } }).FirstOrDefaultAsync(cancellationToken);
			return row is null ? NotFound() : Ok(row);
		}
		if (type.Equals("vector", StringComparison.OrdinalIgnoreCase))
		{
			var semantic = await _db.MetadataSemantics.AsNoTracking().Where(s => s.VectorId == entityId && s.MetadataColumn != null && s.MetadataColumn.MetadataTable != null && s.MetadataColumn.MetadataTable.TenantId == tenantId)
				.Select(s => new { EntityType="VectorPoint", VectorId=s.VectorId, OwnerType="MetadataSemantic", OwnerId=s.Id, s.EmbeddingModel, s.VectorDimension, s.SearchText, ColumnId=s.MetadataColumnId, TableId=s.MetadataColumn!.MetadataTableId }).FirstOrDefaultAsync(cancellationToken);
			if (semantic is not null) return Ok(semantic);
			var column = await _db.MetadataColumns.AsNoTracking().Where(c => c.VectorId == entityId && c.MetadataTable != null && c.MetadataTable.TenantId == tenantId)
				.Select(c => new { EntityType="VectorPoint", VectorId=c.VectorId, OwnerType="MetadataColumn", OwnerId=c.Id, EmbeddingModel=c.MetadataTable!.EmbeddingModel, VectorDimension=c.MetadataTable.VectorDimension, c.SearchText, ColumnId=(long?)c.Id, TableId=c.MetadataTableId }).FirstOrDefaultAsync(cancellationToken);
			return column is null ? NotFound() : Ok(column);
		}
		return BadRequest("未知元数据实体类型或标识。");
	}

	/// <summary>租户管理员创建数据源，并自动向创建者写入显式访问授权。</summary>
	[HttpPost]
	public async Task<IActionResult> Create([FromBody] CreateDataSourceRequest request, CancellationToken cancellationToken)
	{
		var tenantId = ResolveTenantId();
		var userId = ResolveUserId();
		if (tenantId <= 0 || userId <= 0)
			return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" });
		if (!await _identity.HasPermissionAsync(tenantId, userId, IdentityPermissions.MetadataEdit, cancellationToken))
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 metadata:edit 权限。" });

		var rawName = (request.Name ?? string.Empty).Trim();
		var dbType = (request.DbType ?? string.Empty).Trim().ToUpperInvariant();
		var connectionString = (request.ConnectionString ?? string.Empty).Trim();
		var normalizedName = DataSource.NormalizeName(rawName);
		if (rawName.Length == 0 || dbType.Length == 0 || connectionString.Length == 0)
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "名称、数据库类型和连接字符串均为必填项。" });
		if (rawName.Length > 128)
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "名称长度不能超过 128 个字符。" });
		if (connectionString.Length > 2048)
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "连接字符串长度不能超过 2048 个字符。" });
		if (!DataSource.IsSupportedDbType(dbType))
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = $"不支持的数据库类型: {request.DbType}。支持: {string.Join("/", DataSource.SupportedDbTypes)}。" });
		// 租户内名称唯一（规范化后比对；写入路径强制，DB 层因测试种子保持可空兼容）。
		if (await _db.DataSources.AsNoTracking().AnyAsync(x => x.TenantId == tenantId && x.NormalizedName == normalizedName, cancellationToken))
			return Conflict(new ApiError { Code = ErrorCodes.BadRequest, Message = $"本租户内已存在同名数据源: {rawName}" });

		await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
		var source = new DataSource
		{
			TenantId = tenantId,
			Name = rawName,
			NormalizedName = normalizedName,
			DbType = dbType,
			ConnectionString = _secrets.Protect(connectionString),
			Enabled = true,
		};
		_db.DataSources.Add(source);
		await _db.SaveChangesAsync(cancellationToken);
		_db.DataSourceAccessGrants.Add(new DataSourceAccessGrant
		{
			TenantId = tenantId,
			DataSourceId = source.Id,
			SubjectType = DataSourceGrantSubjectType.User,
			SubjectId = userId,
		});
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return Ok(new DataSourceSummaryDto(source.Id, source.Name, source.DbType));
	}

	/// <summary>租户管理员编辑数据源：名称必填，类型白名单校验，连接串仅在提供时重置（绝不回显原值）。</summary>
	[HttpPut("{id:long}")]
	public async Task<IActionResult> Update(long id, [FromBody] UpdateDataSourceRequest request, CancellationToken cancellationToken)
	{
		var tenantId = ResolveTenantId();
		var userId = ResolveUserId();
		if (tenantId <= 0 || userId <= 0)
			return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" });
		if (!await _identity.HasPermissionAsync(tenantId, userId, IdentityPermissions.MetadataEdit, cancellationToken))
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 metadata:edit 权限。" });

		var source = await _db.DataSources.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);
		if (source is null)
			return NotFound(new ApiError { Code = ErrorCodes.BadRequest, Message = "数据源不存在或不属于当前租户。" });

		var rawName = (request.Name ?? string.Empty).Trim();
		if (rawName.Length == 0)
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "名称不能为空。" });
		if (rawName.Length > 128)
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "名称长度不能超过 128 个字符。" });
		source.NormalizedName = DataSource.NormalizeName(rawName);
		source.Name = rawName;

		if (!string.IsNullOrWhiteSpace(request.DbType))
		{
			var dbType = request.DbType.Trim().ToUpperInvariant();
			if (!DataSource.IsSupportedDbType(dbType))
				return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = $"不支持的数据库类型: {request.DbType}。支持: {string.Join("/", DataSource.SupportedDbTypes)}。" });
			source.DbType = dbType;
		}

		if (!string.IsNullOrWhiteSpace(request.ConnectionString))
		{
			var connectionString = request.ConnectionString.Trim();
			if (connectionString.Length > 2048)
				return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "连接字符串长度不能超过 2048 个字符。" });
			source.ConnectionString = _secrets.Protect(connectionString);
		}

		await _db.SaveChangesAsync(cancellationToken);
		return Ok(new DataSourceSummaryDto(source.Id, source.Name, source.DbType));
	}

	/// <summary>启用数据源。</summary>
	[HttpPatch("{id:long}/enable")]
	public async Task<IActionResult> Enable(long id, CancellationToken cancellationToken) => await SetEnabledAsync(id, true, cancellationToken);

	/// <summary>停用数据源（停用后不参与 Ask 选择与扫描）。</summary>
	[HttpPatch("{id:long}/disable")]
	public async Task<IActionResult> Disable(long id, CancellationToken cancellationToken) => await SetEnabledAsync(id, false, cancellationToken);

	private async Task<IActionResult> SetEnabledAsync(long id, bool enabled, CancellationToken cancellationToken)
	{
		var tenantId = ResolveTenantId();
		var userId = ResolveUserId();
		if (tenantId <= 0 || userId <= 0)
			return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" });
		if (!await _identity.HasPermissionAsync(tenantId, userId, IdentityPermissions.MetadataEdit, cancellationToken))
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 metadata:edit 权限。" });

		var source = await _db.DataSources.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);
		if (source is null)
			return NotFound(new ApiError { Code = ErrorCodes.BadRequest, Message = "数据源不存在或不属于当前租户。" });
		source.Enabled = enabled;
		await _db.SaveChangesAsync(cancellationToken);
		return Ok(new DataSourceSummaryDto(source.Id, source.Name, source.DbType));
	}

	/// <summary>
	/// 测试数据源连通性：以存储的连接串打开连接并执行 <c>SELECT 1</c>，
	/// 结果（Ok/Failed + 脱敏错误码）写回 <see cref="DataSource.LastTestStatus"/> 等字段。
	/// 连接串仅用于本次探测，绝不返回。
	/// </summary>
	[HttpPost("{id:long}/test-connection")]
	public async Task<IActionResult> TestConnection(long id, CancellationToken cancellationToken)
	{
		var tenantId = ResolveTenantId();
		var userId = ResolveUserId();
		if (tenantId <= 0 || userId <= 0)
			return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" });
		if (!await _identity.HasPermissionAsync(tenantId, userId, IdentityPermissions.MetadataEdit, cancellationToken))
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 metadata:edit 权限。" });

		var source = await _db.DataSources.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);
		if (source is null)
			return NotFound(new ApiError { Code = ErrorCodes.BadRequest, Message = "数据源不存在或不属于当前租户。" });
		if (string.IsNullOrWhiteSpace(source.ConnectionString))
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "连接字符串为空，无法测试。" });

		var sw = Stopwatch.StartNew();
		string? errorCode = null;
		try
		{
			using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
			using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
			var plainConnectionString = _secrets.ResolvePlaintext(source.ConnectionString);
			if (string.IsNullOrWhiteSpace(plainConnectionString))
			{
				await RecordLastTestAsync(id, "Failed", "EmptyConnectionString", cancellationToken);
				return Ok(new { status = "Failed", elapsedMs = sw.ElapsedMilliseconds, errorCode = "EmptyConnectionString" });
			}
			await using var connection = CreateConnection(new DataSource { DbType = source.DbType, ConnectionString = plainConnectionString });
			await connection.OpenAsync(linked.Token);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT 1";
			command.CommandTimeout = 15;
			await command.ExecuteScalarAsync(linked.Token);
			await RecordLastTestAsync(id, "Ok", null, cancellationToken);
			return Ok(new { status = "Ok", elapsedMs = sw.ElapsedMilliseconds, errorCode = (string?)null });
		}
		catch (Exception ex)
		{
			errorCode = ex is OperationCanceledException or TimeoutException ? "Timeout" : ex.GetType().Name;
			await RecordLastTestAsync(id, "Failed", errorCode, cancellationToken);
			return Ok(new { status = "Failed", elapsedMs = sw.ElapsedMilliseconds, errorCode });
		}
	}

	/// <summary>保存前测试连接；不创建数据源或授权，不返回连接串或驱动异常消息。</summary>
	[HttpPost("test-connection")]
	public async Task<IActionResult> TestConnectionString([FromBody] TestDataSourceConnectionRequest request, CancellationToken cancellationToken)
	{
		var tenantId = ResolveTenantId();
		var userId = ResolveUserId();
		if (tenantId <= 0 || userId <= 0)
			return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" });
		if (!await _identity.HasPermissionAsync(tenantId, userId, IdentityPermissions.MetadataEdit, cancellationToken))
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 metadata:edit 权限。" });

		var dbType = (request.DbType ?? string.Empty).Trim().ToUpperInvariant();
		var connectionString = (request.ConnectionString ?? string.Empty).Trim();
		if (!DataSource.IsSupportedDbType(dbType) || connectionString.Length == 0 || connectionString.Length > 2048)
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "请提供支持的数据库类型及 1–2048 字符的连接字符串。" });

		var sw = Stopwatch.StartNew();
		try
		{
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(TimeSpan.FromSeconds(15));
			await using var connection = CreateConnection(new DataSource { DbType = dbType, ConnectionString = connectionString });
			await connection.OpenAsync(timeout.Token);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT 1";
			command.CommandTimeout = 15;
			await command.ExecuteScalarAsync(timeout.Token);
			return Ok(new { status = "Ok", elapsedMs = sw.ElapsedMilliseconds, errorCode = (string?)null });
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
		catch (Exception ex)
		{
			var errorCode = ex is OperationCanceledException or TimeoutException ? "Timeout" : ex.GetType().Name;
			return Ok(new { status = "Failed", elapsedMs = sw.ElapsedMilliseconds, errorCode });
		}
	}

	private static DbConnection CreateConnection(DataSource source)
	{
		return source.DbType?.ToUpperInvariant() switch
		{
			"SQLSERVER" => new SqlConnection(source.ConnectionString),
			"MYSQL" => new MySqlConnection(source.ConnectionString),
			"POSTGRESQL" => new NpgsqlConnection(source.ConnectionString),
			_ => throw new NotSupportedException($"不支持数据库类型:{source.DbType}"),
		};
	}

	/// <summary>
	/// 记录最近一次连接测试结果（尽力而为，失败不影响主流程）。仅写入脱敏错误码（异常类型名/超时标记）。
	/// </summary>
	private async Task RecordLastTestAsync(long dataSourceId, string status, string? errorCode, CancellationToken cancellationToken)
	{
		try
		{
			var ds = await _db.DataSources.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == dataSourceId, cancellationToken);
			if (ds is null) return;
			ds.LastTestStatus = status;
			ds.LastTestTime = DateTime.UtcNow;
			ds.LastErrorCode = errorCode;
			await _db.SaveChangesAsync(cancellationToken);
		}
		catch
		{
			// 记录测试结果是辅助能力，不应影响主流程。
		}
	}

	private sealed record DataSourceSummaryDto(long Id, string Name, string DbType);

	private long ResolveTenantId()
	{
		var v = User.FindFirst("tid")?.Value;
		return long.TryParse(v, out var t) ? t : 0;
	}

	private long ResolveUserId()
	{
		var v = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		return long.TryParse(v, out var u) ? u : 0;
	}
}

public sealed record TestDataSourceConnectionRequest(string? DbType, string? ConnectionString);
public sealed record CreateDataSourceRequest(string? Name, string? DbType, string? ConnectionString);

public sealed record UpdateDataSourceRequest(string? Name, string? DbType, string? ConnectionString);
