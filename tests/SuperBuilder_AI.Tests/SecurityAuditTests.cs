using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Middleware;
using SuperBuilder_AI.Models.Audit;
using SuperBuilder_AI.Services.Audit;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class SecurityAuditTests
{
	private sealed class CaptureAudit : IAuditLogService
	{
		public AuditLogEntry? Entry { get; private set; }
		public Task<long> LogAsync(AuditLogEntry entry, CancellationToken ct = default) { Entry = entry; return Task.FromResult(1L); }
		public Task<IReadOnlyList<AuditLog>> QueryAsync(AuditLogQuery query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<AuditLog>>(Array.Empty<AuditLog>());
	}

	private static ClaimsPrincipal Principal(long tenantId, long userId) => new(new ClaimsIdentity(new[]
	{
		new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
		new Claim(ClaimTypes.Name, "auditor"),
		new Claim("tid", tenantId.ToString())
	}, "Bearer"));

	[Fact]
	public async Task QueryPlanException_IsRecordedAsRejectionBeforeExceptionMiddleware()
	{
		var audit = new CaptureAudit();
		var middleware = new AuditMiddleware(_ => throw SuperBuilderException.FromCode(ErrorCodes.QueryPlanSecurityRejected, 403));
		var context = new DefaultHttpContext { User = Principal(7, 9), TraceIdentifier = "trace-security-1" };
		context.Request.Path = "/api/ask";

		var ex = await Assert.ThrowsAsync<SuperBuilderException>(() => middleware.InvokeAsync(context, audit));
		Assert.Equal(ErrorCodes.QueryPlanSecurityRejected, ex.ErrorCode);
		var entry = Assert.IsType<AuditLogEntry>(audit.Entry);
		Assert.Equal("query-plan.rejected", entry.Action);
		Assert.Equal("failure", entry.Result);
		Assert.Equal(7, entry.TenantId);
		Assert.Equal(9, entry.UserId);
		using var json = JsonDocument.Parse(entry.AfterJson!);
		Assert.Equal(403, json.RootElement.GetProperty("statusCode").GetInt32());
		Assert.Equal("trace-security-1", json.RootElement.GetProperty("traceId").GetString());
		Assert.Equal(ErrorCodes.QueryPlanSecurityRejected, json.RootElement.GetProperty("reasonCode").GetString());
	}

	[Fact]
	public async Task LoginFailure_RecordsSafeFactsWithoutRequestSecrets()
	{
		var audit = new CaptureAudit();
		var middleware = new AuditMiddleware(context =>
		{
			SecurityAuditContext.Reject(context, ErrorCodes.AuthInvalidCredential, "invalid-credential", 7, 9);
			context.Response.StatusCode = 401;
			return Task.CompletedTask;
		});
		var context = new DefaultHttpContext { TraceIdentifier = "trace-login" };
		context.Request.Path = "/api/auth/login";
		context.Request.Headers.Authorization = "Bearer secret-token";

		await middleware.InvokeAsync(context, audit);

		var entry = Assert.IsType<AuditLogEntry>(audit.Entry);
		Assert.Equal("login.failed", entry.Action);
		Assert.Equal(7, entry.TenantId);
		Assert.Equal(9, entry.UserId);
		Assert.DoesNotContain("secret-token", entry.AfterJson);
		Assert.DoesNotContain("password", entry.AfterJson, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task AuditStorage_RejectsUpdateAndDelete_AndTenantQueryExcludesPlatform()
	{
		await using var connection = new SqliteConnection("DataSource=:memory:");
		await connection.OpenAsync();
		await using var db = new SuperBIContext(new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options);
		await db.Database.EnsureCreatedAsync();
		var service = new AuditLogService(db);
		await service.LogAsync(new AuditLogEntry(7, "tenant.event", "SecurityEvent"));
		await service.LogAsync(new AuditLogEntry(0, "platform.event", "SecurityEvent"));

		var tenantLogs = await service.QueryAsync(new AuditLogQuery(TenantId: 7));
		Assert.Single(tenantLogs);
		Assert.Equal(7, tenantLogs[0].TenantId);

		var stored = await db.AuditLogs.SingleAsync(x => x.TenantId == 7);
		stored.Message = "tampered";
		await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
		db.Entry(stored).State = EntityState.Unchanged;
		db.AuditLogs.Remove(stored);
		await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
	}
}
