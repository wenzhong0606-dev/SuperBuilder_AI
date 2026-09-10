BEGIN TRANSACTION;
DROP TABLE [AppPublishIdempotencies];

DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AppPlans]') AND [c].[name] = N'DraftRevision');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [AppPlans] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [AppPlans] DROP COLUMN [DraftRevision];

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20260909025001_M7_11_PublishIdempotency';

COMMIT;
GO

