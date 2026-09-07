using Xunit;

namespace SuperBuilder_AI.E2E;

/// <summary>
/// 共享浏览器生命周期的 Playwright 集合定义。所有 E2E 测试类标注
/// <c>[Collection("playwright")]</c> 后共用同一个 <see cref="PlaywrightFixture"/>，
/// 避免每个测试类重复拉起 Chromium。
/// </summary>
[CollectionDefinition("playwright")]
public sealed class PlaywrightCollection : ICollectionFixture<PlaywrightFixture>
{
}
