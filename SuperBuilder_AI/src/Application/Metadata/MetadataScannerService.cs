using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.Metadata;


namespace SuperBuilder_AI.Services;

/// <summary>
/// Metadata扫描服务
///
/// 功能:
///
/// 1. 扫描业务数据库结构
/// 2. 同步MetadataTable
/// 3. 同步MetadataColumn
/// 4. Batch生成MetadataSemantic
/// 5. 创建Table/Column/Semantic Vector
/// 6. 回写Qdrant VectorId
///
/// 流程:
///
/// 数据库
///    ↓
/// MetadataTable
///    ↓
/// MetadataColumn
///    ↓
/// Qwen Batch Semantic
///    ↓
/// MetadataSemantic
///    ↓
/// Embedding
///    ↓
/// Qdrant
///
/// </summary>
public class MetadataScannerService
{


	private readonly SuperBIContext _context;


	private readonly IDataSourceMetadataReader _reader;


	private readonly IMetadataSearchTextBuilder _textBuilder;


	private readonly IMetadataSemanticService _semanticService;


	private readonly IMetadataVectorService _vectorService;



	public MetadataScannerService(
		SuperBIContext context,
		IDataSourceMetadataReader reader,
		IMetadataSearchTextBuilder textBuilder,
		IMetadataSemanticService semanticService,
		IMetadataVectorService vectorService)
	{

		_context = context;

		_reader = reader;

		_textBuilder = textBuilder;

		_semanticService = semanticService;

		_vectorService = vectorService;

	}






	/// <summary>
	/// 扫描Metadata
	/// </summary>
	public async Task ScanAsync(
		long tenantId,
		long dataSourceId,
		string connectionString)
	{


		// M0-06：元数据扫描写入强制从 DataSource 继承 TenantId。
		// 解析归属数据源，以 dataSource.TenantId 为权威写入租户；拒绝非归属租户的扫描请求。
		var dataSource = await _context.DataSources
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Id == dataSourceId, CancellationToken.None);
		if (dataSource is null)
			throw new KeyNotFoundException($"数据源 {dataSourceId} 不存在，无法扫描元数据。");
		if (dataSource.TenantId != tenantId)
			throw new InvalidOperationException(
				$"数据源 {dataSourceId} 不属于租户 {tenantId}（实际归属租户 {dataSource.TenantId}），拒绝元数据扫描写入。");
		var effectiveTenantId = dataSource.TenantId ?? 0;

		/*
		 * =============================
		 *
		 * 1.
		 * 读取数据库结构
		 *
		 * =============================
		 */


		var tables =
			await _reader
			.GetTablesAsync(
				connectionString);



		var columns =
			await _reader
			.GetColumnsAsync(
				connectionString);







		/*
		 * =============================
		 *
		 * 2.
		 * 加载已有Metadata
		 *
		 * =============================
		 */


		var existsTables =
			await _context.MetadataTables

			.Include(x =>
				x.Columns)

			.Where(x =>
				x.TenantId == effectiveTenantId
				&&
				x.DataSourceId == dataSourceId)

			.ToListAsync();







