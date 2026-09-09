using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.AppBuilder;
using SuperBuilder_AI.Models.AppBuilder;

namespace SuperBuilder_AI.Infrastructure.Persistence;

/// <summary>
/// M7-11：Ask 查询快照的 EF 实现。仅在查询成功且存在已认证访问者时由
/// <see cref="SuperBuilder_AI.Services.BI.BIConversationService"/> 写入。
/// </summary>
public sealed class AskQuerySnapshotStore : IAskQuerySnapshotStore
{
	private readonly SuperBIContext _db;

	public AskQuerySnapshotStore(SuperBIContext db)
	{
		_db = db;
	}

	public async Task<string> SaveAsync(AskQuerySnapshot snapshot, CancellationToken cancellationToken = default)
	{
		_db.AskQuerySnapshots.Add(snapshot);
		await _db.SaveChangesAsync(cancellationToken);
		return snapshot.TurnId;
	}

	public async Task<AskQuerySnapshot?> GetAsync(string turnId, CancellationToken cancellationToken = default)
	{
		var entity = await _db.AskQuerySnapshots
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.TurnId == turnId, cancellationToken);
		if (entity is null) return null;
		// 过期快照视为不存在（防篡改：不返回过期上下文）。
		if (entity.ExpiresAt <= DateTime.UtcNow) return null;
		return entity;
	}
}
