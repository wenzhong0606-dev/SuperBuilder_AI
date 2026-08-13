using Microsoft.EntityFrameworkCore;
using SuperBulider_AI.Models.Metadata;
using SuperBulider_AI.Models.Organization;


namespace SuperBulider_AI.Data;

/// <summary>
/// SuperBI数据库上下文
///
/// 负责:
///
/// 1. 平台基础数据
/// 2. Metadata元数据
/// 3. AI语义数据
/// 4. 向量索引关系
///
/// </summary>
public class SuperBIContext
	: DbContext
{


	public SuperBIContext(
		DbContextOptions<SuperBIContext> options)
		: base(options)
	{

	}





	#region Organization


	public DbSet<Tenant> Tenants { get; set; }



	public DbSet<DataSource> DataSources { get; set; }


	#endregion





	#region Metadata


	public DbSet<MetadataTable> MetadataTables { get; set; }



	public DbSet<MetadataColumn> MetadataColumns { get; set; }



	public DbSet<MetadataSemantic> MetadataSemantics { get; set; }



	public DbSet<MetadataLearningRecord> LearningRecords { get; set; }


	#endregion






	protected override void OnModelCreating(
		ModelBuilder builder)
	{


		base.OnModelCreating(builder);





		/*
		 * 基础字段注释
		 */

		foreach (var entityType in builder.Model.GetEntityTypes())
		{

			var id =
				entityType.FindProperty(
					"Id");


			if (id != null)
			{
				id.SetComment("主键");
			}



			var createdTime =
				entityType.FindProperty(
					"CreatedTime");


			if (createdTime != null)
			{
				createdTime.SetComment("创建时间");
			}

		}






		#region Tenant



		builder.Entity<Tenant>()
			.HasIndex(x =>
				x.TenantCode)
			.IsUnique();



		builder.Entity<Tenant>()
			.ToTable(
				tb =>
					tb.HasComment("租户"));



		builder.Entity<Tenant>()
			.Property(x =>
				x.TenantCode)
			.HasComment("租户编码");


		builder.Entity<Tenant>()
			.Property(x =>
				x.TenantName)
			.HasComment("租户名称");


		builder.Entity<Tenant>()
			.Property(x =>
				x.Enabled)
			.HasComment("是否启用");



		#endregion







		#region DataSource



		builder.Entity<DataSource>()

			.HasOne(x =>
				x.Tenant)

			.WithMany(x =>
				x.DataSources)

			.HasForeignKey(x =>
				x.TenantId);



		builder.Entity<DataSource>()
			.ToTable(
				tb =>
					tb.HasComment("数据源"));



		builder.Entity<DataSource>()
			.Property(x =>
				x.ConnectionString)
			.HasComment("连接字符串");


		builder.Entity<DataSource>()
			.Property(x =>
				x.DbType)
			.HasComment("数据库类型");



		#endregion







		#region MetadataTable



		builder.Entity<MetadataTable>()

			.HasOne(x =>
				x.DataSource)

			.WithMany(x =>
				x.Tables)

			.HasForeignKey(x =>
				x.DataSourceId);



		builder.Entity<MetadataTable>()
			.ToTable(
				tb =>
					tb.HasComment("元数据表"));



		/*
		 * 同一个数据库中
		 * 表名唯一
		 *
		 * 不同数据库允许重复
		 *
		 */

		builder.Entity<MetadataTable>()
			.HasIndex(x =>
				new
				{
					x.DataSourceId,
					x.TableName
				})
			.IsUnique();




		builder.Entity<MetadataTable>()
			.Property(x =>
				x.SearchText)
			.HasComment(
				"Embedding文本");



		builder.Entity<MetadataTable>()
			.Property(x =>
				x.VectorId)
			.HasComment(
				"Qdrant向量ID");



		#endregion







		#region MetadataColumn



		builder.Entity<MetadataColumn>()


			.HasOne(x =>
				x.MetadataTable)

			.WithMany(x =>
				x.Columns)

			.HasForeignKey(x =>
				x.MetadataTableId);





		builder.Entity<MetadataColumn>()
			.ToTable(
				tb =>
					tb.HasComment("元数据字段"));






		/*
		 * 字段业务唯一键
		 *
		 * 解决:
		 *
		 * 多数据库:
		 *
		 * ERP.Customer.Name
		 *
		 * CRM.Customer.Name
		 *
		 * 两者不能混淆
		 *
		 */


		builder.Entity<MetadataColumn>()

			.HasIndex(x =>
				new
				{
					x.MetadataTableId,
					x.ColumnName
				})

			.IsUnique();







		builder.Entity<MetadataColumn>()
			.Property(x =>
				x.SearchText)
			.HasComment(
				"字段Embedding文本");



		builder.Entity<MetadataColumn>()
			.Property(x =>
				x.VectorId)
			.HasComment(
				"Qdrant字段向量ID");




		builder.Entity<MetadataColumn>()
			.Property(x =>
				x.BusinessKey)
			.HasComment(
				"字段业务唯一标识");



		#endregion







		#region MetadataSemantic



		/*
		 *
		 * 一个字段
		 *
		 * 对应一个语义
		 *
		 */


		builder.Entity<MetadataSemantic>()


			.HasOne(x =>
				x.MetadataColumn)

			.WithOne(x =>
				x.Semantic)

			.HasForeignKey<MetadataSemantic>(
				x =>
					x.MetadataColumnId)

			.OnDelete(
				DeleteBehavior.Cascade);







		builder.Entity<MetadataSemantic>()

			.HasIndex(x =>
				x.MetadataColumnId)

			.IsUnique();







		builder.Entity<MetadataSemantic>()

			.Property(x =>
				x.Confidence)

			.HasColumnType(
				"decimal(5,4)");






		builder.Entity<MetadataSemantic>()
			.ToTable(
				tb =>
					tb.HasComment(
						"字段AI语义"));





		builder.Entity<MetadataSemantic>()
			.Property(x =>
				x.BusinessMeaning)
			.HasComment(
				"业务含义");



		builder.Entity<MetadataSemantic>()
			.Property(x =>
				x.Keywords)
			.HasComment(
				"关键词");



		builder.Entity<MetadataSemantic>()
			.Property(x =>
				x.Synonyms)
			.HasComment(
				"同义词");



		builder.Entity<MetadataSemantic>()
			.Property(x =>
				x.ExampleQuestions)
			.HasComment(
				"示例问题");



		builder.Entity<MetadataSemantic>()
			.Property(x =>
				x.BusinessDomain)
			.HasComment(
				"业务域");



		builder.Entity<MetadataSemantic>()
			.Property(x =>
				x.Source)
			.HasComment(
				"来源");



		builder.Entity<MetadataSemantic>()
			.Property(x =>
				x.SearchText)
			.HasComment(
				"语义Embedding文本");



		builder.Entity<MetadataSemantic>()
			.Property(x =>
				x.VectorId)
			.HasComment(
				"Qdrant语义向量ID");



		#endregion







		#region Learning



		builder.Entity<MetadataLearningRecord>()
			.ToTable(
				tb =>
					tb.HasComment(
						"学习记录"));



		#endregion


	}

}