		foreach (var table in tables)
		{


			var metadataTable =
				existsTables
				.FirstOrDefault(x =>
					x.TableName ==
					table.TableName);




			if (metadataTable == null)
			{

				metadataTable =
					new MetadataTable
					{

					TenantId =
						effectiveTenantId,


						DataSourceId =
							dataSourceId,


						TableName =
							table.TableName,


						TableComment =
							table.TableComment

					};



				_context.MetadataTables
					.Add(metadataTable);

			}
			else
			{

				metadataTable.TableComment =
					table.TableComment;

			}







			/*
			 * =============================
			 *
			 * 3.
			 * Column同步
			 *
			 * =============================
			 */


			var tableColumns =
				columns
				.Where(x =>
					x.TableName ==
					table.TableName);



			foreach (var column in tableColumns)
			{


				var metadataColumn =
					metadataTable.Columns
					.FirstOrDefault(x =>
						x.ColumnName ==
						column.ColumnName);




				if (metadataColumn == null)
				{

					metadataColumn =
						new MetadataColumn
						{

							ColumnName =
								column.ColumnName,


							ColumnComment =
								column.ColumnComment,


							DataType =
								column.DataType,


							Length =
								column.Length,


							IsNullable =
								column.IsNullable,


							IsPrimaryKey =
								column.IsPrimaryKey

						};



					metadataTable.Columns
						.Add(metadataColumn);

				}
				else
				{

					metadataColumn.ColumnComment =
						column.ColumnComment;


					metadataColumn.DataType =
						column.DataType;

				}






				/*
				 * Column SearchText
				 */


				metadataColumn.SearchText =
					_textBuilder
					.BuildColumnText(

						table.TableName,

						column.ColumnName,

						column.ColumnComment,

						column.DataType

					);



			}







			/*
			 * =============================
			 *
			 * 4.
			 * Table SearchText
			 *
			 * =============================
			 */


			metadataTable.SearchText =
				_textBuilder
				.BuildMetadataText(

					table.TableName,

					table.TableComment,


					metadataTable.Columns
					.Select(x =>
						x.SearchText ?? "")

				);


		}







		/*
		 * =============================
		 *
		 * 5.
		 * 保存Metadata
		 *
		 * 获取Column Id
		 *
		 * =============================
		 */


		await _context
			.SaveChangesAsync();









		/*
		 * =============================
		 *
		 * 6.
		 * Batch生成Semantic
		 *
		 * =============================
		 */


		var semanticColumns =
			await _context.MetadataColumns

			.Include(x =>
				x.MetadataTable)

			.Include(x =>
				x.Semantic)

			.Where(x =>
				x.MetadataTable!.TenantId == effectiveTenantId
				&&
				x.MetadataTable.DataSourceId == dataSourceId
				&&
				x.Semantic == null)

			.ToListAsync();





		if (semanticColumns.Count > 0)
		{

			await _semanticService
				.GenerateBatchAsync(
					semanticColumns);

		}







		/*
		 * =============================
		 *
		 * 7.
		 * Vector同步
		 *
		 * =============================
		 */


		var syncTables =
			await _context.MetadataTables

			.Include(x =>
				x.Columns)

			.ThenInclude(x =>
				x.Semantic)

			.Where(x =>
				x.TenantId == effectiveTenantId
				&&
				x.DataSourceId == dataSourceId)

			.ToListAsync();







		foreach (var metadataTable in syncTables)
		{


			var needIndex =
				string.IsNullOrWhiteSpace(
					metadataTable.VectorId);




			if (!needIndex)
			{

				needIndex =
					metadataTable.Columns
					.Any(x =>
						string.IsNullOrWhiteSpace(
							x.VectorId)

						||

						x.Semantic != null
						&&
						string.IsNullOrWhiteSpace(
							x.Semantic.VectorId));

			}




			if (!needIndex)
			{
				continue;
			}







			var result =
				await _vectorService
				.IndexAsync(
					metadataTable);







			/*
			 * Table Vector
			 */


			if (!string.IsNullOrWhiteSpace(
				result.TableVectorId))
			{

				metadataTable.VectorId =
					result.TableVectorId;

			}







			/*
			 * Column / Semantic Vector
			 */


			foreach (var column in metadataTable.Columns)
			{


				if (result.ColumnVectors
					.TryGetValue(
						column.Id,
						out var columnVectorId))
				{

					column.VectorId =
						columnVectorId;

				}






				if (column.Semantic != null
					&&
					result.SemanticVectors
					.TryGetValue(
						column.Semantic.Id,
						out var semanticVectorId))
				{

					column.Semantic.VectorId =
						semanticVectorId;

				}


			}


		}







		/*
		 * =============================
		 *
		 * 8.
		 * 保存VectorId
		 *
		 * =============================
		 */


		await _context
			.SaveChangesAsync();

	}


}