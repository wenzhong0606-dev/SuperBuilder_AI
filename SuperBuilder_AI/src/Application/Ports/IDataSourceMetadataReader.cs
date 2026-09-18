using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Models.DTO;

namespace SuperBuilder_AI.Interfaces;

/// <summary>
/// 数据源元数据读取器接口。
/// 提供从外部数据源（如 MySQL、SQL Server、Postgres 等）读取表和列元数据信息的方法契约。
/// 实现类应根据特定数据源的驱动或方言实现具体读取逻辑。
///
/// 所有方法接受 <see cref="CancellationToken"/>，用于在读取边界（阶段/每表）响应软取消。
/// </summary>
public interface IDataSourceMetadataReader
{
	/// <summary>
	/// 异步获取指定连接字符串下的表元数据列表。
	/// </summary>
	/// <exception cref="System.ArgumentException">当 connectionString 为空或无效时抛出。</exception>
	/// <exception cref="System.Data.Common.DbException">在与数据库通信时出现底层错误时抛出。</exception>
	/// <exception cref="System.OperationCanceledException">当 ct 触发取消时抛出。</exception>
	Task<List<TableMetadataDto>> GetTablesAsync(string connectionString, CancellationToken ct = default);

	Task<List<TableMetadataDto>> GetTablesAsync(string connectionString, string? dbType, CancellationToken ct = default)
		=> GetTablesAsync(connectionString, ct);

	/// <summary>
	/// 异步获取指定连接字符串下的列元数据列表（全表）。
	/// </summary>
	/// <exception cref="System.OperationCanceledException">当 ct 触发取消时抛出。</exception>
	Task<List<ColumnMetadataDto>> GetColumnsAsync(string connectionString, CancellationToken ct = default);

	Task<List<ColumnMetadataDto>> GetColumnsAsync(string connectionString, string? dbType, CancellationToken ct = default)
		=> GetColumnsAsync(connectionString, ct);

	/// <summary>
	/// 异步获取指定连接字符串下、给定表名集合的列元数据列表（逐表边界读取，单表失败仅影响该表重试）。
	/// </summary>
	/// <exception cref="System.OperationCanceledException">当 ct 触发取消时抛出。</exception>
	Task<List<ColumnMetadataDto>> GetColumnsAsync(
		string connectionString,
		string? dbType,
		IEnumerable<string> tableNames,
		CancellationToken ct = default);

	/// <summary>
	/// 异步获取外键关系列表（本表列 → 引用表列）。
	/// 用于结果译码时的字段→名称列绑定（同数据源内）。
	/// 外键为可选能力：读取器未实现（或该数据源不支持）时返回空集合，由默认实现兜底 —— 不阻断元数据扫描。
	/// </summary>
	Task<List<ForeignKeyMetadataDto>> GetForeignKeysAsync(string connectionString, CancellationToken ct = default)
		=> Task.FromResult(new List<ForeignKeyMetadataDto>());

	Task<List<ForeignKeyMetadataDto>> GetForeignKeysAsync(string connectionString, string? dbType, CancellationToken ct = default)
		=> GetForeignKeysAsync(connectionString, ct);

	/// <summary>
	/// 枚举目标实例下可扫描的数据库名（§10.1 跨库扫描）。排除系统库。
	/// 默认实现返回空集合（不支持/未实现的读取器），由调用方决定单库扫描。
	/// </summary>
	Task<List<string>> GetDatabasesAsync(string connectionString, string? dbType = null, CancellationToken ct = default)
		=> Task.FromResult(new List<string>());
}
