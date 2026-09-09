using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SuperBuilder_AI.Application.Common.Configuration;
using Xunit;

namespace SuperBuilder_AI.Tests.Configuration;

/// <summary>
/// 最小 <see cref="IHostEnvironment"/> 替身，仅用于驱动环境判定分支。
/// </summary>
public sealed class FakeHostEnvironment : IHostEnvironment
{
	public string EnvironmentName { get; set; } = Environments.Production;
	public string ApplicationName { get; set; } = "test";
	public string ContentRootPath { get; set; } = ".";
	public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

public sealed class StartupConfigurationValidatorTests
{
	private static IConfiguration Config(Dictionary<string, string?> data) =>
		new ConfigurationBuilder().AddInMemoryCollection(data).Build();

	private static StartupConfigurationValidator Validator => new();

	private static Dictionary<string, string?> ValidBase() => new()
	{
		["ConnectionStrings:DefaultConnection"] = "Server=.;Database=platform",
		["ConnectionStrings:SuperBI"] = "Server=.;Database=platform",
		["Qwen:ApiKey"] = "k",
		["Embedding:ApiKey"] = "k",
		["Qdrant:Host"] = "localhost",
	};

	[Fact]
	public void Production_MissingDefaultConnection_FailsFast()
	{
		var data = ValidBase();
		data["ConnectionStrings:DefaultConnection"] = "__REQUIRED__";

		var report = Validator.Validate(Config(data), new FakeHostEnvironment { EnvironmentName = Environments.Production });

		Assert.True(report.HasErrors);
		Assert.Contains(report.Errors, e => e.Key == "ConnectionStrings:DefaultConnection");
	}

	[Fact]
	public void Production_AllValid_NoErrors()
	{
		var report = Validator.Validate(Config(ValidBase()), new FakeHostEnvironment { EnvironmentName = Environments.Production });

		Assert.False(report.HasErrors);
		Assert.Empty(report.Errors);
	}

	[Fact]
	public void Development_MissingConnectionString_IsWarningNotError()
	{
		var data = ValidBase();
		data["ConnectionStrings:DefaultConnection"] = "__REQUIRED__";

		var report = Validator.Validate(Config(data), new FakeHostEnvironment { EnvironmentName = Environments.Development });

		Assert.False(report.HasErrors);
		Assert.Contains(report.Warnings, w => w.Key == "ConnectionStrings:DefaultConnection");
	}

	[Fact]
	public void TestEnvironment_TreatedLikeDevelopment_WarningOnly()
	{
		var data = ValidBase();
		data["ConnectionStrings:DefaultConnection"] = "__REQUIRED__";

		var report = Validator.Validate(Config(data), new FakeHostEnvironment { EnvironmentName = "Test" });

		Assert.False(report.HasErrors);
		Assert.Contains(report.Warnings, w => w.Key == "ConnectionStrings:DefaultConnection");
	}

	[Fact]
	public void Production_MissingLlmOrEmbeddingKey_FailsFast()
	{
		var data = ValidBase();
		data["Qwen:ApiKey"] = "__SET_VIA_ENV_Qwen__ApiKey_OR_appsettings.Local.json__";

		var report = Validator.Validate(Config(data), new FakeHostEnvironment { EnvironmentName = Environments.Production });

		Assert.True(report.HasErrors);
		Assert.Contains(report.Errors, e => e.Key == "Qwen:ApiKey");
	}

	[Fact]
	public void Production_MissingQdrantHost_FailsFast()
	{
		var data = ValidBase();
		data.Remove("Qdrant:Host");

		var report = Validator.Validate(Config(data), new FakeHostEnvironment { EnvironmentName = Environments.Production });

		Assert.True(report.HasErrors);
		Assert.Contains(report.Errors, e => e.Key == "Qdrant:Host");
	}

	[Fact]
	public void Production_AllowedHostsWildcard_IsWarningOnly()
	{
		var data = ValidBase();
		data["AllowedHosts"] = "*";

		var report = Validator.Validate(Config(data), new FakeHostEnvironment { EnvironmentName = Environments.Production });

		Assert.False(report.HasErrors);
		Assert.Contains(report.Warnings, w => w.Key == "AllowedHosts");
	}

	[Fact]
	public void Production_PlaceholderConnectionString_ErrorDoesNotLeakToken()
	{
		var data = ValidBase();
		data["ConnectionStrings:DefaultConnection"] = "__REQUIRED__";

		var report = Validator.Validate(Config(data), new FakeHostEnvironment { EnvironmentName = Environments.Production });

		Assert.True(report.HasErrors);
		var serialized = JsonSerializer.Serialize(report);
		// 绝不输出占位符/密钥值：仅含键路径与通用原因。
		Assert.DoesNotContain("__REQUIRED__", serialized);
	}

	[Fact]
	public void Report_NeverContainsRealSecretValue()
	{
		var data = ValidBase();
		data["ConnectionStrings:DefaultConnection"] = "Server=secret-host;User Id=admin;Password=supersecret";
		data["Qwen:ApiKey"] = "sk-secret-12345";

		var report = Validator.Validate(Config(data), new FakeHostEnvironment { EnvironmentName = Environments.Production });

		Assert.False(report.HasErrors);
		var serialized = JsonSerializer.Serialize(report);
		Assert.DoesNotContain("supersecret", serialized);
		Assert.DoesNotContain("sk-secret-12345", serialized);
	}
}
