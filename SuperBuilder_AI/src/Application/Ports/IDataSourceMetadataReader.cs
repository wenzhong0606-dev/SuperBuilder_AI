using SuperBuilder_AI.Models.DTO;


namespace SuperBuilder_AI.Interfaces;

/// <summary>
/// 数据源元数据读取器接口。
/// 提供从外部数据源（如 MySQL、SQL Server、Postgres 等）读取表和列元数据信息的方法契约。
/// 实现类应根据特定数据源的驱动或方言实现具体读取逻辑。
/// </summary>
public interface IDataSourceMetadataReader
{
	/// <summary>
	/// 异步获取指定连接字符串下的表元数据列表。
	/// </summary>
	/// <param name="connectionString">目标数据库的连接字符串，用于建立到数据源的连接。</param>
	/// <returns>包含表元数据的 DTO 列表，每项表示一个表的基本信息（如表名、注释等）。</returns>
	/// <exception cref="System.ArgumentException">当 connectionString 为空或无效时抛出。</exception>
	/// <exception cref="System.Data.Common.DbException">在与数据库通信时出现底层错误时抛出。</exception>
	Task<List<TableMetadataDto>> GetTablesAsync(string connectionString);

	Task<List<TableMetadataDto>> GetTablesAsync(string connectionString, string? dbType)
		=> GetTablesAsync(connectionString);

	/// <summary>
	/// 异步获取指定连接字符串下的列元数据列表。
	/// </summary>
	/// <param name="connectionString">目标数据库的连接字符串，用于建立到数据源的连接。</param>
	/// <returns>包含列元数据的 DTO 列表，每项表示表中某一列的详细信息（如列名、数据类型、是否为空、注释等）。</returns>
	/// <exception cref="System.ArgumentException">当 connectionString 为空或无效时抛出。</exception>
	/// <exception cref="System.Data.Common.DbException">在与数据库通信时出现底层错误时抛出。</exception>
	Task<List<ColumnMetadataDto>> GetColumnsAsync(string connectionString);

	Task<List<ColumnMetadataDto>> GetColumnsAsync(string connectionString, string? dbType)
		=> GetColumnsAsync(connectionString);

	/// <summary>
	/// 异步获取外键关系列表（本表列 → 引用表列）。
	/// 用于结果译码时的字段→名称列绑定（同数据源内）。
	///
	/// 外键为可选能力：读取器未实现（或该数据源不支持）时返回空集合，
	/// 由默认实现兜底 —— 不阻断元数据扫描。
	/// </summary>
	Task<List<ForeignKeyMetadataDto>> GetForeignKeysAsync(string connectionString)
		=> Task.FromResult(new List<ForeignKeyMetadataDto>());

	Task<List<ForeignKeyMetadataDto>> GetForeignKeysAsync(string connectionString, string? dbType)
		=> GetForeignKeysAsync(connectionString);
}