using SuperBuilder_AI.Models.Organization;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M1-02 TenantSettingPolicy 纯单元测试：Key 允许目录、安全配置锁定、DataType 白名单与值校验。
/// </summary>
public sealed class TenantSettingPolicyTests
{
	[Theory]
	[InlineData("localization:defaultCulture", true)]
	[InlineData("theme:defaultKey", true)]
	[InlineData("workspace:name", true)]
	[InlineData("security:token", true)]
	[InlineData("feature:flag", true)]
	[InlineData("ui:locale", true)]
	[InlineData("integration:webhook", true)]
	[InlineData("random:foo", false)]
	[InlineData("noprefix", false)]
	public void IsKnownKey_AllowList(string key, bool expected)
		=> Assert.Equal(expected, TenantSettingPolicy.IsKnownKey(key));

	[Theory]
	[InlineData("localization:defaultCulture", true)]
	[InlineData("localization:availableCultures", true)]
	[InlineData("security:anything", true)]
	[InlineData("theme:defaultKey", false)]
	[InlineData("workspace:name", false)]
	public void IsLockedKey_DetectsSecurityConfig(string key, bool expected)
		=> Assert.Equal(expected, TenantSettingPolicy.IsLockedKey(key));

	[Fact]
	public void ValidateTenantWrite_RejectsUnknownPrefix()
		=> Assert.False(TenantSettingPolicy.ValidateTenantWrite("evil:payload", out _));

	[Fact]
	public void ValidateTenantWrite_RejectsLockedKey()
		=> Assert.False(TenantSettingPolicy.ValidateTenantWrite("localization:defaultCulture", out _));

	[Fact]
	public void ValidateTenantWrite_AcceptsKnownNonLocked()
		=> Assert.True(TenantSettingPolicy.ValidateTenantWrite("workspace:name", out _));

	[Theory]
	[InlineData("string")]
	[InlineData("int")]
	[InlineData("bool")]
	[InlineData("json")]
	public void IsValidDataType_WhiteList(string dt)
		=> Assert.True(TenantSettingPolicy.IsValidDataType(dt));

	[Theory]
	[InlineData("xml")]
	[InlineData("")]
	[InlineData(null)]
	public void IsValidDataType_Rejects(string? dt)
		=> Assert.False(TenantSettingPolicy.IsValidDataType(dt));

	[Fact]
	public void TryValidateValue_IntParses()
		=> Assert.True(TenantSettingPolicy.TryValidateValue("int", "123", out _));

	[Fact]
	public void TryValidateValue_IntRejectsNonNumeric()
		=> Assert.False(TenantSettingPolicy.TryValidateValue("int", "abc", out _));

	[Fact]
	public void TryValidateValue_BoolParses()
		=> Assert.True(TenantSettingPolicy.TryValidateValue("bool", "true", out _));

	[Fact]
	public void TryValidateValue_BoolRejects()
		=> Assert.False(TenantSettingPolicy.TryValidateValue("bool", "yes", out _));

	[Fact]
	public void TryValidateValue_StringAndJsonAlwaysOk()
	{
		Assert.True(TenantSettingPolicy.TryValidateValue("string", "anything", out _));
		Assert.True(TenantSettingPolicy.TryValidateValue("json", "{\"a\":1}", out _));
	}
}
