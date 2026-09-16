using System;
using System.Linq;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// Phase 3：显式纠正指令解析（仅识别明确句式，避免规则中毒）。
/// </summary>
public class CorrectionInstructionParserTests
{
	[Fact]
	public void Parse_ValueMap_ExtractsColumnAndPairs()
	{
		var captures = CorrectionInstructionParser.Parse(
			"type 字段的取值应该是 1=采购入库，2=调拨入库");

		var capture = Assert.Single(captures);
		Assert.Equal(CorrectionKind.ValueMap, capture.Kind);
		Assert.Equal("type", capture.Payload.ColumnName);
		Assert.Equal("采购入库", capture.Payload.ValueMap!["1"]);
		Assert.Equal("调拨入库", capture.Payload.ValueMap!["2"]);
	}

	[Fact]
	public void Parse_ColumnDisplayDeclaration_WithoutMap()
	{
		var captures = CorrectionInstructionParser.Parse("status 字段应该显示状态名称");

		var capture = Assert.Single(captures);
		Assert.Equal(CorrectionKind.ColumnDisplay, capture.Kind);
		Assert.Equal("status", capture.Payload.ColumnName);
		Assert.Null(capture.Payload.ValueMap);
	}

	[Fact]
	public void Parse_PlainQuestion_YieldsNothing()
	{
		Assert.Empty(CorrectionInstructionParser.Parse("最近十条入库凭证"));
		Assert.Empty(CorrectionInstructionParser.Parse(null));
		Assert.Empty(CorrectionInstructionParser.Parse("   "));
	}

	[Fact]
	public void ExtractValueMap_DropsPureNumericLabels()
	{
		var map = CorrectionInstructionParser.ExtractValueMap("type：1=采购入库，2=3");

		Assert.Equal("采购入库", map["1"]);
		// label 为纯数字的项视为条件而非映射，丢弃。
		Assert.False(map.ContainsKey("2"));
	}

	[Fact]
	public void Parse_ColumnDisplay_WithDictionaryCategoryHint()
	{
		var captures = CorrectionInstructionParser.Parse("type 字段应该显示 receipt_type 名称");

		var capture = Assert.Single(captures);
		Assert.Equal(CorrectionKind.ColumnDisplay, capture.Kind);
		Assert.Equal("type", capture.Payload.ColumnName);
		Assert.Equal("receipt_type", capture.Payload.CategoryHint);
	}

	[Fact]
	public void ExtractCategoryHint_RejectsNaturalLanguageOrSelfReference()
	{
		// 「状态名称」是自然语言描述，不是字典分类标识符。
		Assert.Null(CorrectionInstructionParser.ExtractCategoryHint("status 字段应该显示状态名称", "status"));
		// 目标词与列名相同 → 无信息量。
		Assert.Null(CorrectionInstructionParser.ExtractCategoryHint("type 字段应该显示 type 名称", "type"));
		Assert.Equal("receipt_type", CorrectionInstructionParser.ExtractCategoryHint("type 应显示 receipt_type 名称"));
	}

	[Fact]
	public void ExtractCategoryHint_SupportsMultiCategoryList()
	{
		// PMIS 实测：WMS 单据表 type 单列码值跨多个 dict_type，必须能声明多分类。
		Assert.Equal("warehousing_type,outbound_type",
			CorrectionInstructionParser.ExtractCategoryHint(
				"type 字段应该显示 warehousing_type，outbound_type 名称", "type"));

		Assert.Equal("warehousing_type,outbound_type,variation_type",
			CorrectionInstructionParser.ExtractCategoryHint(
				"type 应显示 warehousing_type,outbound_type|variation_type"));

		// 单分类（历史行为）不变。
		Assert.Equal("receipt_type",
			CorrectionInstructionParser.ExtractCategoryHint("type 应该显示 receipt_type 名称", "type"));
	}

	[Fact]
	public void Parse_ColumnDisplay_WithMultiCategoryHint_RoundTrips()
	{
		var captures = CorrectionInstructionParser.Parse(
			"type 字段应该显示 warehousing_type,outbound_type 名称");

		var capture = Assert.Single(captures);
		Assert.Equal(CorrectionKind.ColumnDisplay, capture.Kind);
		Assert.Equal("type", capture.Payload.ColumnName);
		Assert.Equal("warehousing_type,outbound_type", capture.Payload.CategoryHint);
	}

	[Fact]
	public void ExtractColumn_IgnoresSqlKeywords()
	{
		Assert.Equal("warehouse_id", CorrectionInstructionParser.ExtractColumn("warehouse_id 列应该显示仓库名称"));
		Assert.Null(CorrectionInstructionParser.ExtractColumn("select 应该显示什么"));
	}
}
