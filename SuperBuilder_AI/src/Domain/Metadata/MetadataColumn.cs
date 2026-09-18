using SuperBuilder_AI.Models;

namespace SuperBuilder_AI.Models.Metadata;

/// <summary>
/// 表示数据表列的元数据信息。
///
/// 用于描述业务数据库中的字段结构，
/// 同时承载 AI 语义分析、Embedding、Qdrant 向量索引信息。
/// </summary>
public class MetadataColumn : BaseEntity
{

	/// <summary>
	/// 所属元数据表标识。
	/// </summary>
	public long MetadataTableId { get; set; }



	/// <summary>
	/// 关联的元数据表实体。
	/// </summary>
	public MetadataTable? MetadataTable { get; set; }



	/// <summary>
	/// 字段业务唯一标识。
	///
	/// 用于解决多数据库、多数据源中：
	///
	/// SalesOrder.Amount
	///
	/// 重复的问题。
	///
	/// 格式建议：
	///
	/// DataSourceId.Schema.Table.Column
	///
	/// 示例：
	///
	/// 1.dbo.SalesOrder.Amount
	///
	/// 注意：
	/// 该字段不是数据库主键，
	/// Id 仍然作为 EF Core 主键。
	/// </summary>
	public string? BusinessKey { get; set; }



	/// <summary>
	/// 字段语义信息。
	///
	/// 一对一关系：
	///
	/// MetadataColumn
	///        |
	///        |
	/// MetadataSemantic
	///
	/// </summary>
	public MetadataSemantic? Semantic { get; set; }



	/// <summary>
	/// 列名称（必填）。
	/// </summary>
	public string ColumnName { get; set; } = string.Empty;


	/// <summary>
	/// 列序号（在表中的物理顺序，从 0 开始）。
	/// </summary>
	public int Ordinal { get; set; }


	/// <summary>
	/// 原生类型（数据库返回的类型名，如 varchar / decimal / bigint）。
	/// </summary>
	public string? NativeType { get; set; }


	/// <summary>
	/// 精度（数值/字符类型的精度，如 decimal(18,4) 的 18）。
	/// </summary>
	public int? Precision { get; set; }


	/// <summary>
	/// 小数位（数值类型的小数位数，如 decimal(18,4) 的 4）。
	/// </summary>
	public int? Scale { get; set; }



	/// <summary>
	/// 列注释或数据库描述。
	/// </summary>
	public string? ColumnComment { get; set; }



	/// <summary>
	/// 数据类型。
	///
	/// 示例：
	///
	/// varchar
	/// int
	/// decimal
	/// datetime
	///
	/// </summary>
	public string? DataType { get; set; }



	/// <summary>
	/// 字段长度或精度。
	/// </summary>
	public long? Length { get; set; }



	/// <summary>
	/// 是否允许为空。
	/// </summary>
	public bool? IsNullable { get; set; }



	/// <summary>
	/// 是否为主键字段。
	/// </summary>
	public bool? IsPrimaryKey { get; set; }



	/// <summary>
	/// Embedding搜索文本。
	///
	/// 内容包括：
	///
	/// 表信息
	/// 字段信息
	/// 字段描述
	/// 
	/// Phase 1.4.5后会融合Semantic内容。
	/// </summary>
	public string? SearchText { get; set; }



	/// <summary>
	/// Qdrant向量ID。
	///
	/// 保存字段对应的Vector Point Id。
	/// </summary>
	public string? VectorId { get; set; }

	/// <summary>
	/// 列向量使用的 Embedding 模型。
	/// </summary>
	public string? EmbeddingModel { get; set; }

	/// <summary>
	/// 列向量维度。
	/// </summary>
	public int? VectorDimension { get; set; }

	/// <summary>
	/// 向量最近同步时间（UTC）。
	/// </summary>
	public DateTime? VectorSyncTime { get; set; }

	/// <summary>
	/// 向量状态：待同步(Pending) / 已同步(Synced) / 同步失败(Failed) / 过期(Stale)。
	/// </summary>
	public string? VectorStatus { get; set; }

	/// <summary>
	/// 向量同步错误码（同步失败时记录脱敏后的异常类型名）。
	/// </summary>
	public string? VectorErrorCode { get; set; }

	/// <summary>
	/// 写入本行的扫描批次号（§L 版本生命周期）；等于 job.BatchVersion。Ask 仅读取 == ActiveMetadataVersion 的行。
	/// </summary>
	public int MetadataVersion { get; set; }

	/// <summary>
	/// 外键目标表名（同数据源内；跨数据源引用由 QueryCorrectionRules / MetadataDictionaryConfig 承载）。
	/// </summary>
	public string? ReferencedTable { get; set; }

	/// <summary>
	/// 外键目标列名（被引用表的主键列）。
	/// </summary>
	public string? ReferencedColumn { get; set; }

	/// <summary>
	/// 外键目标表的展示列名（结果译码写 {col}_name 用）。
	/// 扫描阶段启发式优选名称型列，否则取被引用表主键列。
	/// </summary>
	public string? ReferencedDisplayColumn { get; set; }

	/// <summary>
	/// 枚举/码值映射 JSON：{"1":"采购入库","2":"调拨入库"}。
	/// 来源可为列注释图例解析或学习规则；结果译码时逐行替换单元格为 label。
	/// </summary>
	public string? ValueMapJson { get; set; }

	/// <summary>
	/// 是否由跨数据源字典表（如 PMIS）译码：true 时按 DictConfigId 关联字典配置。
	/// </summary>
	public bool IsDictBacked { get; set; }

	/// <summary>
	/// 关联的跨源字典配置 Id（MetadataDictionaryConfig）。
	///
	/// 软引用：不建数据库外键。原因：DataSources 已同时经 MetadataTables（级联）与
	/// MetadataDictionaryConfigs（级联）到达 MetadataColumns，再加一条外键会构成
	/// SQL Server 禁止的多重级联路径（错误 1785）。有效性由应用层按租户+数据源校验。
	/// </summary>
	public long? DictConfigId { get; set; }

	/// <summary>
	/// 字典取值分类（对应字典表 TypeColumn 的值，如 "receipt_type"）。
	///
	/// <para>
	/// <b>支持多值</b>，以 <c>, ; |</c>（含全角）分隔，译码时展开为
	/// <c>dict_type IN (...)</c>。实测必要：WMS 单据表的 <c>type</c> 单列码值同时归属
	/// warehousing_type / outbound_type / variation_type / Input_output_type / wms_check_type
	/// 五个分类，单值会漏译大部分码值。
	/// </para>
	/// </summary>
	public string? DictCategoryValue { get; set; }

}
