using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SuperBuilder_AI.Data;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SuperBIContext))]
[Migration("20260918000100_MetadataVersionCounterRepair")]
public sealed class MetadataVersionCounterRepair : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("""
			UPDATE d
			SET NextMetadataVersion = CASE
			    WHEN d.NextMetadataVersion > versions.HighestVersion THEN d.NextMetadataVersion
			    ELSE versions.HighestVersion + 1
			END
			FROM DataSources AS d
			CROSS APPLY (
			    SELECT MAX(v.VersionNumber) AS HighestVersion
			    FROM (
			        SELECT d.ActiveMetadataVersion AS VersionNumber
			        UNION ALL
			        SELECT ISNULL(MAX(j.BatchVersion), 0)
			        FROM MetadataScanJobs AS j WHERE j.DataSourceId = d.Id
			        UNION ALL
			        SELECT ISNULL(MAX(t.MetadataVersion), 0)
			        FROM MetadataTables AS t WHERE t.DataSourceId = d.Id
			    ) AS v
			) AS versions
			WHERE d.NextMetadataVersion <= versions.HighestVersion;
			""");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		// A counter advance cannot be reversed without risking reuse of an allocated version.
	}
}
