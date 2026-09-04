using System;
using System.Collections.Generic;
using System.Globalization;

namespace SuperBuilder_AI.Models.Organization;

/// <summary>
/// M1-02 TenantSetting 写入策略：Key 允许目录、安全配置锁定、DataType 白名单与值校验。
/// 集中放置以便控制器与测试一致复用。
/// </summary>
public static class TenantSettingPolicy
{
	/// <summary>允许的 Key 前缀目录（业务域命名空间）。不在目录中的 Key 一律拒绝。</summary>
	public static readonly IReadOnlyList<string> AllowedKeyPrefixes = new[]
	{
		"localization:", "theme:", "workspace:", "security:", "feature:", "ui:", "integration:"
	};

	/// <summary>锁定键（平台/安全配置，禁止租户侧覆盖）。</summary>
	private static readonly HashSet<string> LockedKeys = new(StringComparer.OrdinalIgnoreCase)
	{
		"localization:availableCultures",
		"localization:defaultCulture"
	};

	/// <summary>合法 DataType 白名单。</summary>
	public static readonly IReadOnlyList<string> AllowedDataTypes = new[] { "string", "int", "bool", "json" };

	/// <summary>判断 Key 是否属于允许目录（前缀匹配）。</summary>
	public static bool IsKnownKey(string? key)
	{
		if (string.IsNullOrWhiteSpace(key)) return false;
		foreach (var prefix in AllowedKeyPrefixes)
			if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
		return false;
	}

	/// <summary>判断 Key 是否为锁定配置（租户不可覆盖）。</summary>
	public static bool IsLockedKey(string? key)
	{
		if (string.IsNullOrWhiteSpace(key)) return false;
		if (LockedKeys.Contains(key)) return true;
		// security: 命名空间整体视为安全配置，租户不可覆盖。
		return key.StartsWith("security:", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// 校验租户侧写入（非平台内部）：Key 必须属于允许目录且非锁定配置。
	/// 返回 false 时 <paramref name="error"/> 描述原因（未知前缀→prefix，锁定→locked）。
	/// </summary>
	public static bool ValidateTenantWrite(string? key, out string? error)
	{
		error = null;
		if (!IsKnownKey(key))
		{
			error = $"Key 不在允许目录中：{key}。";
			return false;
		}
		if (IsLockedKey(key))
		{
			error = $"配置 {key} 为锁定安全配置，租户不可覆盖。";
			return false;
		}
		return true;
	}

	/// <summary>平台内部写入：仅校验 Key 属于允许目录（允许写入锁定键）。</summary>
	public static bool ValidatePlatformWrite(string? key, out string? error)
	{
		error = null;
		if (!IsKnownKey(key))
		{
			error = $"Key 不在允许目录中：{key}。";
			return false;
		}
		return true;
	}

	/// <summary>DataType 是否合法。</summary>
	public static bool IsValidDataType(string? dataType)
		=> Array.Exists(AllowedDataTypes.ToArray(),
			d => string.Equals(d, dataType, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// 按 DataType 校验并规范化值：int/bool 需可解析；string/json 原样保留。
	/// 返回 false 时 <paramref name="error"/> 描述原因。
	/// </summary>
	public static bool TryValidateValue(string? dataType, string? value, out string? error)
	{
		error = null;
		if (!IsValidDataType(dataType))
		{
			error = $"DataType 必须为 {string.Join("/", AllowedDataTypes)} 之一。";
			return false;
		}
		var dt = dataType!.ToLowerInvariant();
		if (dt == "int")
		{
			if (value is null || !long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
			{
				error = "值无法解析为整数。";
				return false;
			}
		}
		else if (dt == "bool")
		{
			if (value is null ||
				!(value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
				  value.Equals("false", StringComparison.OrdinalIgnoreCase)))
			{
				error = "值必须为 true/false。";
				return false;
			}
		}
		return true;
	}
}
