using System.Collections.Generic;
using SuperBuilder_AI.Models.DTO;
using SuperBuilder_AI.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// 元数据发现启发式：字典表识别 / 角色映射（code-name-type）/ 外键展示列优选。
/// 关键不变量：只依赖「角色」而非字面列名，以适配不同租户的异构 schema。
/// </summary>
public class MetadataDiscoveryHeuristicsTests
{
	[Theory]
	[InlineData("sys_dict", true)]
	[InlineData("base_code", true)]
	[InlineData("t_code_table", true)]
	[InlineData("wms_dict_type", true)]
	[InlineData("字典表", true)]
	[InlineData("wms_storage_receipt", false)]
	[InlineData("wms_warehouse", false)]
	[InlineData("pms_complete_storage", false)]
	public void IsDictionaryCandidate_MatchesOnlyDictLikeNames(string tableName, bool expected)
		=> Assert.Equal(expected, MetadataDiscoveryHeuristics.IsDictionaryCandidate(tableName));

	[Fact]
	public void ResolveRoles_StandardCodeNameTypeLayout()
	{
		var ok = MetadataDiscoveryHeuristics.TryResolveDictionaryRoles(
			Cols(("dict_code", false), ("dict_name", false), ("dict_type", false)),
			out var code, out var name, out var type);

		Assert.True(ok);
		Assert.Equal("dict_code", code);
		Assert.Equal("dict_name", name);
		Assert.Equal("dict_type", type);
	}

	[Fact]
	public void ResolveRoles_HeterogeneousLayout_UsesRolesNotLiteralNames()
	{
		// 异构租户的字典表用 key/value/group 命名：仍须按角色正确解析。
		var ok = MetadataDiscoveryHeuristics.TryResolveDictionaryRoles(
			Cols(("item_key", false), ("item_value", false), ("item_group", false)),
			out var code, out var name, out var type);

		Assert.True(ok);
		Assert.Equal("item_key", code);
		Assert.Equal("item_value", name);
		Assert.Equal("item_group", type);
	}

	[Fact]
	public void ResolveRoles_WithoutTypeColumn_StillSucceeds()
	{
		var ok = MetadataDiscoveryHeuristics.TryResolveDictionaryRoles(
			Cols(("code", false), ("name", false)),
			out var code, out var name, out var type);

		Assert.True(ok);
		Assert.Equal("code", code);
		Assert.Equal("name", name);
		Assert.Null(type);
	}

	[Fact]
	public void ResolveRoles_WithoutNameRole_Fails()
	{
		// 只有 code + 数值列，无 name 角色 → 宁可不建配置。
		var ok = MetadataDiscoveryHeuristics.TryResolveDictionaryRoles(
			Cols(("code", false), ("amount", false)),
			out _, out _, out _);

		Assert.False(ok);
	}

	[Fact]
	public void ResolveRoles_TooNarrow_Fails()
	{
		Assert.False(MetadataDiscoveryHeuristics.TryResolveDictionaryRoles(
			Cols(("code", false)), out _, out _, out _));
	}

	[Fact]
	public void PickDisplayColumn_PrefersNameColumnOverPrimaryKey()
	{
		var byTable = new Dictionary<string, List<ColumnMetadataDto>>
		{
			["wms_warehouse"] = new()
			{
				new ColumnMetadataDto { TableName = "wms_warehouse", ColumnName = "id", IsPrimaryKey = true },
				new ColumnMetadataDto { TableName = "wms_warehouse", ColumnName = "warehouse_name" }
			}
		};

		Assert.Equal("warehouse_name",
			MetadataDiscoveryHeuristics.PickDisplayColumn(byTable, "wms_warehouse", "id"));
	}

	[Fact]
	public void PickDisplayColumn_FallsBackToPrimaryKey()
	{
		var byTable = new Dictionary<string, List<ColumnMetadataDto>>
		{
			["wms_warehouse"] = new()
			{
				new ColumnMetadataDto { TableName = "wms_warehouse", ColumnName = "id", IsPrimaryKey = true },
				new ColumnMetadataDto { TableName = "wms_warehouse", ColumnName = "capacity" }
			}
		};

		Assert.Equal("id", MetadataDiscoveryHeuristics.PickDisplayColumn(byTable, "wms_warehouse", "id"));
	}

	[Fact]
	public void PickDisplayColumn_UnknownTable_FallsBackToReferencedColumn()
	{
		Assert.Equal("id", MetadataDiscoveryHeuristics.PickDisplayColumn(null, "unknown", "id"));
	}

	/// <summary>
	/// 实测回放：PMIS（JeeSite 系）<c>js_sys_dict_data</c> 共 44 列，其中
	/// <c>parent_codes</c> 会误命中 code 角色、<c>tree_names</c> 会误命中 name 角色。
	/// 语义列过滤后必须解析出 dict_code / dict_label / dict_type。
	/// </summary>
	[Fact]
	public void ResolveRoles_JeeSiteWideDictionaryTable_IgnoresNoiseColumns()
	{
		var raw = new List<ColumnMetadataDto>();
		foreach (var n in new[]
		{
			"is_sys", "parent_code", "parent_codes", "remarks", "status",
			"tree_leaf", "tree_level", "tree_names", "tree_sort", "tree_sorts",
			"update_by", "update_date", "extend_s1", "extend_s2", "extend_json",
			"extend_i1", "extend_f1", "extend_d1", "dict_value", "dict_type",
			"dict_label", "dict_icon", "dict_code", "description", "css_style",
			"corp_name", "corp_code",
		})
			raw.Add(new ColumnMetadataDto { TableName = "js_sys_dict_data", ColumnName = n });

		var semantic = MetadataDiscoveryHeuristics.SemanticColumns(raw);
		Assert.Equal(6, semantic.Count);
		Assert.DoesNotContain(semantic, c => c.ColumnName == "parent_codes");
		Assert.DoesNotContain(semantic, c => c.ColumnName == "tree_names");

		Assert.True(MetadataDiscoveryHeuristics.TryResolveDictionaryRoles(
			semantic, out var code, out var name, out var type));
		Assert.Equal("dict_code", code);
		Assert.Equal("dict_label", name);
		Assert.Equal("dict_type", type);
	}

