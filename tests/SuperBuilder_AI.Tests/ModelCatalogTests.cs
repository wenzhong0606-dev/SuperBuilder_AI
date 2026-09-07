using System.Linq;
using SuperBuilder_AI.Application.ModelAccounts;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M7-07 模型目录（静态种子）单测：覆盖主力供应商/模型完整性、展示名、状态。
/// 目录驱动前端下拉，故其契约稳定性需被锁定。
/// </summary>
public class ModelCatalogTests
{
	[Fact]
	public void Catalog_Contains_Expected_Providers_And_Models()
	{
		var catalog = ModelCatalogProvider.GetCatalog().ToList();
		Assert.NotEmpty(catalog);

		Assert.Contains(catalog, c => c.Provider == "Qwen" && c.ModelId == "qwen-plus");
		Assert.Contains(catalog, c => c.Provider == "Qwen" && c.ModelId == "qwen-max");
		Assert.Contains(catalog, c => c.Provider == "OpenAI" && c.ModelId == "gpt-4o");
		Assert.Contains(catalog, c => c.Provider == "Azure" && c.ModelId == "azure-gpt-4o");
		Assert.Contains(catalog, c => c.Provider == "DeepSeek" && c.ModelId == "deepseek-chat");
	}

	[Fact]
	public void Catalog_Entries_Have_DisplayNames_And_Active_Status()
	{
		var catalog = ModelCatalogProvider.GetCatalog();
		Assert.All(catalog, c => Assert.False(string.IsNullOrWhiteSpace(c.DisplayName)));
		Assert.All(catalog, c => Assert.Equal("active", c.Status));
	}

	[Fact]
	public void Catalog_Keys_Align_With_Binding_Contract()
	{
		// 目录 (Provider, ModelId) 必须与 ModelAccount 绑定契约一致，前端下拉值可直接用于 POST。
		var catalog = ModelCatalogProvider.GetCatalog().ToList();
		Assert.All(catalog, c =>
		{
			Assert.False(string.IsNullOrWhiteSpace(c.Provider));
			Assert.False(string.IsNullOrWhiteSpace(c.ModelId));
		});
	}
}
