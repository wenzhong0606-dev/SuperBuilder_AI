using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.AppBuilder;
using SuperBuilder_AI.Models.AppBuilder;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M7-11 控制器单测用的轻量桩（不调 LLM / 不触真实取数链路；仅满足构造函数依赖）。
/// 既有 CRUD / 版本隔离测试不会走到 from-ask 与 render 路径，因此桩可为空实现。
/// </summary>
internal sealed class FakeAppQueryBindingExporter : IAppQueryBindingExporter
{
	public Task<AppDataSourceBinding> ExportAsync(
		string turnId, long tenantId, long userId, CancellationToken cancellationToken = default) =>
		Task.FromResult(new AppDataSourceBinding { Entity = "stub" });
}

internal sealed class FakeAppQueryExecutor : IAppQueryExecutor
{
	public Task<AppComponentRender> ExecuteComponentAsync(
		AppDataSourceBinding binding, long tenantId, long userId, CancellationToken cancellationToken = default) =>
		Task.FromResult(new AppComponentRender { Succeeded = true });
}
