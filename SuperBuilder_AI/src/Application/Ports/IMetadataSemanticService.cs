using SuperBuilder_AI.Models.Metadata;


namespace SuperBuilder_AI.Interfaces;

/// <summary>
/// Metadata字段语义生成服务接口。
///
/// 职责：
///
/// MetadataColumn
///        ↓
/// AI语义分析
///        ↓
/// MetadataSemantic
///
/// 支持：
/// 1. 单字段语义生成（兼容旧流程）
/// 2. 批量字段语义生成（Phase 1.4.5优化）
///
/// Batch模式用于解决：
///
/// 原流程：
///
/// MetadataColumn
///      ↓
/// foreach
///      ↓
/// Qwen API
///
/// 1000字段 = 1000次请求
///
///
/// 新流程：
///
/// MetadataColumn集合
///      ↓
/// Batch Prompt
///      ↓
/// 一次/少量Qwen请求
///      ↓
/// 批量保存MetadataSemantic
///
/// </summary>
public interface IMetadataSemanticService
{


	/// <summary>
	/// 根据单个字段生成业务语义。
	///
	/// 兼容旧版本调用。
	/// </summary>
	/// <param name="column">
	/// 元数据字段
	/// </param>
	/// <returns>
	/// AI生成的字段语义
	/// </returns>
	Task<MetadataSemantic?>
		GenerateAsync(
			MetadataColumn column);




	/// <summary>
	/// 批量生成字段业务语义。
	///
	/// Phase 1.4.5:
	///
	/// MetadataColumn列表
	///          |
	///          |
	///      Qwen Batch
	///          |
	///          |
	/// MetadataSemantic列表
	///
	/// 通过 MetadataColumn.Id
	/// 精确关联字段。
	///
	/// 不使用：
	/// BusinessKey
	/// TableName + ColumnName
	///
	/// 防止多数据库同名字段冲突。
	/// </summary>
	/// <param name="columns">
	/// 待生成语义的字段集合
	/// </param>
	/// <returns>
	/// 批量生成的字段语义集合
	/// </returns>
	Task<List<MetadataSemantic>>
		GenerateBatchAsync(
			List<MetadataColumn> columns);

	Task<List<MetadataSemantic>> GenerateBatchAsync(
		List<MetadataColumn> columns,
		Action<SemanticGenerationProgress>? progress,
		CancellationToken ct)
		=> GenerateBatchAsync(columns);



}

public sealed record SemanticGenerationProgress(
	int BatchesCompleted,
	int BatchesTotal,
	int FieldsCompleted,
	int FieldsTotal,
	int FieldsGenerated,
	int FieldsFailed,
	string Message);
