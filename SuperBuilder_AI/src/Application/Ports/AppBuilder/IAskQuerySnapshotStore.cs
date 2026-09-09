using SuperBuilder_AI.Models.AppBuilder;

namespace SuperBuilder_AI.Interfaces.AppBuilder;

/// <summary>
/// M7-11：Ask 查询快照存储端口。
/// 由 BIConversationService 在执行成功后写入；由 from-ask 导出与运行时读取。
/// 作为可选依赖注入：测试/内部兼容路径不注册时，BIConversationService 跳过快照写入（零回归）。
/// </summary>
public interface IAskQuerySnapshotStore
{
	/// <summary>保存快照；turnId 由实现生成并返回。</summary>
	Task<string> SaveAsync(AskQuerySnapshot snapshot, CancellationToken cancellationToken = default);

	/// <summary>按 turnId 取快照（含过期判断）；过期或不存在返回 null。</summary>
	Task<AskQuerySnapshot?> GetAsync(string turnId, CancellationToken cancellationToken = default);
}
