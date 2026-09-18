using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SuperBuilder_AI.Infrastructure.Persistence.Migrations;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class MetadataVersionCounterRepairTests
{
	[Fact]
	public void RepairMigration_AdvancesCounterPastActiveJobsAndTables()
	{
		var migration = new MetadataVersionCounterRepair();
		var sql = Assert.Single(migration.UpOperations.OfType<SqlOperation>()).Sql;

		Assert.Contains("ActiveMetadataVersion", sql);
		Assert.Contains("MetadataScanJobs", sql);
		Assert.Contains("MetadataTables", sql);
		Assert.Contains("NextMetadataVersion", sql);
		Assert.Contains("HighestVersion + 1", sql);
	}
}