	/// <summary>宽度闸门跑在语义列上：44 列的表有效宽度仍须落在可用区间。</summary>
	[Fact]
	public void EffectiveDictionaryWidth_WideExtensionTableStaysNarrow()
	{
		var raw = new List<ColumnMetadataDto>
		{
			new() { ColumnName = "dict_code" },
			new() { ColumnName = "dict_label" },
		};
		for (var i = 1; i <= 20; i++)
			raw.Add(new ColumnMetadataDto { ColumnName = $"extend_s{i}" });

		Assert.Equal(22, raw.Count);               // 2 语义列 + 20 扩展列
		Assert.Equal(2, MetadataDiscoveryHeuristics.EffectiveDictionaryWidth(raw));
	}

	[Fact]
	public void SemanticColumns_NullOrEmpty_YieldsEmpty()
	{
		Assert.Empty(MetadataDiscoveryHeuristics.SemanticColumns(null));
		Assert.Empty(MetadataDiscoveryHeuristics.SemanticColumns(new List<ColumnMetadataDto>()));
		Assert.Equal(0, MetadataDiscoveryHeuristics.EffectiveDictionaryWidth(null));
	}

	// ── 注释图例 → 码值映射（ValueMapJson 的唯一自动写入方） ─────────────────────

	[Fact]
	public void Legend_DashForm_ParsesAllPairs()
	{
		var ok = MetadataDiscoveryHeuristics.TryParseValueMapFromComment(
			"状态 0-已创建 1-执行中 2-已完成 3-已关闭 4-已归档", out var json);

		Assert.True(ok);
		Assert.Equal("{\"0\":\"已创建\",\"1\":\"执行中\",\"2\":\"已完成\",\"3\":\"已关闭\",\"4\":\"已归档\"}", json);
	}

	[Fact]
	public void Legend_FullWidthColonAndParens_AreNormalized()
	{
		var ok = MetadataDiscoveryHeuristics.TryParseValueMapFromComment(
			"单据来源类型（0：生产 1：采购地磅 2采购入库）", out var json);

		Assert.True(ok);
		Assert.Equal("{\"0\":\"生产\",\"1\":\"采购地磅\",\"2\":\"采购入库\"}", json);
	}

	[Fact]
	public void Legend_WhitespaceSeparatedFlags_Parse()
	{
		Assert.True(MetadataDiscoveryHeuristics.TryParseValueMapFromComment(
			"是否删除标识：0否 1是", out var json));
		Assert.Equal("{\"0\":\"否\",\"1\":\"是\"}", json);
	}

	/// <summary>实测存在缺号图例（wms_sign_feedback.state = 0,1,3），须容忍有界跳号。</summary>
	[Fact]
	public void Legend_GappedCodes_AreAccepted()
	{
		Assert.True(MetadataDiscoveryHeuristics.TryParseValueMapFromComment(
			"反馈状态（0:待确认反馈 1：已反馈 3:已退回）", out var json));
		Assert.Equal("{\"0\":\"待确认反馈\",\"1\":\"已反馈\",\"3\":\"已退回\"}", json);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("仓库id")]
	[InlineData("金蝶收料通知单编码")]
	[InlineData("状态")]                                        // 单条图例不足
	[InlineData("单据来源类型（0：生产）")]                        // 仅 1 条
	[InlineData("盘点范围（2：物料分类 3：物料）")]                // 起始码非 0/1
	[InlineData("类型 0：当前所有数据（实时库存）1：145仓库当前数据 445 仓库数据")] // 散文
	[InlineData("create_time 2024-01-01 2024-12-31")]          // 年份不是图例（4 位数不匹配）
	public void Legend_AmbiguousOrProseComments_AreRejected(string? comment)
		=> Assert.False(MetadataDiscoveryHeuristics.TryParseValueMapFromComment(comment, out var json));

	/// <summary>图例不在注释前部时不解析（句子中段的数字不是枚举）。</summary>
	[Fact]
	public void Legend_CodeTooFarIntoComment_IsRejected()
	{
		var prefix = new string('说', 25);
		Assert.False(MetadataDiscoveryHeuristics.TryParseValueMapFromComment(
			$"{prefix} 0-甲 1-乙", out _));
	}

	/// <summary>标签含数字即视为散文，不接受（实测 wms_test_inventory.type 即此类）。</summary>
	[Fact]
	public void Legend_LabelContainingDigits_IsRejected()
	{
		Assert.False(MetadataDiscoveryHeuristics.TryParseValueMapFromComment(
			"来源 0-生产 1-145仓库", out _));
	}

	private static List<ColumnMetadataDto> Cols(params (string Name, bool IsPk)[] cols)
	{
		var list = new List<ColumnMetadataDto>();
		foreach (var (name, isPk) in cols)
			list.Add(new ColumnMetadataDto { TableName = "t", ColumnName = name, IsPrimaryKey = isPk });
		return list;
	}
}
