IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810081146_CreateDB'
)
BEGIN
    CREATE TABLE [LearningRecords] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NULL,
        [Question] nvarchar(max) NULL,
        [MetadataColumnId] bigint NULL,
        [Correct] bit NULL,
        [Feedback] nvarchar(max) NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_LearningRecords] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema AS sysname;
    SET @defaultSchema = SCHEMA_NAME();
    DECLARE @description AS sql_variant;
    SET @description = N'学习记录';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', @defaultSchema, 'TABLE', N'LearningRecords';
    SET @description = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', @defaultSchema, 'TABLE', N'LearningRecords', 'COLUMN', N'Id';
    SET @description = N'租户标识';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', @defaultSchema, 'TABLE', N'LearningRecords', 'COLUMN', N'TenantId';
    SET @description = N'问题内容';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', @defaultSchema, 'TABLE', N'LearningRecords', 'COLUMN', N'Question';
    SET @description = N'元数据列标识';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', @defaultSchema, 'TABLE', N'LearningRecords', 'COLUMN', N'MetadataColumnId';
    SET @description = N'回答是否正确';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', @defaultSchema, 'TABLE', N'LearningRecords', 'COLUMN', N'Correct';
    SET @description = N'反馈内容';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', @defaultSchema, 'TABLE', N'LearningRecords', 'COLUMN', N'Feedback';
    SET @description = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', @defaultSchema, 'TABLE', N'LearningRecords', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810081146_CreateDB'
)
BEGIN
    CREATE TABLE [Tenants] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantCode] nvarchar(450) NULL,
        [TenantName] nvarchar(max) NULL,
        [Enabled] bit NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema1 AS sysname;
    SET @defaultSchema1 = SCHEMA_NAME();
    DECLARE @description1 AS sql_variant;
    SET @description1 = N'租户';
    EXEC sp_addextendedproperty 'MS_Description', @description1, 'SCHEMA', @defaultSchema1, 'TABLE', N'Tenants';
    SET @description1 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description1, 'SCHEMA', @defaultSchema1, 'TABLE', N'Tenants', 'COLUMN', N'Id';
    SET @description1 = N'租户编码';
    EXEC sp_addextendedproperty 'MS_Description', @description1, 'SCHEMA', @defaultSchema1, 'TABLE', N'Tenants', 'COLUMN', N'TenantCode';
    SET @description1 = N'租户名称';
    EXEC sp_addextendedproperty 'MS_Description', @description1, 'SCHEMA', @defaultSchema1, 'TABLE', N'Tenants', 'COLUMN', N'TenantName';
    SET @description1 = N'是否启用';
    EXEC sp_addextendedproperty 'MS_Description', @description1, 'SCHEMA', @defaultSchema1, 'TABLE', N'Tenants', 'COLUMN', N'Enabled';
    SET @description1 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description1, 'SCHEMA', @defaultSchema1, 'TABLE', N'Tenants', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810081146_CreateDB'
)
BEGIN
    CREATE TABLE [DataSources] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NULL,
        [Name] nvarchar(max) NULL,
        [DbType] nvarchar(max) NULL,
        [ConnectionString] nvarchar(max) NULL,
        [Enabled] bit NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_DataSources] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DataSources_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id])
    );
    DECLARE @defaultSchema2 AS sysname;
    SET @defaultSchema2 = SCHEMA_NAME();
    DECLARE @description2 AS sql_variant;
    SET @description2 = N'数据源';
    EXEC sp_addextendedproperty 'MS_Description', @description2, 'SCHEMA', @defaultSchema2, 'TABLE', N'DataSources';
    SET @description2 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description2, 'SCHEMA', @defaultSchema2, 'TABLE', N'DataSources', 'COLUMN', N'Id';
    SET @description2 = N'租户标识';
    EXEC sp_addextendedproperty 'MS_Description', @description2, 'SCHEMA', @defaultSchema2, 'TABLE', N'DataSources', 'COLUMN', N'TenantId';
    SET @description2 = N'名称';
    EXEC sp_addextendedproperty 'MS_Description', @description2, 'SCHEMA', @defaultSchema2, 'TABLE', N'DataSources', 'COLUMN', N'Name';
    SET @description2 = N'数据库类型';
    EXEC sp_addextendedproperty 'MS_Description', @description2, 'SCHEMA', @defaultSchema2, 'TABLE', N'DataSources', 'COLUMN', N'DbType';
    SET @description2 = N'连接字符串';
    EXEC sp_addextendedproperty 'MS_Description', @description2, 'SCHEMA', @defaultSchema2, 'TABLE', N'DataSources', 'COLUMN', N'ConnectionString';
    SET @description2 = N'是否启用';
    EXEC sp_addextendedproperty 'MS_Description', @description2, 'SCHEMA', @defaultSchema2, 'TABLE', N'DataSources', 'COLUMN', N'Enabled';
    SET @description2 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description2, 'SCHEMA', @defaultSchema2, 'TABLE', N'DataSources', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810081146_CreateDB'
)
BEGIN
    CREATE TABLE [MetadataTables] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [DataSourceId] bigint NOT NULL,
        [TableName] nvarchar(max) NULL,
        [TableComment] nvarchar(max) NULL,
        [BusinessDomain] nvarchar(max) NULL,
        [SearchText] nvarchar(max) NULL,
        [VectorId] nvarchar(max) NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_MetadataTables] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MetadataTables_DataSources_DataSourceId] FOREIGN KEY ([DataSourceId]) REFERENCES [DataSources] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema3 AS sysname;
    SET @defaultSchema3 = SCHEMA_NAME();
    DECLARE @description3 AS sql_variant;
    SET @description3 = N'元数据表';
    EXEC sp_addextendedproperty 'MS_Description', @description3, 'SCHEMA', @defaultSchema3, 'TABLE', N'MetadataTables';
    SET @description3 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description3, 'SCHEMA', @defaultSchema3, 'TABLE', N'MetadataTables', 'COLUMN', N'Id';
    SET @description3 = N'租户标识';
    EXEC sp_addextendedproperty 'MS_Description', @description3, 'SCHEMA', @defaultSchema3, 'TABLE', N'MetadataTables', 'COLUMN', N'TenantId';
    SET @description3 = N'数据源标识';
    EXEC sp_addextendedproperty 'MS_Description', @description3, 'SCHEMA', @defaultSchema3, 'TABLE', N'MetadataTables', 'COLUMN', N'DataSourceId';
    SET @description3 = N'表名';
    EXEC sp_addextendedproperty 'MS_Description', @description3, 'SCHEMA', @defaultSchema3, 'TABLE', N'MetadataTables', 'COLUMN', N'TableName';
    SET @description3 = N'表注释';
    EXEC sp_addextendedproperty 'MS_Description', @description3, 'SCHEMA', @defaultSchema3, 'TABLE', N'MetadataTables', 'COLUMN', N'TableComment';
    SET @description3 = N'业务域';
    EXEC sp_addextendedproperty 'MS_Description', @description3, 'SCHEMA', @defaultSchema3, 'TABLE', N'MetadataTables', 'COLUMN', N'BusinessDomain';
    SET @description3 = N'搜索文本(Embedding)';
    EXEC sp_addextendedproperty 'MS_Description', @description3, 'SCHEMA', @defaultSchema3, 'TABLE', N'MetadataTables', 'COLUMN', N'SearchText';
    SET @description3 = N'向量存储ID';
    EXEC sp_addextendedproperty 'MS_Description', @description3, 'SCHEMA', @defaultSchema3, 'TABLE', N'MetadataTables', 'COLUMN', N'VectorId';
    SET @description3 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description3, 'SCHEMA', @defaultSchema3, 'TABLE', N'MetadataTables', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810081146_CreateDB'
)
BEGIN
    CREATE TABLE [MetadataColumns] (
        [Id] bigint NOT NULL IDENTITY,
        [MetadataTableId] bigint NULL,
        [ColumnName] nvarchar(max) NULL,
        [ColumnComment] nvarchar(max) NULL,
        [DataType] nvarchar(max) NULL,
        [Length] bigint NULL,
        [IsNullable] bit NULL,
        [IsPrimaryKey] bit NULL,
        [SearchText] nvarchar(max) NULL,
        [VectorId] nvarchar(max) NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_MetadataColumns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MetadataColumns_MetadataTables_MetadataTableId] FOREIGN KEY ([MetadataTableId]) REFERENCES [MetadataTables] ([Id])
    );
    DECLARE @defaultSchema4 AS sysname;
    SET @defaultSchema4 = SCHEMA_NAME();
    DECLARE @description4 AS sql_variant;
    SET @description4 = N'元数据列';
    EXEC sp_addextendedproperty 'MS_Description', @description4, 'SCHEMA', @defaultSchema4, 'TABLE', N'MetadataColumns';
    SET @description4 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description4, 'SCHEMA', @defaultSchema4, 'TABLE', N'MetadataColumns', 'COLUMN', N'Id';
    SET @description4 = N'元数据表标识';
    EXEC sp_addextendedproperty 'MS_Description', @description4, 'SCHEMA', @defaultSchema4, 'TABLE', N'MetadataColumns', 'COLUMN', N'MetadataTableId';
    SET @description4 = N'列名';
    EXEC sp_addextendedproperty 'MS_Description', @description4, 'SCHEMA', @defaultSchema4, 'TABLE', N'MetadataColumns', 'COLUMN', N'ColumnName';
    SET @description4 = N'列注释';
    EXEC sp_addextendedproperty 'MS_Description', @description4, 'SCHEMA', @defaultSchema4, 'TABLE', N'MetadataColumns', 'COLUMN', N'ColumnComment';
    SET @description4 = N'数据类型';
    EXEC sp_addextendedproperty 'MS_Description', @description4, 'SCHEMA', @defaultSchema4, 'TABLE', N'MetadataColumns', 'COLUMN', N'DataType';
    SET @description4 = N'长度/精度';
    EXEC sp_addextendedproperty 'MS_Description', @description4, 'SCHEMA', @defaultSchema4, 'TABLE', N'MetadataColumns', 'COLUMN', N'Length';
    SET @description4 = N'是否允许空值';
    EXEC sp_addextendedproperty 'MS_Description', @description4, 'SCHEMA', @defaultSchema4, 'TABLE', N'MetadataColumns', 'COLUMN', N'IsNullable';
    SET @description4 = N'是否主键';
    EXEC sp_addextendedproperty 'MS_Description', @description4, 'SCHEMA', @defaultSchema4, 'TABLE', N'MetadataColumns', 'COLUMN', N'IsPrimaryKey';
    SET @description4 = N'搜索文本(Embedding)';
    EXEC sp_addextendedproperty 'MS_Description', @description4, 'SCHEMA', @defaultSchema4, 'TABLE', N'MetadataColumns', 'COLUMN', N'SearchText';
    SET @description4 = N'向量存储ID';
    EXEC sp_addextendedproperty 'MS_Description', @description4, 'SCHEMA', @defaultSchema4, 'TABLE', N'MetadataColumns', 'COLUMN', N'VectorId';
    SET @description4 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description4, 'SCHEMA', @defaultSchema4, 'TABLE', N'MetadataColumns', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810081146_CreateDB'
)
BEGIN
    CREATE TABLE [MetadataSemantics] (
        [Id] bigint NOT NULL IDENTITY,
        [MetadataColumnId] bigint NULL,
        [BusinessMeaning] nvarchar(max) NULL,
        [Keywords] nvarchar(max) NULL,
        [Synonyms] nvarchar(max) NULL,
        [ExampleQuestions] nvarchar(max) NULL,
        [BusinessDomain] nvarchar(max) NULL,
        [Confidence] decimal(5,4) NULL,
        [Source] nvarchar(max) NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_MetadataSemantics] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MetadataSemantics_MetadataColumns_MetadataColumnId] FOREIGN KEY ([MetadataColumnId]) REFERENCES [MetadataColumns] ([Id])
    );
    DECLARE @defaultSchema5 AS sysname;
    SET @defaultSchema5 = SCHEMA_NAME();
    DECLARE @description5 AS sql_variant;
    SET @description5 = N'元数据语义';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', @defaultSchema5, 'TABLE', N'MetadataSemantics';
    SET @description5 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', @defaultSchema5, 'TABLE', N'MetadataSemantics', 'COLUMN', N'Id';
    SET @description5 = N'元数据列标识';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', @defaultSchema5, 'TABLE', N'MetadataSemantics', 'COLUMN', N'MetadataColumnId';
    SET @description5 = N'业务含义';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', @defaultSchema5, 'TABLE', N'MetadataSemantics', 'COLUMN', N'BusinessMeaning';
    SET @description5 = N'关键词(JSON数组)';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', @defaultSchema5, 'TABLE', N'MetadataSemantics', 'COLUMN', N'Keywords';
    SET @description5 = N'同义词';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', @defaultSchema5, 'TABLE', N'MetadataSemantics', 'COLUMN', N'Synonyms';
    SET @description5 = N'示例问题';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', @defaultSchema5, 'TABLE', N'MetadataSemantics', 'COLUMN', N'ExampleQuestions';
    SET @description5 = N'业务域';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', @defaultSchema5, 'TABLE', N'MetadataSemantics', 'COLUMN', N'BusinessDomain';
    SET @description5 = N'置信度';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', @defaultSchema5, 'TABLE', N'MetadataSemantics', 'COLUMN', N'Confidence';
    SET @description5 = N'来源';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', @defaultSchema5, 'TABLE', N'MetadataSemantics', 'COLUMN', N'Source';
    SET @description5 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', @defaultSchema5, 'TABLE', N'MetadataSemantics', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810081146_CreateDB'
)
BEGIN
    CREATE INDEX [IX_DataSources_TenantId] ON [DataSources] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810081146_CreateDB'
)
BEGIN
    CREATE INDEX [IX_MetadataColumns_MetadataTableId] ON [MetadataColumns] ([MetadataTableId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810081146_CreateDB'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MetadataSemantics_MetadataColumnId] ON [MetadataSemantics] ([MetadataColumnId]) WHERE [MetadataColumnId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810081146_CreateDB'
)
BEGIN
    CREATE INDEX [IX_MetadataTables_DataSourceId] ON [MetadataTables] ([DataSourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810081146_CreateDB'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Tenants_TenantCode] ON [Tenants] ([TenantCode]) WHERE [TenantCode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810081146_CreateDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260810081146_CreateDB', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811022011_AddMetadataVectorInfo'
)
BEGIN
    ALTER TABLE [MetadataTables] ADD [EmbeddingModel] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811022011_AddMetadataVectorInfo'
)
BEGIN
    ALTER TABLE [MetadataTables] ADD [VectorDimension] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811022011_AddMetadataVectorInfo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260811022011_AddMetadataVectorInfo', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811053412_AddMetadataSemanticVector'
)
BEGIN
    ALTER TABLE [MetadataSemantics] ADD [SearchText] nvarchar(max) NULL;
    DECLARE @defaultSchema6 AS sysname;
    SET @defaultSchema6 = SCHEMA_NAME();
    DECLARE @description6 AS sql_variant;
    SET @description6 = N'语义Embedding文本';
    EXEC sp_addextendedproperty 'MS_Description', @description6, 'SCHEMA', @defaultSchema6, 'TABLE', N'MetadataSemantics', 'COLUMN', N'SearchText';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811053412_AddMetadataSemanticVector'
)
BEGIN
    ALTER TABLE [MetadataSemantics] ADD [VectorId] nvarchar(max) NULL;
    DECLARE @defaultSchema7 AS sysname;
    SET @defaultSchema7 = SCHEMA_NAME();
    DECLARE @description7 AS sql_variant;
    SET @description7 = N'Qdrant向量ID';
    EXEC sp_addextendedproperty 'MS_Description', @description7, 'SCHEMA', @defaultSchema7, 'TABLE', N'MetadataSemantics', 'COLUMN', N'VectorId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811053412_AddMetadataSemanticVector'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260811053412_AddMetadataSemanticVector', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811060637_AddMetadataSemanticVector2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260811060637_AddMetadataSemanticVector2', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    ALTER TABLE [MetadataSemantics] DROP CONSTRAINT [FK_MetadataSemantics_MetadataColumns_MetadataColumnId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DROP INDEX [IX_MetadataTables_DataSourceId] ON [MetadataTables];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DROP INDEX [IX_MetadataColumns_MetadataTableId] ON [MetadataColumns];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema8 AS sysname;
    SET @defaultSchema8 = SCHEMA_NAME();
    DECLARE @description8 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema8, 'TABLE', N'MetadataSemantics';
    SET @description8 = N'字段AI语义';
    EXEC sp_addextendedproperty 'MS_Description', @description8, 'SCHEMA', @defaultSchema8, 'TABLE', N'MetadataSemantics';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema9 AS sysname;
    SET @defaultSchema9 = SCHEMA_NAME();
    DECLARE @description9 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema9, 'TABLE', N'MetadataColumns';
    SET @description9 = N'元数据字段';
    EXEC sp_addextendedproperty 'MS_Description', @description9, 'SCHEMA', @defaultSchema9, 'TABLE', N'MetadataColumns';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema10 AS sysname;
    SET @defaultSchema10 = SCHEMA_NAME();
    DECLARE @description10 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema10, 'TABLE', N'MetadataTables', 'COLUMN', N'VectorId';
    SET @description10 = N'Qdrant向量ID';
    EXEC sp_addextendedproperty 'MS_Description', @description10, 'SCHEMA', @defaultSchema10, 'TABLE', N'MetadataTables', 'COLUMN', N'VectorId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema11 AS sysname;
    SET @defaultSchema11 = SCHEMA_NAME();
    DECLARE @description11 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema11, 'TABLE', N'MetadataTables', 'COLUMN', N'TenantId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @var12 nvarchar(max);
    SELECT @var12 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MetadataTables]') AND [c].[name] = N'TableName');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [MetadataTables] DROP CONSTRAINT ' + @var12 + ';');
    ALTER TABLE [MetadataTables] ALTER COLUMN [TableName] nvarchar(450) NULL;
    DECLARE @defaultSchema13 AS sysname;
    SET @defaultSchema13 = SCHEMA_NAME();
    DECLARE @description13 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema13, 'TABLE', N'MetadataTables', 'COLUMN', N'TableName';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema14 AS sysname;
    SET @defaultSchema14 = SCHEMA_NAME();
    DECLARE @description14 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema14, 'TABLE', N'MetadataTables', 'COLUMN', N'TableComment';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema15 AS sysname;
    SET @defaultSchema15 = SCHEMA_NAME();
    DECLARE @description15 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema15, 'TABLE', N'MetadataTables', 'COLUMN', N'SearchText';
    SET @description15 = N'Embedding文本';
    EXEC sp_addextendedproperty 'MS_Description', @description15, 'SCHEMA', @defaultSchema15, 'TABLE', N'MetadataTables', 'COLUMN', N'SearchText';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema16 AS sysname;
    SET @defaultSchema16 = SCHEMA_NAME();
    DECLARE @description16 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema16, 'TABLE', N'MetadataTables', 'COLUMN', N'DataSourceId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema17 AS sysname;
    SET @defaultSchema17 = SCHEMA_NAME();
    DECLARE @description17 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema17, 'TABLE', N'MetadataTables', 'COLUMN', N'BusinessDomain';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema18 AS sysname;
    SET @defaultSchema18 = SCHEMA_NAME();
    DECLARE @description18 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema18, 'TABLE', N'MetadataSemantics', 'COLUMN', N'VectorId';
    SET @description18 = N'Qdrant语义向量ID';
    EXEC sp_addextendedproperty 'MS_Description', @description18, 'SCHEMA', @defaultSchema18, 'TABLE', N'MetadataSemantics', 'COLUMN', N'VectorId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema19 AS sysname;
    SET @defaultSchema19 = SCHEMA_NAME();
    DECLARE @description19 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema19, 'TABLE', N'MetadataSemantics', 'COLUMN', N'MetadataColumnId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema20 AS sysname;
    SET @defaultSchema20 = SCHEMA_NAME();
    DECLARE @description20 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema20, 'TABLE', N'MetadataSemantics', 'COLUMN', N'Keywords';
    SET @description20 = N'关键词';
    EXEC sp_addextendedproperty 'MS_Description', @description20, 'SCHEMA', @defaultSchema20, 'TABLE', N'MetadataSemantics', 'COLUMN', N'Keywords';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema21 AS sysname;
    SET @defaultSchema21 = SCHEMA_NAME();
    DECLARE @description21 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema21, 'TABLE', N'MetadataSemantics', 'COLUMN', N'Confidence';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    ALTER TABLE [MetadataSemantics] ADD [BusinessKey] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema22 AS sysname;
    SET @defaultSchema22 = SCHEMA_NAME();
    DECLARE @description22 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema22, 'TABLE', N'MetadataColumns', 'COLUMN', N'VectorId';
    SET @description22 = N'Qdrant字段向量ID';
    EXEC sp_addextendedproperty 'MS_Description', @description22, 'SCHEMA', @defaultSchema22, 'TABLE', N'MetadataColumns', 'COLUMN', N'VectorId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema23 AS sysname;
    SET @defaultSchema23 = SCHEMA_NAME();
    DECLARE @description23 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema23, 'TABLE', N'MetadataColumns', 'COLUMN', N'SearchText';
    SET @description23 = N'字段Embedding文本';
    EXEC sp_addextendedproperty 'MS_Description', @description23, 'SCHEMA', @defaultSchema23, 'TABLE', N'MetadataColumns', 'COLUMN', N'SearchText';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema24 AS sysname;
    SET @defaultSchema24 = SCHEMA_NAME();
    DECLARE @description24 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema24, 'TABLE', N'MetadataColumns', 'COLUMN', N'MetadataTableId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema25 AS sysname;
    SET @defaultSchema25 = SCHEMA_NAME();
    DECLARE @description25 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema25, 'TABLE', N'MetadataColumns', 'COLUMN', N'Length';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema26 AS sysname;
    SET @defaultSchema26 = SCHEMA_NAME();
    DECLARE @description26 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema26, 'TABLE', N'MetadataColumns', 'COLUMN', N'IsPrimaryKey';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema27 AS sysname;
    SET @defaultSchema27 = SCHEMA_NAME();
    DECLARE @description27 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema27, 'TABLE', N'MetadataColumns', 'COLUMN', N'IsNullable';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema28 AS sysname;
    SET @defaultSchema28 = SCHEMA_NAME();
    DECLARE @description28 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema28, 'TABLE', N'MetadataColumns', 'COLUMN', N'DataType';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @var29 nvarchar(max);
    SELECT @var29 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MetadataColumns]') AND [c].[name] = N'ColumnName');
    IF @var29 IS NOT NULL EXEC(N'ALTER TABLE [MetadataColumns] DROP CONSTRAINT ' + @var29 + ';');
    ALTER TABLE [MetadataColumns] ALTER COLUMN [ColumnName] nvarchar(450) NULL;
    DECLARE @defaultSchema30 AS sysname;
    SET @defaultSchema30 = SCHEMA_NAME();
    DECLARE @description30 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema30, 'TABLE', N'MetadataColumns', 'COLUMN', N'ColumnName';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema31 AS sysname;
    SET @defaultSchema31 = SCHEMA_NAME();
    DECLARE @description31 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema31, 'TABLE', N'MetadataColumns', 'COLUMN', N'ColumnComment';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    ALTER TABLE [MetadataColumns] ADD [BusinessKey] nvarchar(max) NULL;
    DECLARE @defaultSchema32 AS sysname;
    SET @defaultSchema32 = SCHEMA_NAME();
    DECLARE @description32 AS sql_variant;
    SET @description32 = N'字段业务唯一标识';
    EXEC sp_addextendedproperty 'MS_Description', @description32, 'SCHEMA', @defaultSchema32, 'TABLE', N'MetadataColumns', 'COLUMN', N'BusinessKey';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema33 AS sysname;
    SET @defaultSchema33 = SCHEMA_NAME();
    DECLARE @description33 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema33, 'TABLE', N'LearningRecords', 'COLUMN', N'TenantId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema34 AS sysname;
    SET @defaultSchema34 = SCHEMA_NAME();
    DECLARE @description34 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema34, 'TABLE', N'LearningRecords', 'COLUMN', N'Question';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema35 AS sysname;
    SET @defaultSchema35 = SCHEMA_NAME();
    DECLARE @description35 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema35, 'TABLE', N'LearningRecords', 'COLUMN', N'MetadataColumnId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema36 AS sysname;
    SET @defaultSchema36 = SCHEMA_NAME();
    DECLARE @description36 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema36, 'TABLE', N'LearningRecords', 'COLUMN', N'Feedback';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema37 AS sysname;
    SET @defaultSchema37 = SCHEMA_NAME();
    DECLARE @description37 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema37, 'TABLE', N'LearningRecords', 'COLUMN', N'Correct';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema38 AS sysname;
    SET @defaultSchema38 = SCHEMA_NAME();
    DECLARE @description38 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema38, 'TABLE', N'DataSources', 'COLUMN', N'TenantId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema39 AS sysname;
    SET @defaultSchema39 = SCHEMA_NAME();
    DECLARE @description39 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema39, 'TABLE', N'DataSources', 'COLUMN', N'Name';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    DECLARE @defaultSchema40 AS sysname;
    SET @defaultSchema40 = SCHEMA_NAME();
    DECLARE @description40 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema40, 'TABLE', N'DataSources', 'COLUMN', N'Enabled';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MetadataTables_DataSourceId_TableName] ON [MetadataTables] ([DataSourceId], [TableName]) WHERE [TableName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MetadataColumns_MetadataTableId_ColumnName] ON [MetadataColumns] ([MetadataTableId], [ColumnName]) WHERE [MetadataTableId] IS NOT NULL AND [ColumnName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    ALTER TABLE [MetadataSemantics] ADD CONSTRAINT [FK_MetadataSemantics_MetadataColumns_MetadataColumnId] FOREIGN KEY ([MetadataColumnId]) REFERENCES [MetadataColumns] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811075305_Phase145SemanticOptimization'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260811075305_Phase145SemanticOptimization', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811082322_Phase145SemanticOptimization2'
)
BEGIN
    EXEC sp_rename N'[MetadataSemantics].[BusinessKey]', N'EmbeddingModel', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811082322_Phase145SemanticOptimization2'
)
BEGIN
    ALTER TABLE [MetadataSemantics] ADD [VectorDimension] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811082322_Phase145SemanticOptimization2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260811082322_Phase145SemanticOptimization2', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE TABLE [BusinessDomains] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_BusinessDomains] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessDomains_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @defaultSchema41 AS sysname;
    SET @defaultSchema41 = SCHEMA_NAME();
    DECLARE @description41 AS sql_variant;
    SET @description41 = N'业务域';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', @defaultSchema41, 'TABLE', N'BusinessDomains';
    SET @description41 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', @defaultSchema41, 'TABLE', N'BusinessDomains', 'COLUMN', N'Id';
    SET @description41 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', @defaultSchema41, 'TABLE', N'BusinessDomains', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE TABLE [BusinessEntities] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [BusinessKey] nvarchar(200) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [DisplayName] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [BusinessDomain] nvarchar(max) NULL,
        [SemanticText] nvarchar(max) NULL,
        [Status] nvarchar(50) NOT NULL,
        [BusinessDomainId] bigint NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_BusinessEntities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessEntities_BusinessDomains_BusinessDomainId] FOREIGN KEY ([BusinessDomainId]) REFERENCES [BusinessDomains] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BusinessEntities_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @defaultSchema42 AS sysname;
    SET @defaultSchema42 = SCHEMA_NAME();
    DECLARE @description42 AS sql_variant;
    SET @description42 = N'业务实体';
    EXEC sp_addextendedproperty 'MS_Description', @description42, 'SCHEMA', @defaultSchema42, 'TABLE', N'BusinessEntities';
    SET @description42 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description42, 'SCHEMA', @defaultSchema42, 'TABLE', N'BusinessEntities', 'COLUMN', N'Id';
    SET @description42 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description42, 'SCHEMA', @defaultSchema42, 'TABLE', N'BusinessEntities', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE TABLE [BusinessEntityDimensions] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [BusinessDomainId] bigint NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_BusinessEntityDimensions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessEntityDimensions_BusinessDomains_BusinessDomainId] FOREIGN KEY ([BusinessDomainId]) REFERENCES [BusinessDomains] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema43 AS sysname;
    SET @defaultSchema43 = SCHEMA_NAME();
    DECLARE @description43 AS sql_variant;
    SET @description43 = N'业务实体维度';
    EXEC sp_addextendedproperty 'MS_Description', @description43, 'SCHEMA', @defaultSchema43, 'TABLE', N'BusinessEntityDimensions';
    SET @description43 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description43, 'SCHEMA', @defaultSchema43, 'TABLE', N'BusinessEntityDimensions', 'COLUMN', N'Id';
    SET @description43 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description43, 'SCHEMA', @defaultSchema43, 'TABLE', N'BusinessEntityDimensions', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE TABLE [BusinessEntityAttributes] (
        [Id] bigint NOT NULL IDENTITY,
        [BusinessEntityId] bigint NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [DisplayName] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [SemanticType] nvarchar(max) NULL,
        [IsNullable] bit NOT NULL,
        [IsIdentifier] bit NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_BusinessEntityAttributes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessEntityAttributes_BusinessEntities_BusinessEntityId] FOREIGN KEY ([BusinessEntityId]) REFERENCES [BusinessEntities] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema44 AS sysname;
    SET @defaultSchema44 = SCHEMA_NAME();
    DECLARE @description44 AS sql_variant;
    SET @description44 = N'业务实体属性';
    EXEC sp_addextendedproperty 'MS_Description', @description44, 'SCHEMA', @defaultSchema44, 'TABLE', N'BusinessEntityAttributes';
    SET @description44 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description44, 'SCHEMA', @defaultSchema44, 'TABLE', N'BusinessEntityAttributes', 'COLUMN', N'Id';
    SET @description44 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description44, 'SCHEMA', @defaultSchema44, 'TABLE', N'BusinessEntityAttributes', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE TABLE [BusinessEntityKeys] (
        [Id] bigint NOT NULL IDENTITY,
        [BusinessEntityId] bigint NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [DisplayName] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [IsPrimary] bit NOT NULL,
        [KeyType] nvarchar(max) NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_BusinessEntityKeys] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessEntityKeys_BusinessEntities_BusinessEntityId] FOREIGN KEY ([BusinessEntityId]) REFERENCES [BusinessEntities] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema45 AS sysname;
    SET @defaultSchema45 = SCHEMA_NAME();
    DECLARE @description45 AS sql_variant;
    SET @description45 = N'业务实体键';
    EXEC sp_addextendedproperty 'MS_Description', @description45, 'SCHEMA', @defaultSchema45, 'TABLE', N'BusinessEntityKeys';
    SET @description45 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description45, 'SCHEMA', @defaultSchema45, 'TABLE', N'BusinessEntityKeys', 'COLUMN', N'Id';
    SET @description45 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description45, 'SCHEMA', @defaultSchema45, 'TABLE', N'BusinessEntityKeys', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE TABLE [BusinessEntityMetrics] (
        [Id] bigint NOT NULL IDENTITY,
        [BusinessEntityId] bigint NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [DisplayName] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [SemanticType] nvarchar(max) NULL,
        [Aggregation] nvarchar(50) NULL,
        [IsCalculated] bit NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_BusinessEntityMetrics] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessEntityMetrics_BusinessEntities_BusinessEntityId] FOREIGN KEY ([BusinessEntityId]) REFERENCES [BusinessEntities] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema46 AS sysname;
    SET @defaultSchema46 = SCHEMA_NAME();
    DECLARE @description46 AS sql_variant;
    SET @description46 = N'业务实体指标';
    EXEC sp_addextendedproperty 'MS_Description', @description46, 'SCHEMA', @defaultSchema46, 'TABLE', N'BusinessEntityMetrics';
    SET @description46 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description46, 'SCHEMA', @defaultSchema46, 'TABLE', N'BusinessEntityMetrics', 'COLUMN', N'Id';
    SET @description46 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description46, 'SCHEMA', @defaultSchema46, 'TABLE', N'BusinessEntityMetrics', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE TABLE [BusinessEntityRelationships] (
        [Id] bigint NOT NULL IDENTITY,
        [SourceEntityId] bigint NOT NULL,
        [TargetEntityId] bigint NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [DisplayName] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [RelationshipType] nvarchar(max) NULL,
        [Cardinality] nvarchar(max) NULL,
        [IsRequired] bit NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_BusinessEntityRelationships] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessEntityRelationships_BusinessEntities_SourceEntityId] FOREIGN KEY ([SourceEntityId]) REFERENCES [BusinessEntities] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BusinessEntityRelationships_BusinessEntities_TargetEntityId] FOREIGN KEY ([TargetEntityId]) REFERENCES [BusinessEntities] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @defaultSchema47 AS sysname;
    SET @defaultSchema47 = SCHEMA_NAME();
    DECLARE @description47 AS sql_variant;
    SET @description47 = N'业务实体关系';
    EXEC sp_addextendedproperty 'MS_Description', @description47, 'SCHEMA', @defaultSchema47, 'TABLE', N'BusinessEntityRelationships';
    SET @description47 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description47, 'SCHEMA', @defaultSchema47, 'TABLE', N'BusinessEntityRelationships', 'COLUMN', N'Id';
    SET @description47 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description47, 'SCHEMA', @defaultSchema47, 'TABLE', N'BusinessEntityRelationships', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE TABLE [PhysicalBindings] (
        [Id] bigint NOT NULL IDENTITY,
        [DataSourceId] bigint NOT NULL,
        [MetadataTableId] bigint NOT NULL,
        [MetadataColumnId] bigint NOT NULL,
        [PhysicalRole] nvarchar(50) NULL,
        [BindingType] nvarchar(50) NULL,
        [Priority] int NOT NULL,
        [IsActive] bit NOT NULL,
        [BusinessEntityKeyId] bigint NULL,
        [BusinessEntityAttributeId] bigint NULL,
        [BusinessEntityMetricId] bigint NULL,
        [BusinessEntityRelationshipId] bigint NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_PhysicalBindings] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PhysicalBindings_ExactlyOneOwner] CHECK (((CASE WHEN BusinessEntityKeyId IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN BusinessEntityAttributeId IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN BusinessEntityMetricId IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN BusinessEntityRelationshipId IS NOT NULL THEN 1 ELSE 0 END)) = 1),
        CONSTRAINT [FK_PhysicalBindings_BusinessEntityAttributes_BusinessEntityAttributeId] FOREIGN KEY ([BusinessEntityAttributeId]) REFERENCES [BusinessEntityAttributes] ([Id]),
        CONSTRAINT [FK_PhysicalBindings_BusinessEntityKeys_BusinessEntityKeyId] FOREIGN KEY ([BusinessEntityKeyId]) REFERENCES [BusinessEntityKeys] ([Id]),
        CONSTRAINT [FK_PhysicalBindings_BusinessEntityMetrics_BusinessEntityMetricId] FOREIGN KEY ([BusinessEntityMetricId]) REFERENCES [BusinessEntityMetrics] ([Id]),
        CONSTRAINT [FK_PhysicalBindings_BusinessEntityRelationships_BusinessEntityRelationshipId] FOREIGN KEY ([BusinessEntityRelationshipId]) REFERENCES [BusinessEntityRelationships] ([Id]),
        CONSTRAINT [FK_PhysicalBindings_DataSources_DataSourceId] FOREIGN KEY ([DataSourceId]) REFERENCES [DataSources] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PhysicalBindings_MetadataColumns_MetadataColumnId] FOREIGN KEY ([MetadataColumnId]) REFERENCES [MetadataColumns] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PhysicalBindings_MetadataTables_MetadataTableId] FOREIGN KEY ([MetadataTableId]) REFERENCES [MetadataTables] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @defaultSchema48 AS sysname;
    SET @defaultSchema48 = SCHEMA_NAME();
    DECLARE @description48 AS sql_variant;
    SET @description48 = N'业务语义到物理元数据的映射';
    EXEC sp_addextendedproperty 'MS_Description', @description48, 'SCHEMA', @defaultSchema48, 'TABLE', N'PhysicalBindings';
    SET @description48 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description48, 'SCHEMA', @defaultSchema48, 'TABLE', N'PhysicalBindings', 'COLUMN', N'Id';
    SET @description48 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description48, 'SCHEMA', @defaultSchema48, 'TABLE', N'PhysicalBindings', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessDomains_TenantId_Name] ON [BusinessDomains] ([TenantId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE INDEX [IX_BusinessEntities_BusinessDomainId] ON [BusinessEntities] ([BusinessDomainId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessEntities_TenantId_BusinessKey] ON [BusinessEntities] ([TenantId], [BusinessKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE INDEX [IX_BusinessEntityAttributes_BusinessEntityId_IsIdentifier] ON [BusinessEntityAttributes] ([BusinessEntityId], [IsIdentifier]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessEntityAttributes_BusinessEntityId_Name] ON [BusinessEntityAttributes] ([BusinessEntityId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE INDEX [IX_BusinessEntityDimensions_BusinessDomainId] ON [BusinessEntityDimensions] ([BusinessDomainId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessEntityDimensions_TenantId_BusinessDomainId_Name] ON [BusinessEntityDimensions] ([TenantId], [BusinessDomainId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE INDEX [IX_BusinessEntityKeys_BusinessEntityId_IsPrimary] ON [BusinessEntityKeys] ([BusinessEntityId], [IsPrimary]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessEntityKeys_BusinessEntityId_Name] ON [BusinessEntityKeys] ([BusinessEntityId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessEntityMetrics_BusinessEntityId_Name] ON [BusinessEntityMetrics] ([BusinessEntityId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessEntityRelationships_SourceEntityId_TargetEntityId_Name] ON [BusinessEntityRelationships] ([SourceEntityId], [TargetEntityId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE INDEX [IX_BusinessEntityRelationships_TargetEntityId] ON [BusinessEntityRelationships] ([TargetEntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE INDEX [IX_PhysicalBindings_BusinessEntityAttributeId_IsActive] ON [PhysicalBindings] ([BusinessEntityAttributeId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE INDEX [IX_PhysicalBindings_BusinessEntityKeyId_IsActive] ON [PhysicalBindings] ([BusinessEntityKeyId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE INDEX [IX_PhysicalBindings_BusinessEntityMetricId_IsActive] ON [PhysicalBindings] ([BusinessEntityMetricId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE INDEX [IX_PhysicalBindings_BusinessEntityRelationshipId_PhysicalRole_IsActive] ON [PhysicalBindings] ([BusinessEntityRelationshipId], [PhysicalRole], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE INDEX [IX_PhysicalBindings_DataSourceId_MetadataTableId_MetadataColumnId_Priority] ON [PhysicalBindings] ([DataSourceId], [MetadataTableId], [MetadataColumnId], [Priority]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE INDEX [IX_PhysicalBindings_MetadataColumnId] ON [PhysicalBindings] ([MetadataColumnId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    CREATE INDEX [IX_PhysicalBindings_MetadataTableId] ON [PhysicalBindings] ([MetadataTableId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828144504_P3BusinessEntityModelWithDomains'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260828144504_P3BusinessEntityModelWithDomains', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829090341_P4_2_TenantSettings'
)
BEGIN
    CREATE TABLE [TenantSettings] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Key] nvarchar(128) NOT NULL,
        [Value] nvarchar(max) NULL,
        [DataType] nvarchar(32) NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_TenantSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantSettings_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema49 AS sysname;
    SET @defaultSchema49 = SCHEMA_NAME();
    DECLARE @description49 AS sql_variant;
    SET @description49 = N'租户键值配置';
    EXEC sp_addextendedproperty 'MS_Description', @description49, 'SCHEMA', @defaultSchema49, 'TABLE', N'TenantSettings';
    SET @description49 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description49, 'SCHEMA', @defaultSchema49, 'TABLE', N'TenantSettings', 'COLUMN', N'Id';
    SET @description49 = N'配置键';
    EXEC sp_addextendedproperty 'MS_Description', @description49, 'SCHEMA', @defaultSchema49, 'TABLE', N'TenantSettings', 'COLUMN', N'Key';
    SET @description49 = N'配置值';
    EXEC sp_addextendedproperty 'MS_Description', @description49, 'SCHEMA', @defaultSchema49, 'TABLE', N'TenantSettings', 'COLUMN', N'Value';
    SET @description49 = N'值类型';
    EXEC sp_addextendedproperty 'MS_Description', @description49, 'SCHEMA', @defaultSchema49, 'TABLE', N'TenantSettings', 'COLUMN', N'DataType';
    SET @description49 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description49, 'SCHEMA', @defaultSchema49, 'TABLE', N'TenantSettings', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829090341_P4_2_TenantSettings'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TenantSettings_TenantId_Key] ON [TenantSettings] ([TenantId], [Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829090341_P4_2_TenantSettings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260829090341_P4_2_TenantSettings', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829154339_P5_2_SemanticLabels'
)
BEGIN
    CREATE TABLE [SemanticLabels] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [ConceptType] nvarchar(64) NOT NULL,
        [ConceptId] bigint NOT NULL,
        [Culture] nvarchar(16) NOT NULL,
        [LabelKind] nvarchar(32) NOT NULL,
        [Value] nvarchar(512) NOT NULL,
        [Source] nvarchar(32) NULL,
        [SortOrder] int NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_SemanticLabels] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema50 AS sysname;
    SET @defaultSchema50 = SCHEMA_NAME();
    DECLARE @description50 AS sql_variant;
    SET @description50 = N'语义多语言标签';
    EXEC sp_addextendedproperty 'MS_Description', @description50, 'SCHEMA', @defaultSchema50, 'TABLE', N'SemanticLabels';
    SET @description50 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description50, 'SCHEMA', @defaultSchema50, 'TABLE', N'SemanticLabels', 'COLUMN', N'Id';
    SET @description50 = N'所属租户（0=全局共享）';
    EXEC sp_addextendedproperty 'MS_Description', @description50, 'SCHEMA', @defaultSchema50, 'TABLE', N'SemanticLabels', 'COLUMN', N'TenantId';
    SET @description50 = N'概念类型';
    EXEC sp_addextendedproperty 'MS_Description', @description50, 'SCHEMA', @defaultSchema50, 'TABLE', N'SemanticLabels', 'COLUMN', N'ConceptType';
    SET @description50 = N'概念实体Id';
    EXEC sp_addextendedproperty 'MS_Description', @description50, 'SCHEMA', @defaultSchema50, 'TABLE', N'SemanticLabels', 'COLUMN', N'ConceptId';
    SET @description50 = N'语言标签';
    EXEC sp_addextendedproperty 'MS_Description', @description50, 'SCHEMA', @defaultSchema50, 'TABLE', N'SemanticLabels', 'COLUMN', N'Culture';
    SET @description50 = N'标签种类';
    EXEC sp_addextendedproperty 'MS_Description', @description50, 'SCHEMA', @defaultSchema50, 'TABLE', N'SemanticLabels', 'COLUMN', N'LabelKind';
    SET @description50 = N'标签文本';
    EXEC sp_addextendedproperty 'MS_Description', @description50, 'SCHEMA', @defaultSchema50, 'TABLE', N'SemanticLabels', 'COLUMN', N'Value';
    SET @description50 = N'来源';
    EXEC sp_addextendedproperty 'MS_Description', @description50, 'SCHEMA', @defaultSchema50, 'TABLE', N'SemanticLabels', 'COLUMN', N'Source';
    SET @description50 = N'排序';
    EXEC sp_addextendedproperty 'MS_Description', @description50, 'SCHEMA', @defaultSchema50, 'TABLE', N'SemanticLabels', 'COLUMN', N'SortOrder';
    SET @description50 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description50, 'SCHEMA', @defaultSchema50, 'TABLE', N'SemanticLabels', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829154339_P5_2_SemanticLabels'
)
BEGIN
    CREATE INDEX [IX_SemanticLabels_ConceptType_ConceptId_Culture] ON [SemanticLabels] ([ConceptType], [ConceptId], [Culture]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829154339_P5_2_SemanticLabels'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SemanticLabels_TenantId_ConceptType_ConceptId_Culture_LabelKind_SortOrder] ON [SemanticLabels] ([TenantId], [ConceptType], [ConceptId], [Culture], [LabelKind], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829154339_P5_2_SemanticLabels'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260829154339_P5_2_SemanticLabels', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830012408_P6_1_Dashboard'
)
BEGIN
    CREATE TABLE [Dashboards] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Code] nvarchar(128) NOT NULL,
        [Title] nvarchar(256) NOT NULL,
        [Description] nvarchar(1024) NULL,
        [Status] nvarchar(32) NOT NULL,
        [DslVersion] nvarchar(16) NOT NULL,
        [DslJson] nvarchar(max) NOT NULL,
        [ThemeKey] nvarchar(64) NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_Dashboards] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema51 AS sysname;
    SET @defaultSchema51 = SCHEMA_NAME();
    DECLARE @description51 AS sql_variant;
    SET @description51 = N'仪表盘';
    EXEC sp_addextendedproperty 'MS_Description', @description51, 'SCHEMA', @defaultSchema51, 'TABLE', N'Dashboards';
    SET @description51 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description51, 'SCHEMA', @defaultSchema51, 'TABLE', N'Dashboards', 'COLUMN', N'Id';
    SET @description51 = N'所属租户（0=全局模板）';
    EXEC sp_addextendedproperty 'MS_Description', @description51, 'SCHEMA', @defaultSchema51, 'TABLE', N'Dashboards', 'COLUMN', N'TenantId';
    SET @description51 = N'业务编码';
    EXEC sp_addextendedproperty 'MS_Description', @description51, 'SCHEMA', @defaultSchema51, 'TABLE', N'Dashboards', 'COLUMN', N'Code';
    SET @description51 = N'标题';
    EXEC sp_addextendedproperty 'MS_Description', @description51, 'SCHEMA', @defaultSchema51, 'TABLE', N'Dashboards', 'COLUMN', N'Title';
    SET @description51 = N'描述';
    EXEC sp_addextendedproperty 'MS_Description', @description51, 'SCHEMA', @defaultSchema51, 'TABLE', N'Dashboards', 'COLUMN', N'Description';
    SET @description51 = N'状态';
    EXEC sp_addextendedproperty 'MS_Description', @description51, 'SCHEMA', @defaultSchema51, 'TABLE', N'Dashboards', 'COLUMN', N'Status';
    SET @description51 = N'DSL版本';
    EXEC sp_addextendedproperty 'MS_Description', @description51, 'SCHEMA', @defaultSchema51, 'TABLE', N'Dashboards', 'COLUMN', N'DslVersion';
    SET @description51 = N'DSL文档（结构化，非裸HTML）';
    EXEC sp_addextendedproperty 'MS_Description', @description51, 'SCHEMA', @defaultSchema51, 'TABLE', N'Dashboards', 'COLUMN', N'DslJson';
    SET @description51 = N'主题键';
    EXEC sp_addextendedproperty 'MS_Description', @description51, 'SCHEMA', @defaultSchema51, 'TABLE', N'Dashboards', 'COLUMN', N'ThemeKey';
    SET @description51 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description51, 'SCHEMA', @defaultSchema51, 'TABLE', N'Dashboards', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830012408_P6_1_Dashboard'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Dashboards_TenantId_Code] ON [Dashboards] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830012408_P6_1_Dashboard'
)
BEGIN
    CREATE INDEX [IX_Dashboards_TenantId_Status] ON [Dashboards] ([TenantId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830012408_P6_1_Dashboard'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260830012408_P6_1_Dashboard', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830034528_P7_1_Theme'
)
BEGIN
    CREATE TABLE [Themes] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Key] nvarchar(64) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [IsBuiltIn] bit NOT NULL,
        [DslVersion] nvarchar(16) NOT NULL,
        [DslJson] nvarchar(max) NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_Themes] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema52 AS sysname;
    SET @defaultSchema52 = SCHEMA_NAME();
    DECLARE @description52 AS sql_variant;
    SET @description52 = N'主题';
    EXEC sp_addextendedproperty 'MS_Description', @description52, 'SCHEMA', @defaultSchema52, 'TABLE', N'Themes';
    SET @description52 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description52, 'SCHEMA', @defaultSchema52, 'TABLE', N'Themes', 'COLUMN', N'Id';
    SET @description52 = N'所属租户（0=内置/全局模板）';
    EXEC sp_addextendedproperty 'MS_Description', @description52, 'SCHEMA', @defaultSchema52, 'TABLE', N'Themes', 'COLUMN', N'TenantId';
    SET @description52 = N'主题键（同租户内唯一）';
    EXEC sp_addextendedproperty 'MS_Description', @description52, 'SCHEMA', @defaultSchema52, 'TABLE', N'Themes', 'COLUMN', N'Key';
    SET @description52 = N'主题名称';
    EXEC sp_addextendedproperty 'MS_Description', @description52, 'SCHEMA', @defaultSchema52, 'TABLE', N'Themes', 'COLUMN', N'Name';
    SET @description52 = N'是否内置主题';
    EXEC sp_addextendedproperty 'MS_Description', @description52, 'SCHEMA', @defaultSchema52, 'TABLE', N'Themes', 'COLUMN', N'IsBuiltIn';
    SET @description52 = N'DSL版本';
    EXEC sp_addextendedproperty 'MS_Description', @description52, 'SCHEMA', @defaultSchema52, 'TABLE', N'Themes', 'COLUMN', N'DslVersion';
    SET @description52 = N'主题DSL文档（结构化令牌，非CSS/HTML）';
    EXEC sp_addextendedproperty 'MS_Description', @description52, 'SCHEMA', @defaultSchema52, 'TABLE', N'Themes', 'COLUMN', N'DslJson';
    SET @description52 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description52, 'SCHEMA', @defaultSchema52, 'TABLE', N'Themes', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830034528_P7_1_Theme'
)
BEGIN
    CREATE INDEX [IX_Themes_TenantId] ON [Themes] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830034528_P7_1_Theme'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Themes_TenantId_Key] ON [Themes] ([TenantId], [Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830034528_P7_1_Theme'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260830034528_P7_1_Theme', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830053330_P8_1_AppPlan'
)
BEGIN
    CREATE TABLE [AppPlans] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Code] nvarchar(128) NOT NULL,
        [Name] nvarchar(256) NOT NULL,
        [Description] nvarchar(1024) NULL,
        [Status] nvarchar(32) NOT NULL,
        [DslVersion] nvarchar(16) NOT NULL,
        [DslJson] nvarchar(max) NOT NULL,
        [ThemeKey] nvarchar(64) NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_AppPlans] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema53 AS sysname;
    SET @defaultSchema53 = SCHEMA_NAME();
    DECLARE @description53 AS sql_variant;
    SET @description53 = N'应用';
    EXEC sp_addextendedproperty 'MS_Description', @description53, 'SCHEMA', @defaultSchema53, 'TABLE', N'AppPlans';
    SET @description53 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description53, 'SCHEMA', @defaultSchema53, 'TABLE', N'AppPlans', 'COLUMN', N'Id';
    SET @description53 = N'所属租户（0=全局模板）';
    EXEC sp_addextendedproperty 'MS_Description', @description53, 'SCHEMA', @defaultSchema53, 'TABLE', N'AppPlans', 'COLUMN', N'TenantId';
    SET @description53 = N'业务编码';
    EXEC sp_addextendedproperty 'MS_Description', @description53, 'SCHEMA', @defaultSchema53, 'TABLE', N'AppPlans', 'COLUMN', N'Code';
    SET @description53 = N'名称';
    EXEC sp_addextendedproperty 'MS_Description', @description53, 'SCHEMA', @defaultSchema53, 'TABLE', N'AppPlans', 'COLUMN', N'Name';
    SET @description53 = N'描述';
    EXEC sp_addextendedproperty 'MS_Description', @description53, 'SCHEMA', @defaultSchema53, 'TABLE', N'AppPlans', 'COLUMN', N'Description';
    SET @description53 = N'状态';
    EXEC sp_addextendedproperty 'MS_Description', @description53, 'SCHEMA', @defaultSchema53, 'TABLE', N'AppPlans', 'COLUMN', N'Status';
    SET @description53 = N'DSL版本';
    EXEC sp_addextendedproperty 'MS_Description', @description53, 'SCHEMA', @defaultSchema53, 'TABLE', N'AppPlans', 'COLUMN', N'DslVersion';
    SET @description53 = N'DSL文档（结构化，非裸HTML）';
    EXEC sp_addextendedproperty 'MS_Description', @description53, 'SCHEMA', @defaultSchema53, 'TABLE', N'AppPlans', 'COLUMN', N'DslJson';
    SET @description53 = N'主题键';
    EXEC sp_addextendedproperty 'MS_Description', @description53, 'SCHEMA', @defaultSchema53, 'TABLE', N'AppPlans', 'COLUMN', N'ThemeKey';
    SET @description53 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description53, 'SCHEMA', @defaultSchema53, 'TABLE', N'AppPlans', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830053330_P8_1_AppPlan'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AppPlans_TenantId_Code] ON [AppPlans] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830053330_P8_1_AppPlan'
)
BEGIN
    CREATE INDEX [IX_AppPlans_TenantId_Status] ON [AppPlans] ([TenantId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830053330_P8_1_AppPlan'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260830053330_P8_1_AppPlan', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830073012_P9_1_AgentPlan'
)
BEGIN
    CREATE TABLE [AgentPlans] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Code] nvarchar(128) NOT NULL,
        [Name] nvarchar(256) NOT NULL,
        [Description] nvarchar(1024) NULL,
        [Status] nvarchar(32) NOT NULL,
        [DslVersion] nvarchar(16) NOT NULL,
        [DslJson] nvarchar(max) NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_AgentPlans] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema54 AS sysname;
    SET @defaultSchema54 = SCHEMA_NAME();
    DECLARE @description54 AS sql_variant;
    SET @description54 = N'Agent计划';
    EXEC sp_addextendedproperty 'MS_Description', @description54, 'SCHEMA', @defaultSchema54, 'TABLE', N'AgentPlans';
    SET @description54 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description54, 'SCHEMA', @defaultSchema54, 'TABLE', N'AgentPlans', 'COLUMN', N'Id';
    SET @description54 = N'所属租户（0=全局模板）';
    EXEC sp_addextendedproperty 'MS_Description', @description54, 'SCHEMA', @defaultSchema54, 'TABLE', N'AgentPlans', 'COLUMN', N'TenantId';
    SET @description54 = N'业务编码';
    EXEC sp_addextendedproperty 'MS_Description', @description54, 'SCHEMA', @defaultSchema54, 'TABLE', N'AgentPlans', 'COLUMN', N'Code';
    SET @description54 = N'名称';
    EXEC sp_addextendedproperty 'MS_Description', @description54, 'SCHEMA', @defaultSchema54, 'TABLE', N'AgentPlans', 'COLUMN', N'Name';
    SET @description54 = N'描述';
    EXEC sp_addextendedproperty 'MS_Description', @description54, 'SCHEMA', @defaultSchema54, 'TABLE', N'AgentPlans', 'COLUMN', N'Description';
    SET @description54 = N'状态';
    EXEC sp_addextendedproperty 'MS_Description', @description54, 'SCHEMA', @defaultSchema54, 'TABLE', N'AgentPlans', 'COLUMN', N'Status';
    SET @description54 = N'DSL版本';
    EXEC sp_addextendedproperty 'MS_Description', @description54, 'SCHEMA', @defaultSchema54, 'TABLE', N'AgentPlans', 'COLUMN', N'DslVersion';
    SET @description54 = N'DSL文档（结构化，非裸HTML）';
    EXEC sp_addextendedproperty 'MS_Description', @description54, 'SCHEMA', @defaultSchema54, 'TABLE', N'AgentPlans', 'COLUMN', N'DslJson';
    SET @description54 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description54, 'SCHEMA', @defaultSchema54, 'TABLE', N'AgentPlans', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830073012_P9_1_AgentPlan'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AgentPlans_TenantId_Code] ON [AgentPlans] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830073012_P9_1_AgentPlan'
)
BEGIN
    CREATE INDEX [IX_AgentPlans_TenantId_Status] ON [AgentPlans] ([TenantId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830073012_P9_1_AgentPlan'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260830073012_P9_1_AgentPlan', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830093125_P10_1_Identity'
)
BEGIN
    CREATE TABLE [Permissions] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [Category] nvarchar(32) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema55 AS sysname;
    SET @defaultSchema55 = SCHEMA_NAME();
    DECLARE @description55 AS sql_variant;
    SET @description55 = N'权限';
    EXEC sp_addextendedproperty 'MS_Description', @description55, 'SCHEMA', @defaultSchema55, 'TABLE', N'Permissions';
    SET @description55 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description55, 'SCHEMA', @defaultSchema55, 'TABLE', N'Permissions', 'COLUMN', N'Id';
    SET @description55 = N'所属租户（0=全局权限）';
    EXEC sp_addextendedproperty 'MS_Description', @description55, 'SCHEMA', @defaultSchema55, 'TABLE', N'Permissions', 'COLUMN', N'TenantId';
    SET @description55 = N'权限码（同租户唯一）';
    EXEC sp_addextendedproperty 'MS_Description', @description55, 'SCHEMA', @defaultSchema55, 'TABLE', N'Permissions', 'COLUMN', N'Code';
    SET @description55 = N'权限名';
    EXEC sp_addextendedproperty 'MS_Description', @description55, 'SCHEMA', @defaultSchema55, 'TABLE', N'Permissions', 'COLUMN', N'Name';
    SET @description55 = N'权限分类';
    EXEC sp_addextendedproperty 'MS_Description', @description55, 'SCHEMA', @defaultSchema55, 'TABLE', N'Permissions', 'COLUMN', N'Category';
    SET @description55 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description55, 'SCHEMA', @defaultSchema55, 'TABLE', N'Permissions', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830093125_P10_1_Identity'
)
BEGIN
    CREATE TABLE [RolePermissions] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [RoleId] bigint NOT NULL,
        [PermissionId] bigint NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema56 AS sysname;
    SET @defaultSchema56 = SCHEMA_NAME();
    DECLARE @description56 AS sql_variant;
    SET @description56 = N'角色-权限关联';
    EXEC sp_addextendedproperty 'MS_Description', @description56, 'SCHEMA', @defaultSchema56, 'TABLE', N'RolePermissions';
    SET @description56 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description56, 'SCHEMA', @defaultSchema56, 'TABLE', N'RolePermissions', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830093125_P10_1_Identity'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema57 AS sysname;
    SET @defaultSchema57 = SCHEMA_NAME();
    DECLARE @description57 AS sql_variant;
    SET @description57 = N'角色';
    EXEC sp_addextendedproperty 'MS_Description', @description57, 'SCHEMA', @defaultSchema57, 'TABLE', N'Roles';
    SET @description57 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description57, 'SCHEMA', @defaultSchema57, 'TABLE', N'Roles', 'COLUMN', N'Id';
    SET @description57 = N'所属租户（0=全局角色）';
    EXEC sp_addextendedproperty 'MS_Description', @description57, 'SCHEMA', @defaultSchema57, 'TABLE', N'Roles', 'COLUMN', N'TenantId';
    SET @description57 = N'角色码（同租户唯一）';
    EXEC sp_addextendedproperty 'MS_Description', @description57, 'SCHEMA', @defaultSchema57, 'TABLE', N'Roles', 'COLUMN', N'Code';
    SET @description57 = N'角色名';
    EXEC sp_addextendedproperty 'MS_Description', @description57, 'SCHEMA', @defaultSchema57, 'TABLE', N'Roles', 'COLUMN', N'Name';
    SET @description57 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description57, 'SCHEMA', @defaultSchema57, 'TABLE', N'Roles', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830093125_P10_1_Identity'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [UserId] bigint NOT NULL,
        [RoleId] bigint NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema58 AS sysname;
    SET @defaultSchema58 = SCHEMA_NAME();
    DECLARE @description58 AS sql_variant;
    SET @description58 = N'用户-角色关联';
    EXEC sp_addextendedproperty 'MS_Description', @description58, 'SCHEMA', @defaultSchema58, 'TABLE', N'UserRoles';
    SET @description58 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description58, 'SCHEMA', @defaultSchema58, 'TABLE', N'UserRoles', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830093125_P10_1_Identity'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Username] nvarchar(128) NOT NULL,
        [DisplayName] nvarchar(128) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [Status] int NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema59 AS sysname;
    SET @defaultSchema59 = SCHEMA_NAME();
    DECLARE @description59 AS sql_variant;
    SET @description59 = N'用户';
    EXEC sp_addextendedproperty 'MS_Description', @description59, 'SCHEMA', @defaultSchema59, 'TABLE', N'Users';
    SET @description59 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description59, 'SCHEMA', @defaultSchema59, 'TABLE', N'Users', 'COLUMN', N'Id';
    SET @description59 = N'所属租户';
    EXEC sp_addextendedproperty 'MS_Description', @description59, 'SCHEMA', @defaultSchema59, 'TABLE', N'Users', 'COLUMN', N'TenantId';
    SET @description59 = N'登录名（全局唯一）';
    EXEC sp_addextendedproperty 'MS_Description', @description59, 'SCHEMA', @defaultSchema59, 'TABLE', N'Users', 'COLUMN', N'Username';
    SET @description59 = N'显示名';
    EXEC sp_addextendedproperty 'MS_Description', @description59, 'SCHEMA', @defaultSchema59, 'TABLE', N'Users', 'COLUMN', N'DisplayName';
    SET @description59 = N'邮箱';
    EXEC sp_addextendedproperty 'MS_Description', @description59, 'SCHEMA', @defaultSchema59, 'TABLE', N'Users', 'COLUMN', N'Email';
    SET @description59 = N'状态';
    EXEC sp_addextendedproperty 'MS_Description', @description59, 'SCHEMA', @defaultSchema59, 'TABLE', N'Users', 'COLUMN', N'Status';
    SET @description59 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description59, 'SCHEMA', @defaultSchema59, 'TABLE', N'Users', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830093125_P10_1_Identity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Permissions_TenantId_Code] ON [Permissions] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830093125_P10_1_Identity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RolePermissions_TenantId_RoleId_PermissionId] ON [RolePermissions] ([TenantId], [RoleId], [PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830093125_P10_1_Identity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_TenantId_Code] ON [Roles] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830093125_P10_1_Identity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserRoles_TenantId_UserId_RoleId] ON [UserRoles] ([TenantId], [UserId], [RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830093125_P10_1_Identity'
)
BEGIN
    CREATE INDEX [IX_Users_TenantId] ON [Users] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830093125_P10_1_Identity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830093125_P10_1_Identity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260830093125_P10_1_Identity', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830104059_P10_3_AuditLog'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [UserId] bigint NULL,
        [Actor] nvarchar(128) NOT NULL,
        [Action] nvarchar(128) NOT NULL,
        [EntityType] nvarchar(128) NOT NULL,
        [EntityId] nvarchar(256) NULL,
        [BeforeJson] nvarchar(max) NULL,
        [AfterJson] nvarchar(max) NULL,
        [Result] nvarchar(32) NOT NULL,
        [Message] nvarchar(max) NULL,
        [Timestamp] datetime2 NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema60 AS sysname;
    SET @defaultSchema60 = SCHEMA_NAME();
    DECLARE @description60 AS sql_variant;
    SET @description60 = N'审计日志';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', @defaultSchema60, 'TABLE', N'AuditLogs';
    SET @description60 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', @defaultSchema60, 'TABLE', N'AuditLogs', 'COLUMN', N'Id';
    SET @description60 = N'所属租户（0=平台级）';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', @defaultSchema60, 'TABLE', N'AuditLogs', 'COLUMN', N'TenantId';
    SET @description60 = N'操作者标识';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', @defaultSchema60, 'TABLE', N'AuditLogs', 'COLUMN', N'Actor';
    SET @description60 = N'动作类型';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', @defaultSchema60, 'TABLE', N'AuditLogs', 'COLUMN', N'Action';
    SET @description60 = N'实体类型';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', @defaultSchema60, 'TABLE', N'AuditLogs', 'COLUMN', N'EntityType';
    SET @description60 = N'实体Id';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', @defaultSchema60, 'TABLE', N'AuditLogs', 'COLUMN', N'EntityId';
    SET @description60 = N'结果';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', @defaultSchema60, 'TABLE', N'AuditLogs', 'COLUMN', N'Result';
    SET @description60 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', @defaultSchema60, 'TABLE', N'AuditLogs', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830104059_P10_3_AuditLog'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_TenantId] ON [AuditLogs] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830104059_P10_3_AuditLog'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_TenantId_Action] ON [AuditLogs] ([TenantId], [Action]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830104059_P10_3_AuditLog'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_TenantId_EntityType] ON [AuditLogs] ([TenantId], [EntityType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830104059_P10_3_AuditLog'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Timestamp] ON [AuditLogs] ([Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830104059_P10_3_AuditLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260830104059_P10_3_AuditLog', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830111544_P10_4_Quota'
)
BEGIN
    CREATE TABLE [QuotaPolicies] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [ResourceType] int NOT NULL,
        [Limit] bigint NOT NULL,
        [Window] int NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_QuotaPolicies] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema61 AS sysname;
    SET @defaultSchema61 = SCHEMA_NAME();
    DECLARE @description61 AS sql_variant;
    SET @description61 = N'配额策略';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', @defaultSchema61, 'TABLE', N'QuotaPolicies';
    SET @description61 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', @defaultSchema61, 'TABLE', N'QuotaPolicies', 'COLUMN', N'Id';
    SET @description61 = N'所属租户（0=平台默认）';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', @defaultSchema61, 'TABLE', N'QuotaPolicies', 'COLUMN', N'TenantId';
    SET @description61 = N'资源类型';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', @defaultSchema61, 'TABLE', N'QuotaPolicies', 'COLUMN', N'ResourceType';
    SET @description61 = N'上限';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', @defaultSchema61, 'TABLE', N'QuotaPolicies', 'COLUMN', N'Limit';
    SET @description61 = N'周期窗口';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', @defaultSchema61, 'TABLE', N'QuotaPolicies', 'COLUMN', N'Window';
    SET @description61 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', @defaultSchema61, 'TABLE', N'QuotaPolicies', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830111544_P10_4_Quota'
)
BEGIN
    CREATE TABLE [QuotaUsages] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [ResourceType] int NOT NULL,
        [Used] bigint NOT NULL,
        [PeriodKey] nvarchar(32) NOT NULL,
        [LastReset] datetime2 NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_QuotaUsages] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema62 AS sysname;
    SET @defaultSchema62 = SCHEMA_NAME();
    DECLARE @description62 AS sql_variant;
    SET @description62 = N'配额使用量';
    EXEC sp_addextendedproperty 'MS_Description', @description62, 'SCHEMA', @defaultSchema62, 'TABLE', N'QuotaUsages';
    SET @description62 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description62, 'SCHEMA', @defaultSchema62, 'TABLE', N'QuotaUsages', 'COLUMN', N'Id';
    SET @description62 = N'所属租户';
    EXEC sp_addextendedproperty 'MS_Description', @description62, 'SCHEMA', @defaultSchema62, 'TABLE', N'QuotaUsages', 'COLUMN', N'TenantId';
    SET @description62 = N'资源类型';
    EXEC sp_addextendedproperty 'MS_Description', @description62, 'SCHEMA', @defaultSchema62, 'TABLE', N'QuotaUsages', 'COLUMN', N'ResourceType';
    SET @description62 = N'已用';
    EXEC sp_addextendedproperty 'MS_Description', @description62, 'SCHEMA', @defaultSchema62, 'TABLE', N'QuotaUsages', 'COLUMN', N'Used';
    SET @description62 = N'周期键';
    EXEC sp_addextendedproperty 'MS_Description', @description62, 'SCHEMA', @defaultSchema62, 'TABLE', N'QuotaUsages', 'COLUMN', N'PeriodKey';
    SET @description62 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description62, 'SCHEMA', @defaultSchema62, 'TABLE', N'QuotaUsages', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830111544_P10_4_Quota'
)
BEGIN
    CREATE UNIQUE INDEX [IX_QuotaPolicies_TenantId_ResourceType] ON [QuotaPolicies] ([TenantId], [ResourceType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830111544_P10_4_Quota'
)
BEGIN
    CREATE UNIQUE INDEX [IX_QuotaUsages_TenantId_ResourceType] ON [QuotaUsages] ([TenantId], [ResourceType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830111544_P10_4_Quota'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260830111544_P10_4_Quota', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901063518_AddUserSecurityStamp'
)
BEGIN
    DECLARE @var63 nvarchar(max);
    SELECT @var63 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'PasswordHash');
    IF @var63 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT ' + @var63 + ';');
    ALTER TABLE [Users] ALTER COLUMN [PasswordHash] nvarchar(256) NULL;
    DECLARE @defaultSchema64 AS sysname;
    SET @defaultSchema64 = SCHEMA_NAME();
    DECLARE @description64 AS sql_variant;
    SET @description64 = N'口令哈希（PBKDF2，可选）';
    EXEC sp_addextendedproperty 'MS_Description', @description64, 'SCHEMA', @defaultSchema64, 'TABLE', N'Users', 'COLUMN', N'PasswordHash';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901063518_AddUserSecurityStamp'
)
BEGIN
    ALTER TABLE [Users] ADD [SecurityStamp] nvarchar(64) NULL;
    DECLARE @defaultSchema65 AS sysname;
    SET @defaultSchema65 = SCHEMA_NAME();
    DECLARE @description65 AS sql_variant;
    SET @description65 = N'安全戳（令牌吊销用）';
    EXEC sp_addextendedproperty 'MS_Description', @description65, 'SCHEMA', @defaultSchema65, 'TABLE', N'Users', 'COLUMN', N'SecurityStamp';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901063518_AddUserSecurityStamp'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260901063518_AddUserSecurityStamp', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901071656_AddDataSourceAccessGrants'
)
BEGIN
    CREATE TABLE [DataSourceAccessGrants] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [DataSourceId] bigint NOT NULL,
        [SubjectType] int NOT NULL,
        [SubjectId] bigint NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_DataSourceAccessGrants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DataSourceAccessGrants_DataSources_DataSourceId] FOREIGN KEY ([DataSourceId]) REFERENCES [DataSources] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema66 AS sysname;
    SET @defaultSchema66 = SCHEMA_NAME();
    DECLARE @description66 AS sql_variant;
    SET @description66 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description66, 'SCHEMA', @defaultSchema66, 'TABLE', N'DataSourceAccessGrants', 'COLUMN', N'Id';
    SET @description66 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description66, 'SCHEMA', @defaultSchema66, 'TABLE', N'DataSourceAccessGrants', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901071656_AddDataSourceAccessGrants'
)
BEGIN
    CREATE INDEX [IX_DataSourceAccessGrants_DataSourceId] ON [DataSourceAccessGrants] ([DataSourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901071656_AddDataSourceAccessGrants'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DataSourceAccessGrants_TenantId_DataSourceId_SubjectType_SubjectId] ON [DataSourceAccessGrants] ([TenantId], [DataSourceId], [SubjectType], [SubjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901071656_AddDataSourceAccessGrants'
)
BEGIN
    CREATE INDEX [IX_DataSourceAccessGrants_TenantId_SubjectType_SubjectId] ON [DataSourceAccessGrants] ([TenantId], [SubjectType], [SubjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901071656_AddDataSourceAccessGrants'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260901071656_AddDataSourceAccessGrants', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901080848_AddRowLevelSecurityPolicies'
)
BEGIN
    CREATE TABLE [RowLevelSecurityPolicies] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [DataSourceId] bigint NOT NULL,
        [MetadataTableId] bigint NOT NULL,
        [MetadataColumnId] bigint NOT NULL,
        [SubjectType] int NOT NULL,
        [SubjectId] bigint NULL,
        [SubjectKey] nvarchar(128) NULL,
        [SubjectValue] nvarchar(512) NULL,
        [Effect] int NOT NULL,
        [Operator] nvarchar(16) NOT NULL,
        [Value] nvarchar(2048) NOT NULL,
        [Enabled] bit NOT NULL,
        [Version] bigint NOT NULL,
        [UpdatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_RowLevelSecurityPolicies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RowLevelSecurityPolicies_DataSources_DataSourceId] FOREIGN KEY ([DataSourceId]) REFERENCES [DataSources] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RowLevelSecurityPolicies_MetadataColumns_MetadataColumnId] FOREIGN KEY ([MetadataColumnId]) REFERENCES [MetadataColumns] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RowLevelSecurityPolicies_MetadataTables_MetadataTableId] FOREIGN KEY ([MetadataTableId]) REFERENCES [MetadataTables] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema67 AS sysname;
    SET @defaultSchema67 = SCHEMA_NAME();
    DECLARE @description67 AS sql_variant;
    SET @description67 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description67, 'SCHEMA', @defaultSchema67, 'TABLE', N'RowLevelSecurityPolicies', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901080848_AddRowLevelSecurityPolicies'
)
BEGIN
    CREATE INDEX [IX_RowLevelSecurityPolicies_DataSourceId] ON [RowLevelSecurityPolicies] ([DataSourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901080848_AddRowLevelSecurityPolicies'
)
BEGIN
    CREATE INDEX [IX_RowLevelSecurityPolicies_MetadataColumnId] ON [RowLevelSecurityPolicies] ([MetadataColumnId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901080848_AddRowLevelSecurityPolicies'
)
BEGIN
    CREATE INDEX [IX_RowLevelSecurityPolicies_MetadataTableId] ON [RowLevelSecurityPolicies] ([MetadataTableId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901080848_AddRowLevelSecurityPolicies'
)
BEGIN
    CREATE INDEX [IX_RowLevelSecurityPolicies_TenantId_DataSourceId_MetadataTableId_Enabled] ON [RowLevelSecurityPolicies] ([TenantId], [DataSourceId], [MetadataTableId], [Enabled]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901080848_AddRowLevelSecurityPolicies'
)
BEGIN
    CREATE INDEX [IX_RowLevelSecurityPolicies_TenantId_SubjectType_SubjectId] ON [RowLevelSecurityPolicies] ([TenantId], [SubjectType], [SubjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901080848_AddRowLevelSecurityPolicies'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260901080848_AddRowLevelSecurityPolicies', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902083109_AddUiLocalizationGovernance'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260902083109_AddUiLocalizationGovernance', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902083839_SyncUiLocalizationModel'
)
BEGIN
    CREATE TABLE [UiLanguages] (
        [Id] bigint NOT NULL IDENTITY,
        [Culture] nvarchar(16) NOT NULL,
        [DisplayName] nvarchar(64) NOT NULL,
        [NativeName] nvarchar(64) NOT NULL,
        [Enabled] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_UiLanguages] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema68 AS sysname;
    SET @defaultSchema68 = SCHEMA_NAME();
    DECLARE @description68 AS sql_variant;
    SET @description68 = N'平台界面语言目录';
    EXEC sp_addextendedproperty 'MS_Description', @description68, 'SCHEMA', @defaultSchema68, 'TABLE', N'UiLanguages';
    SET @description68 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description68, 'SCHEMA', @defaultSchema68, 'TABLE', N'UiLanguages', 'COLUMN', N'Id';
    SET @description68 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description68, 'SCHEMA', @defaultSchema68, 'TABLE', N'UiLanguages', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902083839_SyncUiLocalizationModel'
)
BEGIN
    CREATE TABLE [UiTextResources] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Culture] nvarchar(16) NOT NULL,
        [ResourceKey] nvarchar(160) NOT NULL,
        [Value] nvarchar(2048) NOT NULL,
        [Description] nvarchar(256) NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_UiTextResources] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema69 AS sysname;
    SET @defaultSchema69 = SCHEMA_NAME();
    DECLARE @description69 AS sql_variant;
    SET @description69 = N'平台及租户界面文本';
    EXEC sp_addextendedproperty 'MS_Description', @description69, 'SCHEMA', @defaultSchema69, 'TABLE', N'UiTextResources';
    SET @description69 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description69, 'SCHEMA', @defaultSchema69, 'TABLE', N'UiTextResources', 'COLUMN', N'Id';
    SET @description69 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description69, 'SCHEMA', @defaultSchema69, 'TABLE', N'UiTextResources', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902083839_SyncUiLocalizationModel'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UiLanguages_Culture] ON [UiLanguages] ([Culture]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902083839_SyncUiLocalizationModel'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UiTextResources_TenantId_Culture_ResourceKey] ON [UiTextResources] ([TenantId], [Culture], [ResourceKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902083839_SyncUiLocalizationModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260902083839_SyncUiLocalizationModel', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [UiTextResources] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [UiTextResources] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema70 AS sysname;
    SET @defaultSchema70 = SCHEMA_NAME();
    DECLARE @description70 AS sql_variant;
    SET @description70 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description70, 'SCHEMA', @defaultSchema70, 'TABLE', N'UiTextResources', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [UiTextResources] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [UiTextResources] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [UiLanguages] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [UiLanguages] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema71 AS sysname;
    SET @defaultSchema71 = SCHEMA_NAME();
    DECLARE @description71 AS sql_variant;
    SET @description71 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description71, 'SCHEMA', @defaultSchema71, 'TABLE', N'UiLanguages', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [UiLanguages] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [UiLanguages] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [Themes] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [Themes] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema72 AS sysname;
    SET @defaultSchema72 = SCHEMA_NAME();
    DECLARE @description72 AS sql_variant;
    SET @description72 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description72, 'SCHEMA', @defaultSchema72, 'TABLE', N'Themes', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [Themes] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [Themes] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [TenantSettings] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [TenantSettings] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema73 AS sysname;
    SET @defaultSchema73 = SCHEMA_NAME();
    DECLARE @description73 AS sql_variant;
    SET @description73 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description73, 'SCHEMA', @defaultSchema73, 'TABLE', N'TenantSettings', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [TenantSettings] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [TenantSettings] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [Tenants] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [Tenants] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema74 AS sysname;
    SET @defaultSchema74 = SCHEMA_NAME();
    DECLARE @description74 AS sql_variant;
    SET @description74 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description74, 'SCHEMA', @defaultSchema74, 'TABLE', N'Tenants', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [Tenants] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [Tenants] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [SemanticLabels] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [SemanticLabels] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema75 AS sysname;
    SET @defaultSchema75 = SCHEMA_NAME();
    DECLARE @description75 AS sql_variant;
    SET @description75 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description75, 'SCHEMA', @defaultSchema75, 'TABLE', N'SemanticLabels', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [SemanticLabels] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [SemanticLabels] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [Roles] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [Roles] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema76 AS sysname;
    SET @defaultSchema76 = SCHEMA_NAME();
    DECLARE @description76 AS sql_variant;
    SET @description76 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description76, 'SCHEMA', @defaultSchema76, 'TABLE', N'Roles', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [Roles] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [Roles] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [PhysicalBindings] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [PhysicalBindings] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema77 AS sysname;
    SET @defaultSchema77 = SCHEMA_NAME();
    DECLARE @description77 AS sql_variant;
    SET @description77 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description77, 'SCHEMA', @defaultSchema77, 'TABLE', N'PhysicalBindings', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [PhysicalBindings] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [PhysicalBindings] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [MetadataTables] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [MetadataTables] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema78 AS sysname;
    SET @defaultSchema78 = SCHEMA_NAME();
    DECLARE @description78 AS sql_variant;
    SET @description78 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description78, 'SCHEMA', @defaultSchema78, 'TABLE', N'MetadataTables', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [MetadataTables] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [MetadataTables] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [MetadataSemantics] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [MetadataSemantics] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema79 AS sysname;
    SET @defaultSchema79 = SCHEMA_NAME();
    DECLARE @description79 AS sql_variant;
    SET @description79 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description79, 'SCHEMA', @defaultSchema79, 'TABLE', N'MetadataSemantics', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [MetadataSemantics] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [MetadataSemantics] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [MetadataColumns] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [MetadataColumns] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema80 AS sysname;
    SET @defaultSchema80 = SCHEMA_NAME();
    DECLARE @description80 AS sql_variant;
    SET @description80 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description80, 'SCHEMA', @defaultSchema80, 'TABLE', N'MetadataColumns', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [MetadataColumns] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [MetadataColumns] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [LearningRecords] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [LearningRecords] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema81 AS sysname;
    SET @defaultSchema81 = SCHEMA_NAME();
    DECLARE @description81 AS sql_variant;
    SET @description81 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description81, 'SCHEMA', @defaultSchema81, 'TABLE', N'LearningRecords', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [LearningRecords] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [LearningRecords] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [DataSources] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [DataSources] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema82 AS sysname;
    SET @defaultSchema82 = SCHEMA_NAME();
    DECLARE @description82 AS sql_variant;
    SET @description82 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description82, 'SCHEMA', @defaultSchema82, 'TABLE', N'DataSources', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [DataSources] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [DataSources] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [Dashboards] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [Dashboards] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema83 AS sysname;
    SET @defaultSchema83 = SCHEMA_NAME();
    DECLARE @description83 AS sql_variant;
    SET @description83 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description83, 'SCHEMA', @defaultSchema83, 'TABLE', N'Dashboards', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [Dashboards] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [Dashboards] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntityRelationships] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntityRelationships] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema84 AS sysname;
    SET @defaultSchema84 = SCHEMA_NAME();
    DECLARE @description84 AS sql_variant;
    SET @description84 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description84, 'SCHEMA', @defaultSchema84, 'TABLE', N'BusinessEntityRelationships', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntityRelationships] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntityRelationships] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntityMetrics] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntityMetrics] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema85 AS sysname;
    SET @defaultSchema85 = SCHEMA_NAME();
    DECLARE @description85 AS sql_variant;
    SET @description85 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description85, 'SCHEMA', @defaultSchema85, 'TABLE', N'BusinessEntityMetrics', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntityMetrics] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntityMetrics] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntityKeys] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntityKeys] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema86 AS sysname;
    SET @defaultSchema86 = SCHEMA_NAME();
    DECLARE @description86 AS sql_variant;
    SET @description86 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description86, 'SCHEMA', @defaultSchema86, 'TABLE', N'BusinessEntityKeys', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntityKeys] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntityKeys] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntityDimensions] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntityDimensions] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema87 AS sysname;
    SET @defaultSchema87 = SCHEMA_NAME();
    DECLARE @description87 AS sql_variant;
    SET @description87 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description87, 'SCHEMA', @defaultSchema87, 'TABLE', N'BusinessEntityDimensions', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntityDimensions] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntityDimensions] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntityAttributes] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntityAttributes] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema88 AS sysname;
    SET @defaultSchema88 = SCHEMA_NAME();
    DECLARE @description88 AS sql_variant;
    SET @description88 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description88, 'SCHEMA', @defaultSchema88, 'TABLE', N'BusinessEntityAttributes', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntityAttributes] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntityAttributes] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntities] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntities] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema89 AS sysname;
    SET @defaultSchema89 = SCHEMA_NAME();
    DECLARE @description89 AS sql_variant;
    SET @description89 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description89, 'SCHEMA', @defaultSchema89, 'TABLE', N'BusinessEntities', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntities] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessEntities] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessDomains] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessDomains] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema90 AS sysname;
    SET @defaultSchema90 = SCHEMA_NAME();
    DECLARE @description90 AS sql_variant;
    SET @description90 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description90, 'SCHEMA', @defaultSchema90, 'TABLE', N'BusinessDomains', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessDomains] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [BusinessDomains] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [AppPlans] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [AppPlans] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema91 AS sysname;
    SET @defaultSchema91 = SCHEMA_NAME();
    DECLARE @description91 AS sql_variant;
    SET @description91 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description91, 'SCHEMA', @defaultSchema91, 'TABLE', N'AppPlans', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [AppPlans] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [AppPlans] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [AgentPlans] ADD [CreatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [AgentPlans] ADD [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
    DECLARE @defaultSchema92 AS sysname;
    SET @defaultSchema92 = SCHEMA_NAME();
    DECLARE @description92 AS sql_variant;
    SET @description92 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description92, 'SCHEMA', @defaultSchema92, 'TABLE', N'AgentPlans', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [AgentPlans] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    ALTER TABLE [AgentPlans] ADD [UpdatedTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904145611_M1_01_AuditConcurrency'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260904145611_M1_01_AuditConcurrency', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904152748_M1_02_TenantAndSettingIntegrity'
)
BEGIN
    DECLARE @defaultSchema93 AS sysname;
    SET @defaultSchema93 = SCHEMA_NAME();
    DECLARE @description93 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema93, 'TABLE', N'TenantSettings', 'COLUMN', N'DataType';
    SET @description93 = N'值类型(string|int|bool|json)';
    EXEC sp_addextendedproperty 'MS_Description', @description93, 'SCHEMA', @defaultSchema93, 'TABLE', N'TenantSettings', 'COLUMN', N'DataType';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904152748_M1_02_TenantAndSettingIntegrity'
)
BEGIN
    ALTER TABLE [TenantSettings] ADD [IsLocked] bit NOT NULL DEFAULT CAST(0 AS bit);
    DECLARE @defaultSchema94 AS sysname;
    SET @defaultSchema94 = SCHEMA_NAME();
    DECLARE @description94 AS sql_variant;
    SET @description94 = N'是否锁定(租户不可覆盖)';
    EXEC sp_addextendedproperty 'MS_Description', @description94, 'SCHEMA', @defaultSchema94, 'TABLE', N'TenantSettings', 'COLUMN', N'IsLocked';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904152748_M1_02_TenantAndSettingIntegrity'
)
BEGIN
    DECLARE @var95 nvarchar(max);
    SELECT @var95 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tenants]') AND [c].[name] = N'TenantName');
    IF @var95 IS NOT NULL EXEC(N'ALTER TABLE [Tenants] DROP CONSTRAINT ' + @var95 + ';');
    ALTER TABLE [Tenants] ALTER COLUMN [TenantName] nvarchar(128) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904152748_M1_02_TenantAndSettingIntegrity'
)
BEGIN
    DROP INDEX [IX_Tenants_TenantCode] ON [Tenants];
    DECLARE @var96 nvarchar(max);
    SELECT @var96 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tenants]') AND [c].[name] = N'TenantCode');
    IF @var96 IS NOT NULL EXEC(N'ALTER TABLE [Tenants] DROP CONSTRAINT ' + @var96 + ';');
    ALTER TABLE [Tenants] ALTER COLUMN [TenantCode] nvarchar(64) NULL;
    DECLARE @defaultSchema97 AS sysname;
    SET @defaultSchema97 = SCHEMA_NAME();
    DECLARE @description97 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema97, 'TABLE', N'Tenants', 'COLUMN', N'TenantCode';
    SET @description97 = N'租户编码（规范化小写存储）';
    EXEC sp_addextendedproperty 'MS_Description', @description97, 'SCHEMA', @defaultSchema97, 'TABLE', N'Tenants', 'COLUMN', N'TenantCode';
    EXEC(N'CREATE UNIQUE INDEX [IX_Tenants_TenantCode] ON [Tenants] ([TenantCode]) WHERE [TenantCode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904152748_M1_02_TenantAndSettingIntegrity'
)
BEGIN
    ALTER TABLE [Tenants] ADD [DisabledAt] datetime2 NULL;
    DECLARE @defaultSchema98 AS sysname;
    SET @defaultSchema98 = SCHEMA_NAME();
    DECLARE @description98 AS sql_variant;
    SET @description98 = N'停用时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description98, 'SCHEMA', @defaultSchema98, 'TABLE', N'Tenants', 'COLUMN', N'DisabledAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904152748_M1_02_TenantAndSettingIntegrity'
)
BEGIN
    ALTER TABLE [Tenants] ADD [DisabledByUserId] bigint NULL;
    DECLARE @defaultSchema99 AS sysname;
    SET @defaultSchema99 = SCHEMA_NAME();
    DECLARE @description99 AS sql_variant;
    SET @description99 = N'停用操作者用户Id';
    EXEC sp_addextendedproperty 'MS_Description', @description99, 'SCHEMA', @defaultSchema99, 'TABLE', N'Tenants', 'COLUMN', N'DisabledByUserId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904152748_M1_02_TenantAndSettingIntegrity'
)
BEGIN
    ALTER TABLE [Tenants] ADD [DisabledReason] nvarchar(512) NULL;
    DECLARE @defaultSchema100 AS sysname;
    SET @defaultSchema100 = SCHEMA_NAME();
    DECLARE @description100 AS sql_variant;
    SET @description100 = N'停用原因';
    EXEC sp_addextendedproperty 'MS_Description', @description100, 'SCHEMA', @defaultSchema100, 'TABLE', N'Tenants', 'COLUMN', N'DisabledReason';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904152748_M1_02_TenantAndSettingIntegrity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260904152748_M1_02_TenantAndSettingIntegrity', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904154222_M1_03_UserRolePermissionIntegrity'
)
BEGIN
    DROP INDEX [IX_Users_Username] ON [Users];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904154222_M1_03_UserRolePermissionIntegrity'
)
BEGIN
    DECLARE @defaultSchema101 AS sysname;
    SET @defaultSchema101 = SCHEMA_NAME();
    DECLARE @description101 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema101, 'TABLE', N'Users', 'COLUMN', N'Username';
    SET @description101 = N'登录名（展示用，大小写原始）';
    EXEC sp_addextendedproperty 'MS_Description', @description101, 'SCHEMA', @defaultSchema101, 'TABLE', N'Users', 'COLUMN', N'Username';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904154222_M1_03_UserRolePermissionIntegrity'
)
BEGIN
    ALTER TABLE [Users] ADD [EmailConfirmed] bit NOT NULL DEFAULT CAST(0 AS bit);
    DECLARE @defaultSchema102 AS sysname;
    SET @defaultSchema102 = SCHEMA_NAME();
    DECLARE @description102 AS sql_variant;
    SET @description102 = N'邮箱是否已验证';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', @defaultSchema102, 'TABLE', N'Users', 'COLUMN', N'EmailConfirmed';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904154222_M1_03_UserRolePermissionIntegrity'
)
BEGIN
    ALTER TABLE [Users] ADD [NormalizedEmail] nvarchar(256) NULL;
    DECLARE @defaultSchema103 AS sysname;
    SET @defaultSchema103 = SCHEMA_NAME();
    DECLARE @description103 AS sql_variant;
    SET @description103 = N'规范化邮箱（小写）';
    EXEC sp_addextendedproperty 'MS_Description', @description103, 'SCHEMA', @defaultSchema103, 'TABLE', N'Users', 'COLUMN', N'NormalizedEmail';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904154222_M1_03_UserRolePermissionIntegrity'
)
BEGIN
    ALTER TABLE [Users] ADD [NormalizedUsername] nvarchar(128) NULL;
    DECLARE @defaultSchema104 AS sysname;
    SET @defaultSchema104 = SCHEMA_NAME();
    DECLARE @description104 AS sql_variant;
    SET @description104 = N'规范化登录名（小写，租户内唯一）';
    EXEC sp_addextendedproperty 'MS_Description', @description104, 'SCHEMA', @defaultSchema104, 'TABLE', N'Users', 'COLUMN', N'NormalizedUsername';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904154222_M1_03_UserRolePermissionIntegrity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_TenantId_NormalizedUsername] ON [Users] ([TenantId], [NormalizedUsername]) WHERE [NormalizedUsername] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904154222_M1_03_UserRolePermissionIntegrity'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904154222_M1_03_UserRolePermissionIntegrity'
)
BEGIN
    CREATE INDEX [IX_UserRoles_UserId] ON [UserRoles] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904154222_M1_03_UserRolePermissionIntegrity'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_PermissionId] ON [RolePermissions] ([PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904154222_M1_03_UserRolePermissionIntegrity'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_RoleId] ON [RolePermissions] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904154222_M1_03_UserRolePermissionIntegrity'
)
BEGIN
    ALTER TABLE [RolePermissions] ADD CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904154222_M1_03_UserRolePermissionIntegrity'
)
BEGIN
    ALTER TABLE [RolePermissions] ADD CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904154222_M1_03_UserRolePermissionIntegrity'
)
BEGIN
    ALTER TABLE [UserRoles] ADD CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904154222_M1_03_UserRolePermissionIntegrity'
)
BEGIN
    ALTER TABLE [UserRoles] ADD CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904154222_M1_03_UserRolePermissionIntegrity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260904154222_M1_03_UserRolePermissionIntegrity', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904155755_M1_04_DataSourceIntegrity'
)
BEGIN
    DECLARE @var105 nvarchar(max);
    SELECT @var105 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DataSources]') AND [c].[name] = N'Name');
    IF @var105 IS NOT NULL EXEC(N'ALTER TABLE [DataSources] DROP CONSTRAINT ' + @var105 + ';');
    ALTER TABLE [DataSources] ALTER COLUMN [Name] nvarchar(128) NULL;
    DECLARE @defaultSchema106 AS sysname;
    SET @defaultSchema106 = SCHEMA_NAME();
    DECLARE @description106 AS sql_variant;
    SET @description106 = N'名称（展示用，保留原始大小写）';
    EXEC sp_addextendedproperty 'MS_Description', @description106, 'SCHEMA', @defaultSchema106, 'TABLE', N'DataSources', 'COLUMN', N'Name';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904155755_M1_04_DataSourceIntegrity'
)
BEGIN
    UPDATE [DataSources] SET [Enabled] = 1 WHERE [Enabled] IS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904155755_M1_04_DataSourceIntegrity'
)
BEGIN
    DECLARE @var107 nvarchar(max);
    SELECT @var107 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DataSources]') AND [c].[name] = N'Enabled');
    IF @var107 IS NOT NULL EXEC(N'ALTER TABLE [DataSources] DROP CONSTRAINT ' + @var107 + ';');
    EXEC(N'UPDATE [DataSources] SET [Enabled] = CAST(1 AS bit) WHERE [Enabled] IS NULL');
    ALTER TABLE [DataSources] ALTER COLUMN [Enabled] bit NOT NULL;
    ALTER TABLE [DataSources] ADD DEFAULT CAST(1 AS bit) FOR [Enabled];
    DECLARE @defaultSchema108 AS sysname;
    SET @defaultSchema108 = SCHEMA_NAME();
    DECLARE @description108 AS sql_variant;
    SET @description108 = N'是否启用';
    EXEC sp_addextendedproperty 'MS_Description', @description108, 'SCHEMA', @defaultSchema108, 'TABLE', N'DataSources', 'COLUMN', N'Enabled';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904155755_M1_04_DataSourceIntegrity'
)
BEGIN
    DECLARE @var109 nvarchar(max);
    SELECT @var109 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DataSources]') AND [c].[name] = N'DbType');
    IF @var109 IS NOT NULL EXEC(N'ALTER TABLE [DataSources] DROP CONSTRAINT ' + @var109 + ';');
    ALTER TABLE [DataSources] ALTER COLUMN [DbType] nvarchar(32) NULL;
    DECLARE @defaultSchema110 AS sysname;
    SET @defaultSchema110 = SCHEMA_NAME();
    DECLARE @description110 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema110, 'TABLE', N'DataSources', 'COLUMN', N'DbType';
    SET @description110 = N'数据库类型(MYSQL/SQLSERVER/POSTGRESQL)';
    EXEC sp_addextendedproperty 'MS_Description', @description110, 'SCHEMA', @defaultSchema110, 'TABLE', N'DataSources', 'COLUMN', N'DbType';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904155755_M1_04_DataSourceIntegrity'
)
BEGIN
    DECLARE @var111 nvarchar(max);
    SELECT @var111 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DataSources]') AND [c].[name] = N'ConnectionString');
    IF @var111 IS NOT NULL EXEC(N'ALTER TABLE [DataSources] DROP CONSTRAINT ' + @var111 + ';');
    ALTER TABLE [DataSources] ALTER COLUMN [ConnectionString] nvarchar(2048) NULL;
    DECLARE @defaultSchema112 AS sysname;
    SET @defaultSchema112 = SCHEMA_NAME();
    DECLARE @description112 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', @defaultSchema112, 'TABLE', N'DataSources', 'COLUMN', N'ConnectionString';
    SET @description112 = N'连接字符串（敏感，禁止日志记录）';
    EXEC sp_addextendedproperty 'MS_Description', @description112, 'SCHEMA', @defaultSchema112, 'TABLE', N'DataSources', 'COLUMN', N'ConnectionString';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904155755_M1_04_DataSourceIntegrity'
)
BEGIN
    ALTER TABLE [DataSources] ADD [LastErrorCode] nvarchar(64) NULL;
    DECLARE @defaultSchema113 AS sysname;
    SET @defaultSchema113 = SCHEMA_NAME();
    DECLARE @description113 AS sql_variant;
    SET @description113 = N'最近连接测试错误码(仅异常类型名,脱敏)';
    EXEC sp_addextendedproperty 'MS_Description', @description113, 'SCHEMA', @defaultSchema113, 'TABLE', N'DataSources', 'COLUMN', N'LastErrorCode';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904155755_M1_04_DataSourceIntegrity'
)
BEGIN
    ALTER TABLE [DataSources] ADD [LastTestStatus] nvarchar(32) NULL;
    DECLARE @defaultSchema114 AS sysname;
    SET @defaultSchema114 = SCHEMA_NAME();
    DECLARE @description114 AS sql_variant;
    SET @description114 = N'最近连接测试状态(Ok/Failed/Unknown)';
    EXEC sp_addextendedproperty 'MS_Description', @description114, 'SCHEMA', @defaultSchema114, 'TABLE', N'DataSources', 'COLUMN', N'LastTestStatus';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904155755_M1_04_DataSourceIntegrity'
)
BEGIN
    ALTER TABLE [DataSources] ADD [LastTestTime] datetime2 NULL;
    DECLARE @defaultSchema115 AS sysname;
    SET @defaultSchema115 = SCHEMA_NAME();
    DECLARE @description115 AS sql_variant;
    SET @description115 = N'最近连接测试时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description115, 'SCHEMA', @defaultSchema115, 'TABLE', N'DataSources', 'COLUMN', N'LastTestTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904155755_M1_04_DataSourceIntegrity'
)
BEGIN
    ALTER TABLE [DataSources] ADD [NormalizedName] nvarchar(128) NULL;
    DECLARE @defaultSchema116 AS sysname;
    SET @defaultSchema116 = SCHEMA_NAME();
    DECLARE @description116 AS sql_variant;
    SET @description116 = N'规范化名称（小写去空白），租户内唯一键';
    EXEC sp_addextendedproperty 'MS_Description', @description116, 'SCHEMA', @defaultSchema116, 'TABLE', N'DataSources', 'COLUMN', N'NormalizedName';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904155755_M1_04_DataSourceIntegrity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DataSources_TenantId_NormalizedName] ON [DataSources] ([TenantId], [NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904155755_M1_04_DataSourceIntegrity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260904155755_M1_04_DataSourceIntegrity', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    DROP INDEX [IX_MetadataTables_DataSourceId_TableName] ON [MetadataTables];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    UPDATE [MetadataTables] SET [TableName] = N'table_' + CAST([Id] AS NVARCHAR(20)) WHERE [TableName] IS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    DECLARE @defaultSchema117 AS sysname;
    SET @defaultSchema117 = SCHEMA_NAME();
    DECLARE @description117 AS sql_variant;
    SET @description117 = N'向量维度';
    EXEC sp_addextendedproperty 'MS_Description', @description117, 'SCHEMA', @defaultSchema117, 'TABLE', N'MetadataTables', 'COLUMN', N'VectorDimension';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    DECLARE @var118 nvarchar(max);
    SELECT @var118 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MetadataTables]') AND [c].[name] = N'TableName');
    IF @var118 IS NOT NULL EXEC(N'ALTER TABLE [MetadataTables] DROP CONSTRAINT ' + @var118 + ';');
    ALTER TABLE [MetadataTables] ALTER COLUMN [TableName] nvarchar(128) NOT NULL;
    DECLARE @defaultSchema119 AS sysname;
    SET @defaultSchema119 = SCHEMA_NAME();
    DECLARE @description119 AS sql_variant;
    SET @description119 = N'表名';
    EXEC sp_addextendedproperty 'MS_Description', @description119, 'SCHEMA', @defaultSchema119, 'TABLE', N'MetadataTables', 'COLUMN', N'TableName';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    DECLARE @var120 nvarchar(max);
    SELECT @var120 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MetadataTables]') AND [c].[name] = N'EmbeddingModel');
    IF @var120 IS NOT NULL EXEC(N'ALTER TABLE [MetadataTables] DROP CONSTRAINT ' + @var120 + ';');
    ALTER TABLE [MetadataTables] ALTER COLUMN [EmbeddingModel] nvarchar(128) NULL;
    DECLARE @defaultSchema121 AS sysname;
    SET @defaultSchema121 = SCHEMA_NAME();
    DECLARE @description121 AS sql_variant;
    SET @description121 = N'Embedding模型';
    EXEC sp_addextendedproperty 'MS_Description', @description121, 'SCHEMA', @defaultSchema121, 'TABLE', N'MetadataTables', 'COLUMN', N'EmbeddingModel';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    ALTER TABLE [MetadataTables] ADD [CatalogName] nvarchar(128) NULL;
    DECLARE @defaultSchema122 AS sysname;
    SET @defaultSchema122 = SCHEMA_NAME();
    DECLARE @description122 AS sql_variant;
    SET @description122 = N'目录名';
    EXEC sp_addextendedproperty 'MS_Description', @description122, 'SCHEMA', @defaultSchema122, 'TABLE', N'MetadataTables', 'COLUMN', N'CatalogName';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    ALTER TABLE [MetadataTables] ADD [SchemaName] nvarchar(128) NULL;
    DECLARE @defaultSchema123 AS sysname;
    SET @defaultSchema123 = SCHEMA_NAME();
    DECLARE @description123 AS sql_variant;
    SET @description123 = N'模式名';
    EXEC sp_addextendedproperty 'MS_Description', @description123, 'SCHEMA', @defaultSchema123, 'TABLE', N'MetadataTables', 'COLUMN', N'SchemaName';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    ALTER TABLE [MetadataTables] ADD [VectorErrorCode] nvarchar(64) NULL;
    DECLARE @defaultSchema124 AS sysname;
    SET @defaultSchema124 = SCHEMA_NAME();
    DECLARE @description124 AS sql_variant;
    SET @description124 = N'向量错误码';
    EXEC sp_addextendedproperty 'MS_Description', @description124, 'SCHEMA', @defaultSchema124, 'TABLE', N'MetadataTables', 'COLUMN', N'VectorErrorCode';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    ALTER TABLE [MetadataTables] ADD [VectorStatus] nvarchar(16) NULL;
    DECLARE @defaultSchema125 AS sysname;
    SET @defaultSchema125 = SCHEMA_NAME();
    DECLARE @description125 AS sql_variant;
    SET @description125 = N'向量状态';
    EXEC sp_addextendedproperty 'MS_Description', @description125, 'SCHEMA', @defaultSchema125, 'TABLE', N'MetadataTables', 'COLUMN', N'VectorStatus';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    ALTER TABLE [MetadataTables] ADD [VectorSyncTime] datetime2 NULL;
    DECLARE @defaultSchema126 AS sysname;
    SET @defaultSchema126 = SCHEMA_NAME();
    DECLARE @description126 AS sql_variant;
    SET @description126 = N'向量同步时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description126, 'SCHEMA', @defaultSchema126, 'TABLE', N'MetadataTables', 'COLUMN', N'VectorSyncTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    DECLARE @defaultSchema127 AS sysname;
    SET @defaultSchema127 = SCHEMA_NAME();
    DECLARE @description127 AS sql_variant;
    SET @description127 = N'向量维度';
    EXEC sp_addextendedproperty 'MS_Description', @description127, 'SCHEMA', @defaultSchema127, 'TABLE', N'MetadataSemantics', 'COLUMN', N'VectorDimension';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    DECLARE @var128 nvarchar(max);
    SELECT @var128 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MetadataSemantics]') AND [c].[name] = N'Synonyms');
    IF @var128 IS NOT NULL EXEC(N'ALTER TABLE [MetadataSemantics] DROP CONSTRAINT ' + @var128 + ';');
    ALTER TABLE [MetadataSemantics] ALTER COLUMN [Synonyms] nvarchar(2048) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    UPDATE [MetadataSemantics] SET [Source] = N'Manual' WHERE [Source] IS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    DECLARE @var129 nvarchar(max);
    SELECT @var129 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MetadataSemantics]') AND [c].[name] = N'Source');
    IF @var129 IS NOT NULL EXEC(N'ALTER TABLE [MetadataSemantics] DROP CONSTRAINT ' + @var129 + ';');
    EXEC(N'UPDATE [MetadataSemantics] SET [Source] = N''Manual'' WHERE [Source] IS NULL');
    ALTER TABLE [MetadataSemantics] ALTER COLUMN [Source] nvarchar(16) NOT NULL;
    ALTER TABLE [MetadataSemantics] ADD DEFAULT N'Manual' FOR [Source];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    DECLARE @var130 nvarchar(max);
    SELECT @var130 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MetadataSemantics]') AND [c].[name] = N'Keywords');
    IF @var130 IS NOT NULL EXEC(N'ALTER TABLE [MetadataSemantics] DROP CONSTRAINT ' + @var130 + ';');
    ALTER TABLE [MetadataSemantics] ALTER COLUMN [Keywords] nvarchar(2048) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    DECLARE @var131 nvarchar(max);
    SELECT @var131 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MetadataSemantics]') AND [c].[name] = N'ExampleQuestions');
    IF @var131 IS NOT NULL EXEC(N'ALTER TABLE [MetadataSemantics] DROP CONSTRAINT ' + @var131 + ';');
    ALTER TABLE [MetadataSemantics] ALTER COLUMN [ExampleQuestions] nvarchar(2048) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    DECLARE @var132 nvarchar(max);
    SELECT @var132 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MetadataSemantics]') AND [c].[name] = N'EmbeddingModel');
    IF @var132 IS NOT NULL EXEC(N'ALTER TABLE [MetadataSemantics] DROP CONSTRAINT ' + @var132 + ';');
    ALTER TABLE [MetadataSemantics] ALTER COLUMN [EmbeddingModel] nvarchar(128) NULL;
    DECLARE @defaultSchema133 AS sysname;
    SET @defaultSchema133 = SCHEMA_NAME();
    DECLARE @description133 AS sql_variant;
    SET @description133 = N'Embedding模型';
    EXEC sp_addextendedproperty 'MS_Description', @description133, 'SCHEMA', @defaultSchema133, 'TABLE', N'MetadataSemantics', 'COLUMN', N'EmbeddingModel';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    DECLARE @defaultSchema134 AS sysname;
    SET @defaultSchema134 = SCHEMA_NAME();
    DECLARE @description134 AS sql_variant;
    SET @description134 = N'AI生成置信度(0-1)';
    EXEC sp_addextendedproperty 'MS_Description', @description134, 'SCHEMA', @defaultSchema134, 'TABLE', N'MetadataSemantics', 'COLUMN', N'Confidence';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    ALTER TABLE [MetadataSemantics] ADD [VectorErrorCode] nvarchar(64) NULL;
    DECLARE @defaultSchema135 AS sysname;
    SET @defaultSchema135 = SCHEMA_NAME();
    DECLARE @description135 AS sql_variant;
    SET @description135 = N'向量错误码';
    EXEC sp_addextendedproperty 'MS_Description', @description135, 'SCHEMA', @defaultSchema135, 'TABLE', N'MetadataSemantics', 'COLUMN', N'VectorErrorCode';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    ALTER TABLE [MetadataSemantics] ADD [VectorStatus] nvarchar(16) NULL;
    DECLARE @defaultSchema136 AS sysname;
    SET @defaultSchema136 = SCHEMA_NAME();
    DECLARE @description136 AS sql_variant;
    SET @description136 = N'向量状态';
    EXEC sp_addextendedproperty 'MS_Description', @description136, 'SCHEMA', @defaultSchema136, 'TABLE', N'MetadataSemantics', 'COLUMN', N'VectorStatus';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    ALTER TABLE [MetadataSemantics] ADD [VectorSyncTime] datetime2 NULL;
    DECLARE @defaultSchema137 AS sysname;
    SET @defaultSchema137 = SCHEMA_NAME();
    DECLARE @description137 AS sql_variant;
    SET @description137 = N'向量同步时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description137, 'SCHEMA', @defaultSchema137, 'TABLE', N'MetadataSemantics', 'COLUMN', N'VectorSyncTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    UPDATE [MetadataColumns] SET [ColumnName] = N'column_' + CAST([Id] AS NVARCHAR(20)) WHERE [ColumnName] IS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    DROP INDEX [IX_MetadataColumns_MetadataTableId_ColumnName] ON [MetadataColumns];
    DECLARE @var138 nvarchar(max);
    SELECT @var138 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MetadataColumns]') AND [c].[name] = N'ColumnName');
    IF @var138 IS NOT NULL EXEC(N'ALTER TABLE [MetadataColumns] DROP CONSTRAINT ' + @var138 + ';');
    ALTER TABLE [MetadataColumns] ALTER COLUMN [ColumnName] nvarchar(128) NOT NULL;
    DECLARE @defaultSchema139 AS sysname;
    SET @defaultSchema139 = SCHEMA_NAME();
    DECLARE @description139 AS sql_variant;
    SET @description139 = N'列名';
    EXEC sp_addextendedproperty 'MS_Description', @description139, 'SCHEMA', @defaultSchema139, 'TABLE', N'MetadataColumns', 'COLUMN', N'ColumnName';
    EXEC(N'CREATE UNIQUE INDEX [IX_MetadataColumns_MetadataTableId_ColumnName] ON [MetadataColumns] ([MetadataTableId], [ColumnName]) WHERE [MetadataTableId] IS NOT NULL AND [ColumnName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    ALTER TABLE [MetadataColumns] ADD [EmbeddingModel] nvarchar(128) NULL;
    DECLARE @defaultSchema140 AS sysname;
    SET @defaultSchema140 = SCHEMA_NAME();
    DECLARE @description140 AS sql_variant;
    SET @description140 = N'Embedding模型';
    EXEC sp_addextendedproperty 'MS_Description', @description140, 'SCHEMA', @defaultSchema140, 'TABLE', N'MetadataColumns', 'COLUMN', N'EmbeddingModel';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    ALTER TABLE [MetadataColumns] ADD [NativeType] nvarchar(64) NULL;
    DECLARE @defaultSchema141 AS sysname;
    SET @defaultSchema141 = SCHEMA_NAME();
    DECLARE @description141 AS sql_variant;
    SET @description141 = N'原生类型';
    EXEC sp_addextendedproperty 'MS_Description', @description141, 'SCHEMA', @defaultSchema141, 'TABLE', N'MetadataColumns', 'COLUMN', N'NativeType';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    ALTER TABLE [MetadataColumns] ADD [Ordinal] int NOT NULL DEFAULT 0;
    DECLARE @defaultSchema142 AS sysname;
    SET @defaultSchema142 = SCHEMA_NAME();
    DECLARE @description142 AS sql_variant;
    SET @description142 = N'列序号';
    EXEC sp_addextendedproperty 'MS_Description', @description142, 'SCHEMA', @defaultSchema142, 'TABLE', N'MetadataColumns', 'COLUMN', N'Ordinal';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    ALTER TABLE [MetadataColumns] ADD [Precision] int NULL;
    DECLARE @defaultSchema143 AS sysname;
    SET @defaultSchema143 = SCHEMA_NAME();
    DECLARE @description143 AS sql_variant;
    SET @description143 = N'精度';
    EXEC sp_addextendedproperty 'MS_Description', @description143, 'SCHEMA', @defaultSchema143, 'TABLE', N'MetadataColumns', 'COLUMN', N'Precision';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    ALTER TABLE [MetadataColumns] ADD [Scale] int NULL;
    DECLARE @defaultSchema144 AS sysname;
    SET @defaultSchema144 = SCHEMA_NAME();
    DECLARE @description144 AS sql_variant;
    SET @description144 = N'小数位';
    EXEC sp_addextendedproperty 'MS_Description', @description144, 'SCHEMA', @defaultSchema144, 'TABLE', N'MetadataColumns', 'COLUMN', N'Scale';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    ALTER TABLE [MetadataColumns] ADD [VectorDimension] int NULL;
    DECLARE @defaultSchema145 AS sysname;
    SET @defaultSchema145 = SCHEMA_NAME();
    DECLARE @description145 AS sql_variant;
    SET @description145 = N'向量维度';
    EXEC sp_addextendedproperty 'MS_Description', @description145, 'SCHEMA', @defaultSchema145, 'TABLE', N'MetadataColumns', 'COLUMN', N'VectorDimension';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    ALTER TABLE [MetadataColumns] ADD [VectorErrorCode] nvarchar(64) NULL;
    DECLARE @defaultSchema146 AS sysname;
    SET @defaultSchema146 = SCHEMA_NAME();
    DECLARE @description146 AS sql_variant;
    SET @description146 = N'向量错误码';
    EXEC sp_addextendedproperty 'MS_Description', @description146, 'SCHEMA', @defaultSchema146, 'TABLE', N'MetadataColumns', 'COLUMN', N'VectorErrorCode';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    ALTER TABLE [MetadataColumns] ADD [VectorStatus] nvarchar(16) NULL;
    DECLARE @defaultSchema147 AS sysname;
    SET @defaultSchema147 = SCHEMA_NAME();
    DECLARE @description147 AS sql_variant;
    SET @description147 = N'向量状态';
    EXEC sp_addextendedproperty 'MS_Description', @description147, 'SCHEMA', @defaultSchema147, 'TABLE', N'MetadataColumns', 'COLUMN', N'VectorStatus';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    ALTER TABLE [MetadataColumns] ADD [VectorSyncTime] datetime2 NULL;
    DECLARE @defaultSchema148 AS sysname;
    SET @defaultSchema148 = SCHEMA_NAME();
    DECLARE @description148 AS sql_variant;
    SET @description148 = N'向量同步时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description148, 'SCHEMA', @defaultSchema148, 'TABLE', N'MetadataColumns', 'COLUMN', N'VectorSyncTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    UPDATE [LearningRecords] SET [TenantId] = 1 WHERE [TenantId] IS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    DECLARE @var149 nvarchar(max);
    SELECT @var149 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[LearningRecords]') AND [c].[name] = N'TenantId');
    IF @var149 IS NOT NULL EXEC(N'ALTER TABLE [LearningRecords] DROP CONSTRAINT ' + @var149 + ';');
    ALTER TABLE [LearningRecords] ALTER COLUMN [TenantId] bigint NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    CREATE INDEX [IX_MetadataTables_DataSourceId] ON [MetadataTables] ([DataSourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MetadataTables_DataSourceId_CatalogName_SchemaName_TableName] ON [MetadataTables] ([DataSourceId], [CatalogName], [SchemaName], [TableName]) WHERE [CatalogName] IS NOT NULL AND [SchemaName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    EXEC(N'ALTER TABLE [MetadataSemantics] ADD CONSTRAINT [CK_MetadataSemantics_Confidence] CHECK ([Confidence] IS NULL OR ([Confidence] >= 0 AND [Confidence] <= 1))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    CREATE INDEX [IX_LearningRecords_MetadataColumnId] ON [LearningRecords] ([MetadataColumnId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    CREATE INDEX [IX_LearningRecords_TenantId] ON [LearningRecords] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    ALTER TABLE [LearningRecords] ADD CONSTRAINT [FK_LearningRecords_MetadataColumns_MetadataColumnId] FOREIGN KEY ([MetadataColumnId]) REFERENCES [MetadataColumns] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    ALTER TABLE [LearningRecords] ADD CONSTRAINT [FK_LearningRecords_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905004213_M1_05_MetadataVectorIntegrity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260905004213_M1_05_MetadataVectorIntegrity', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905020528_M1_06_PhysicalBindingAuthorizationRls'
)
BEGIN
    ALTER TABLE [PhysicalBindings] DROP CONSTRAINT [CK_PhysicalBindings_ExactlyOneOwner];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905020528_M1_06_PhysicalBindingAuthorizationRls'
)
BEGIN
    ALTER TABLE [MetadataSemantics] ADD [BusinessDomainId] bigint NULL;
    DECLARE @defaultSchema150 AS sysname;
    SET @defaultSchema150 = SCHEMA_NAME();
    DECLARE @description150 AS sql_variant;
    SET @description150 = N'业务域Id（FK 权威，逐步替代字符串 BusinessDomain）';
    EXEC sp_addextendedproperty 'MS_Description', @description150, 'SCHEMA', @defaultSchema150, 'TABLE', N'MetadataSemantics', 'COLUMN', N'BusinessDomainId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905020528_M1_06_PhysicalBindingAuthorizationRls'
)
BEGIN
    DECLARE @defaultSchema151 AS sysname;
    SET @defaultSchema151 = SCHEMA_NAME();
    DECLARE @description151 AS sql_variant;
    SET @description151 = N'业务域名称';
    EXEC sp_addextendedproperty 'MS_Description', @description151, 'SCHEMA', @defaultSchema151, 'TABLE', N'BusinessDomains', 'COLUMN', N'Name';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905020528_M1_06_PhysicalBindingAuthorizationRls'
)
BEGIN
    EXEC(N'ALTER TABLE [RowLevelSecurityPolicies] ADD CONSTRAINT [CK_RlsPolicies_Operator] CHECK ([Operator] IN (''='', ''!='', ''>'', ''>='', ''<'', ''<='', ''LIKE'', ''IN'', ''IS NULL'', ''IS NOT NULL''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905020528_M1_06_PhysicalBindingAuthorizationRls'
)
BEGIN
    EXEC(N'ALTER TABLE [RowLevelSecurityPolicies] ADD CONSTRAINT [CK_RlsPolicies_SubjectConsistency] CHECK (([SubjectType] = 0 AND [SubjectId] IS NULL AND [SubjectKey] IS NULL) OR ([SubjectType] = 1 AND [SubjectId] IS NOT NULL) OR ([SubjectType] = 2 AND [SubjectId] IS NOT NULL) OR ([SubjectType] = 3 AND [SubjectKey] IS NOT NULL) OR ([SubjectType] NOT IN (0,1,2,3)))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905020528_M1_06_PhysicalBindingAuthorizationRls'
)
BEGIN
    CREATE INDEX [IX_PhysicalBindings_BusinessEntityKeyId_BusinessEntityAttributeId_BusinessEntityMetricId_BusinessEntityRelationshipId] ON [PhysicalBindings] ([BusinessEntityKeyId], [BusinessEntityAttributeId], [BusinessEntityMetricId], [BusinessEntityRelationshipId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905020528_M1_06_PhysicalBindingAuthorizationRls'
)
BEGIN
    CREATE INDEX [IX_PhysicalBindings_DataSourceId_MetadataTableId_MetadataColumnId] ON [PhysicalBindings] ([DataSourceId], [MetadataTableId], [MetadataColumnId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905020528_M1_06_PhysicalBindingAuthorizationRls'
)
BEGIN
    EXEC(N'ALTER TABLE [PhysicalBindings] ADD CONSTRAINT [CK_PhysicalBindings_PriorityNonNeg] CHECK ([Priority] >= 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905020528_M1_06_PhysicalBindingAuthorizationRls'
)
BEGIN
    CREATE INDEX [IX_MetadataSemantics_BusinessDomainId] ON [MetadataSemantics] ([BusinessDomainId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905020528_M1_06_PhysicalBindingAuthorizationRls'
)
BEGIN
    CREATE INDEX [IX_BusinessDomains_TenantId] ON [BusinessDomains] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905020528_M1_06_PhysicalBindingAuthorizationRls'
)
BEGIN
    ALTER TABLE [DataSourceAccessGrants] ADD CONSTRAINT [FK_DataSourceAccessGrants_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905020528_M1_06_PhysicalBindingAuthorizationRls'
)
BEGIN
    ALTER TABLE [MetadataSemantics] ADD CONSTRAINT [FK_MetadataSemantics_BusinessDomains_BusinessDomainId] FOREIGN KEY ([BusinessDomainId]) REFERENCES [BusinessDomains] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905020528_M1_06_PhysicalBindingAuthorizationRls'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260905020528_M1_06_PhysicalBindingAuthorizationRls', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905030350_M2_02_PlatformAdminScopeAudit'
)
BEGIN
    ALTER TABLE [AuditLogs] ADD [CorrelationId] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905030350_M2_02_PlatformAdminScopeAudit'
)
BEGIN
    ALTER TABLE [AuditLogs] ADD [ManagementTargetTenantId] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905030350_M2_02_PlatformAdminScopeAudit'
)
BEGIN
    CREATE TABLE [PlatformAdminTenantScopes] (
        [Id] bigint NOT NULL IDENTITY,
        [AdminUserId] bigint NOT NULL,
        [TenantId] bigint NOT NULL,
        [GrantedAt] datetime2 NOT NULL,
        [GrantedBy] nvarchar(128) NULL,
        CONSTRAINT [PK_PlatformAdminTenantScopes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlatformAdminTenantScopes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PlatformAdminTenantScopes_Users_AdminUserId] FOREIGN KEY ([AdminUserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema152 AS sysname;
    SET @defaultSchema152 = SCHEMA_NAME();
    DECLARE @description152 AS sql_variant;
    SET @description152 = N'平台管理员租户范围绑定';
    EXEC sp_addextendedproperty 'MS_Description', @description152, 'SCHEMA', @defaultSchema152, 'TABLE', N'PlatformAdminTenantScopes';
    SET @description152 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description152, 'SCHEMA', @defaultSchema152, 'TABLE', N'PlatformAdminTenantScopes', 'COLUMN', N'Id';
    SET @description152 = N'授权时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description152, 'SCHEMA', @defaultSchema152, 'TABLE', N'PlatformAdminTenantScopes', 'COLUMN', N'GrantedAt';
    SET @description152 = N'授权操作者';
    EXEC sp_addextendedproperty 'MS_Description', @description152, 'SCHEMA', @defaultSchema152, 'TABLE', N'PlatformAdminTenantScopes', 'COLUMN', N'GrantedBy';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905030350_M2_02_PlatformAdminScopeAudit'
)
BEGIN
    CREATE INDEX [IX_PlatformAdminTenantScopes_AdminUserId] ON [PlatformAdminTenantScopes] ([AdminUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905030350_M2_02_PlatformAdminScopeAudit'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PlatformAdminTenantScopes_AdminUserId_TenantId] ON [PlatformAdminTenantScopes] ([AdminUserId], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905030350_M2_02_PlatformAdminScopeAudit'
)
BEGIN
    CREATE INDEX [IX_PlatformAdminTenantScopes_TenantId] ON [PlatformAdminTenantScopes] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905030350_M2_02_PlatformAdminScopeAudit'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260905030350_M2_02_PlatformAdminScopeAudit', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905035637_M2_05_UserTenant'
)
BEGIN
    CREATE TABLE [UserTenants] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [TenantId] bigint NOT NULL,
        [IsDefault] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedByUserId] bigint NULL,
        CONSTRAINT [PK_UserTenants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserTenants_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserTenants_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema153 AS sysname;
    SET @defaultSchema153 = SCHEMA_NAME();
    DECLARE @description153 AS sql_variant;
    SET @description153 = N'用户—租户成员关系（多租户切换）';
    EXEC sp_addextendedproperty 'MS_Description', @description153, 'SCHEMA', @defaultSchema153, 'TABLE', N'UserTenants';
    SET @description153 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description153, 'SCHEMA', @defaultSchema153, 'TABLE', N'UserTenants', 'COLUMN', N'Id';
    SET @description153 = N'操作者用户 Id（管理员代加成员）';
    EXEC sp_addextendedproperty 'MS_Description', @description153, 'SCHEMA', @defaultSchema153, 'TABLE', N'UserTenants', 'COLUMN', N'CreatedByUserId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905035637_M2_05_UserTenant'
)
BEGIN
    CREATE INDEX [IX_UserTenants_TenantId] ON [UserTenants] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905035637_M2_05_UserTenant'
)
BEGIN
    CREATE INDEX [IX_UserTenants_UserId] ON [UserTenants] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905035637_M2_05_UserTenant'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserTenants_UserId_TenantId] ON [UserTenants] ([UserId], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905035637_M2_05_UserTenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260905035637_M2_05_UserTenant', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905060434_M3G0UserLangPref'
)
BEGIN
    CREATE TABLE [UserLanguagePreferences] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [UserId] bigint NOT NULL,
        [Culture] nvarchar(16) NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_UserLanguagePreferences] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema154 AS sysname;
    SET @defaultSchema154 = SCHEMA_NAME();
    DECLARE @description154 AS sql_variant;
    SET @description154 = N'用户界面语言偏好';
    EXEC sp_addextendedproperty 'MS_Description', @description154, 'SCHEMA', @defaultSchema154, 'TABLE', N'UserLanguagePreferences';
    SET @description154 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description154, 'SCHEMA', @defaultSchema154, 'TABLE', N'UserLanguagePreferences', 'COLUMN', N'Id';
    SET @description154 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description154, 'SCHEMA', @defaultSchema154, 'TABLE', N'UserLanguagePreferences', 'COLUMN', N'CreatedTime';
    SET @description154 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description154, 'SCHEMA', @defaultSchema154, 'TABLE', N'UserLanguagePreferences', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905060434_M3G0UserLangPref'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserLanguagePreferences_TenantId_UserId] ON [UserLanguagePreferences] ([TenantId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905060434_M3G0UserLangPref'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260905060434_M3G0UserLangPref', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905064209_M3_01_TenantUiLanguage'
)
BEGIN
    CREATE TABLE [TenantUiLanguages] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [UiLanguageId] bigint NOT NULL,
        [Enabled] bit NOT NULL DEFAULT CAST(1 AS bit),
        [IsDefault] bit NOT NULL DEFAULT CAST(0 AS bit),
        [SortOrder] int NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_TenantUiLanguages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantUiLanguages_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TenantUiLanguages_UiLanguages_UiLanguageId] FOREIGN KEY ([UiLanguageId]) REFERENCES [UiLanguages] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @defaultSchema155 AS sysname;
    SET @defaultSchema155 = SCHEMA_NAME();
    DECLARE @description155 AS sql_variant;
    SET @description155 = N'租户界面语言关系（替代 localization:availableCultures/defaultCulture JSON）';
    EXEC sp_addextendedproperty 'MS_Description', @description155, 'SCHEMA', @defaultSchema155, 'TABLE', N'TenantUiLanguages';
    SET @description155 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description155, 'SCHEMA', @defaultSchema155, 'TABLE', N'TenantUiLanguages', 'COLUMN', N'Id';
    SET @description155 = N'所属租户';
    EXEC sp_addextendedproperty 'MS_Description', @description155, 'SCHEMA', @defaultSchema155, 'TABLE', N'TenantUiLanguages', 'COLUMN', N'TenantId';
    SET @description155 = N'平台语言目录 Id';
    EXEC sp_addextendedproperty 'MS_Description', @description155, 'SCHEMA', @defaultSchema155, 'TABLE', N'TenantUiLanguages', 'COLUMN', N'UiLanguageId';
    SET @description155 = N'是否启用（租户范围内）';
    EXEC sp_addextendedproperty 'MS_Description', @description155, 'SCHEMA', @defaultSchema155, 'TABLE', N'TenantUiLanguages', 'COLUMN', N'Enabled';
    SET @description155 = N'是否为租户默认语言（每租户恰一个）';
    EXEC sp_addextendedproperty 'MS_Description', @description155, 'SCHEMA', @defaultSchema155, 'TABLE', N'TenantUiLanguages', 'COLUMN', N'IsDefault';
    SET @description155 = N'排序';
    EXEC sp_addextendedproperty 'MS_Description', @description155, 'SCHEMA', @defaultSchema155, 'TABLE', N'TenantUiLanguages', 'COLUMN', N'SortOrder';
    SET @description155 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description155, 'SCHEMA', @defaultSchema155, 'TABLE', N'TenantUiLanguages', 'COLUMN', N'CreatedTime';
    SET @description155 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description155, 'SCHEMA', @defaultSchema155, 'TABLE', N'TenantUiLanguages', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905064209_M3_01_TenantUiLanguage'
)
BEGIN
    CREATE INDEX [IX_TenantUiLanguages_TenantId_IsDefault] ON [TenantUiLanguages] ([TenantId], [IsDefault]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905064209_M3_01_TenantUiLanguage'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TenantUiLanguages_TenantId_UiLanguageId] ON [TenantUiLanguages] ([TenantId], [UiLanguageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905064209_M3_01_TenantUiLanguage'
)
BEGIN
    CREATE INDEX [IX_TenantUiLanguages_UiLanguageId] ON [TenantUiLanguages] ([UiLanguageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905064209_M3_01_TenantUiLanguage'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260905064209_M3_01_TenantUiLanguage', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905072839_M3_02_UiTextResource_IsTranslated'
)
BEGIN
    ALTER TABLE [UiTextResources] ADD [IsTranslated] bit NOT NULL DEFAULT CAST(1 AS bit);
    DECLARE @defaultSchema156 AS sysname;
    SET @defaultSchema156 = SCHEMA_NAME();
    DECLARE @description156 AS sql_variant;
    SET @description156 = N'是否已翻译（新建语言从其它语言复制键集合时标记待翻译）';
    EXEC sp_addextendedproperty 'MS_Description', @description156, 'SCHEMA', @defaultSchema156, 'TABLE', N'UiTextResources', 'COLUMN', N'IsTranslated';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905072839_M3_02_UiTextResource_IsTranslated'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260905072839_M3_02_UiTextResource_IsTranslated', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905233450_M4_01_DataSourceScanAt'
)
BEGIN
    ALTER TABLE [DataSources] ADD [LastScanAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905233450_M4_01_DataSourceScanAt'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260905233450_M4_01_DataSourceScanAt', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906000108_M4_05_ScanJob'
)
BEGIN
    CREATE TABLE [MetadataScanJobs] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [DataSourceId] bigint NOT NULL,
        [TriggeredBy] nvarchar(64) NULL,
        [Status] nvarchar(16) NOT NULL DEFAULT N'Queued',
        [ProgressPercent] int NOT NULL DEFAULT 0,
        [StartedAt] datetime2 NULL,
        [FinishedAt] datetime2 NULL,
        [TablesScanned] int NOT NULL DEFAULT 0,
        [ColumnsScanned] int NOT NULL DEFAULT 0,
        [OrphansDetected] int NOT NULL DEFAULT 0,
        [ErrorCode] nvarchar(64) NULL,
        [ErrorMessage] nvarchar(2000) NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_MetadataScanJobs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MetadataScanJobs_DataSources_DataSourceId] FOREIGN KEY ([DataSourceId]) REFERENCES [DataSources] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema157 AS sysname;
    SET @defaultSchema157 = SCHEMA_NAME();
    DECLARE @description157 AS sql_variant;
    SET @description157 = N'元数据扫描任务';
    EXEC sp_addextendedproperty 'MS_Description', @description157, 'SCHEMA', @defaultSchema157, 'TABLE', N'MetadataScanJobs';
    SET @description157 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description157, 'SCHEMA', @defaultSchema157, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'Id';
    SET @description157 = N'触发用户标识';
    EXEC sp_addextendedproperty 'MS_Description', @description157, 'SCHEMA', @defaultSchema157, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'TriggeredBy';
    SET @description157 = N'扫描状态';
    EXEC sp_addextendedproperty 'MS_Description', @description157, 'SCHEMA', @defaultSchema157, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'Status';
    SET @description157 = N'进度百分比';
    EXEC sp_addextendedproperty 'MS_Description', @description157, 'SCHEMA', @defaultSchema157, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'ProgressPercent';
    SET @description157 = N'开始时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description157, 'SCHEMA', @defaultSchema157, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'StartedAt';
    SET @description157 = N'结束时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description157, 'SCHEMA', @defaultSchema157, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'FinishedAt';
    SET @description157 = N'已扫描表数';
    EXEC sp_addextendedproperty 'MS_Description', @description157, 'SCHEMA', @defaultSchema157, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'TablesScanned';
    SET @description157 = N'已扫描字段数';
    EXEC sp_addextendedproperty 'MS_Description', @description157, 'SCHEMA', @defaultSchema157, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'ColumnsScanned';
    SET @description157 = N'孤儿对象数';
    EXEC sp_addextendedproperty 'MS_Description', @description157, 'SCHEMA', @defaultSchema157, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'OrphansDetected';
    SET @description157 = N'错误码(仅异常类型名,脱敏)';
    EXEC sp_addextendedproperty 'MS_Description', @description157, 'SCHEMA', @defaultSchema157, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'ErrorCode';
    SET @description157 = N'错误摘要(脱敏,不含连接串)';
    EXEC sp_addextendedproperty 'MS_Description', @description157, 'SCHEMA', @defaultSchema157, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'ErrorMessage';
    SET @description157 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description157, 'SCHEMA', @defaultSchema157, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'CreatedTime';
    SET @description157 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description157, 'SCHEMA', @defaultSchema157, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906000108_M4_05_ScanJob'
)
BEGIN
    CREATE INDEX [IX_MetadataScanJobs_DataSourceId] ON [MetadataScanJobs] ([DataSourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906000108_M4_05_ScanJob'
)
BEGIN
    CREATE INDEX [IX_MetadataScanJobs_Status] ON [MetadataScanJobs] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906000108_M4_05_ScanJob'
)
BEGIN
    CREATE INDEX [IX_MetadataScanJobs_TenantId] ON [MetadataScanJobs] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906000108_M4_05_ScanJob'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260906000108_M4_05_ScanJob', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    IF EXISTS (
        SELECT 1
        FROM [DataSources] AS ds
        LEFT JOIN [Tenants] AS t ON t.[Id] = ds.[TenantId]
        WHERE ds.[TenantId] IS NULL OR t.[Id] IS NULL)
        THROW 51001, 'M1_ClosureIntegrity: DataSources contain a missing or orphan TenantId.', 1;

    IF EXISTS (
        SELECT 1
        FROM [Users] AS u
        LEFT JOIN [Tenants] AS t ON t.[Id] = u.[TenantId]
        WHERE t.[Id] IS NULL)
        THROW 51002, 'M1_ClosureIntegrity: Users contain an orphan TenantId.', 1;

    IF EXISTS (
        SELECT 1
        FROM [MetadataTables] AS mt
        LEFT JOIN [Tenants] AS t ON t.[Id] = mt.[TenantId]
        LEFT JOIN [DataSources] AS ds ON ds.[Id] = mt.[DataSourceId]
        WHERE t.[Id] IS NULL OR ds.[Id] IS NULL)
        THROW 51003, 'M1_ClosureIntegrity: MetadataTables contain an orphan TenantId or DataSourceId.', 1;

    IF EXISTS (
        SELECT 1
        FROM [MetadataTables] AS mt
        INNER JOIN [DataSources] AS ds ON ds.[Id] = mt.[DataSourceId]
        WHERE mt.[TenantId] <> ds.[TenantId])
        THROW 51004, 'M1_ClosureIntegrity: MetadataTable and DataSource tenant ownership does not match.', 1;

    IF EXISTS (
        SELECT 1
        FROM [MetadataColumns] AS mc
        LEFT JOIN [MetadataTables] AS mt ON mt.[Id] = mc.[MetadataTableId]
        WHERE mc.[MetadataTableId] IS NULL OR mt.[Id] IS NULL)
        THROW 51005, 'M1_ClosureIntegrity: MetadataColumns contain a missing or orphan MetadataTableId.', 1;

    IF EXISTS (
        SELECT 1
        FROM [DataSources]
        WHERE [DbType] IS NULL
           OR UPPER(LTRIM(RTRIM([DbType]))) NOT IN ('MYSQL', 'SQLSERVER', 'POSTGRESQL'))
        THROW 51006, 'M1_ClosureIntegrity: DataSources contain a missing or unsupported DbType.', 1;

    IF EXISTS (SELECT 1 FROM [DataSources] WHERE [ConnectionString] IS NULL)
        THROW 51007, 'M1_ClosureIntegrity: DataSources contain a NULL ConnectionString; supply or disable/remove the source before retrying.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    UPDATE [Tenants]
    SET [TenantCode] = CONCAT('__legacy_tenant_', [Id])
    WHERE [TenantCode] IS NULL OR LEN(LTRIM(RTRIM([TenantCode]))) = 0;

    UPDATE [Tenants]
    SET [TenantName] = CONCAT('Legacy Tenant ', [Id])
    WHERE [TenantName] IS NULL OR LEN(LTRIM(RTRIM([TenantName]))) = 0;

    UPDATE [Users]
    SET [SecurityStamp] = LOWER(REPLACE(CONVERT(nvarchar(36), NEWID()), '-', ''))
    WHERE [SecurityStamp] IS NULL OR LEN(LTRIM(RTRIM([SecurityStamp]))) = 0;

    UPDATE [DataSources]
    SET [Name] = CONCAT('Legacy DataSource ', [Id])
    WHERE [Name] IS NULL OR LEN(LTRIM(RTRIM([Name]))) = 0;

    UPDATE [DataSources]
    SET [NormalizedName] = CONCAT('__legacy_datasource_', [Id])
    WHERE [NormalizedName] IS NULL OR LEN(LTRIM(RTRIM([NormalizedName]))) = 0;

    UPDATE [DataSources]
    SET [DbType] = UPPER(LTRIM(RTRIM([DbType])));
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    ALTER TABLE [DataSources] DROP CONSTRAINT [FK_DataSources_Tenants_TenantId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    ALTER TABLE [MetadataColumns] DROP CONSTRAINT [FK_MetadataColumns_MetadataTables_MetadataTableId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    ALTER TABLE [MetadataTables] DROP CONSTRAINT [FK_MetadataTables_DataSources_DataSourceId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    DROP INDEX [IX_Tenants_TenantCode] ON [Tenants];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    DROP INDEX [IX_MetadataColumns_MetadataTableId_ColumnName] ON [MetadataColumns];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    DROP INDEX [IX_DataSources_TenantId_NormalizedName] ON [DataSources];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    DECLARE @var158 nvarchar(max);
    SELECT @var158 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'SecurityStamp');
    IF @var158 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT ' + @var158 + ';');
    ALTER TABLE [Users] ALTER COLUMN [SecurityStamp] nvarchar(64) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    DECLARE @var159 nvarchar(max);
    SELECT @var159 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tenants]') AND [c].[name] = N'TenantName');
    IF @var159 IS NOT NULL EXEC(N'ALTER TABLE [Tenants] DROP CONSTRAINT ' + @var159 + ';');
    ALTER TABLE [Tenants] ALTER COLUMN [TenantName] nvarchar(128) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    DECLARE @var160 nvarchar(max);
    SELECT @var160 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tenants]') AND [c].[name] = N'TenantCode');
    IF @var160 IS NOT NULL EXEC(N'ALTER TABLE [Tenants] DROP CONSTRAINT ' + @var160 + ';');
    ALTER TABLE [Tenants] ALTER COLUMN [TenantCode] nvarchar(64) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    DECLARE @var161 nvarchar(max);
    SELECT @var161 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MetadataColumns]') AND [c].[name] = N'MetadataTableId');
    IF @var161 IS NOT NULL EXEC(N'ALTER TABLE [MetadataColumns] DROP CONSTRAINT ' + @var161 + ';');
    ALTER TABLE [MetadataColumns] ALTER COLUMN [MetadataTableId] bigint NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    DROP INDEX [IX_DataSources_TenantId] ON [DataSources];
    DECLARE @var162 nvarchar(max);
    SELECT @var162 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DataSources]') AND [c].[name] = N'TenantId');
    IF @var162 IS NOT NULL EXEC(N'ALTER TABLE [DataSources] DROP CONSTRAINT ' + @var162 + ';');
    ALTER TABLE [DataSources] ALTER COLUMN [TenantId] bigint NOT NULL;
    CREATE INDEX [IX_DataSources_TenantId] ON [DataSources] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    DECLARE @var163 nvarchar(max);
    SELECT @var163 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DataSources]') AND [c].[name] = N'NormalizedName');
    IF @var163 IS NOT NULL EXEC(N'ALTER TABLE [DataSources] DROP CONSTRAINT ' + @var163 + ';');
    ALTER TABLE [DataSources] ALTER COLUMN [NormalizedName] nvarchar(128) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    DECLARE @var164 nvarchar(max);
    SELECT @var164 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DataSources]') AND [c].[name] = N'Name');
    IF @var164 IS NOT NULL EXEC(N'ALTER TABLE [DataSources] DROP CONSTRAINT ' + @var164 + ';');
    ALTER TABLE [DataSources] ALTER COLUMN [Name] nvarchar(128) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    DECLARE @var165 nvarchar(max);
    SELECT @var165 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DataSources]') AND [c].[name] = N'DbType');
    IF @var165 IS NOT NULL EXEC(N'ALTER TABLE [DataSources] DROP CONSTRAINT ' + @var165 + ';');
    ALTER TABLE [DataSources] ALTER COLUMN [DbType] nvarchar(32) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    DECLARE @var166 nvarchar(max);
    SELECT @var166 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DataSources]') AND [c].[name] = N'ConnectionString');
    IF @var166 IS NOT NULL EXEC(N'ALTER TABLE [DataSources] DROP CONSTRAINT ' + @var166 + ';');
    ALTER TABLE [DataSources] ALTER COLUMN [ConnectionString] nvarchar(2048) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    ALTER TABLE [DataSources] ADD CONSTRAINT [AK_DataSources_Id_TenantId] UNIQUE ([Id], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tenants_TenantCode] ON [Tenants] ([TenantCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    CREATE INDEX [IX_MetadataTables_DataSourceId_TenantId] ON [MetadataTables] ([DataSourceId], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    CREATE INDEX [IX_MetadataTables_TenantId] ON [MetadataTables] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MetadataColumns_MetadataTableId_ColumnName] ON [MetadataColumns] ([MetadataTableId], [ColumnName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DataSources_TenantId_NormalizedName] ON [DataSources] ([TenantId], [NormalizedName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    ALTER TABLE [DataSources] ADD CONSTRAINT [FK_DataSources_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    ALTER TABLE [MetadataColumns] ADD CONSTRAINT [FK_MetadataColumns_MetadataTables_MetadataTableId] FOREIGN KEY ([MetadataTableId]) REFERENCES [MetadataTables] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    ALTER TABLE [MetadataTables] ADD CONSTRAINT [FK_MetadataTables_DataSources_DataSourceId_TenantId] FOREIGN KEY ([DataSourceId], [TenantId]) REFERENCES [DataSources] ([Id], [TenantId]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    ALTER TABLE [MetadataTables] ADD CONSTRAINT [FK_MetadataTables_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906075147_M1_ClosureIntegrity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260906075147_M1_ClosureIntegrity', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906131855_M7_01_DashboardVersion'
)
BEGIN
    ALTER TABLE [Dashboards] ADD [PublishedAt] datetime2 NULL;
    DECLARE @defaultSchema167 AS sysname;
    SET @defaultSchema167 = SCHEMA_NAME();
    DECLARE @description167 AS sql_variant;
    SET @description167 = N'最近发布时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description167, 'SCHEMA', @defaultSchema167, 'TABLE', N'Dashboards', 'COLUMN', N'PublishedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906131855_M7_01_DashboardVersion'
)
BEGIN
    ALTER TABLE [Dashboards] ADD [PublishedBy] nvarchar(128) NULL;
    DECLARE @defaultSchema168 AS sysname;
    SET @defaultSchema168 = SCHEMA_NAME();
    DECLARE @description168 AS sql_variant;
    SET @description168 = N'最近发布者';
    EXEC sp_addextendedproperty 'MS_Description', @description168, 'SCHEMA', @defaultSchema168, 'TABLE', N'Dashboards', 'COLUMN', N'PublishedBy';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906131855_M7_01_DashboardVersion'
)
BEGIN
    ALTER TABLE [Dashboards] ADD [PublishedDslJson] nvarchar(max) NULL;
    DECLARE @defaultSchema169 AS sysname;
    SET @defaultSchema169 = SCHEMA_NAME();
    DECLARE @description169 AS sql_variant;
    SET @description169 = N'已发布DSL快照（null=未发布）';
    EXEC sp_addextendedproperty 'MS_Description', @description169, 'SCHEMA', @defaultSchema169, 'TABLE', N'Dashboards', 'COLUMN', N'PublishedDslJson';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906131855_M7_01_DashboardVersion'
)
BEGIN
    ALTER TABLE [Dashboards] ADD [PublishedVersion] int NOT NULL DEFAULT 0;
    DECLARE @defaultSchema170 AS sysname;
    SET @defaultSchema170 = SCHEMA_NAME();
    DECLARE @description170 AS sql_variant;
    SET @description170 = N'当前发布版本号（0=未发布）';
    EXEC sp_addextendedproperty 'MS_Description', @description170, 'SCHEMA', @defaultSchema170, 'TABLE', N'Dashboards', 'COLUMN', N'PublishedVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906131855_M7_01_DashboardVersion'
)
BEGIN
    CREATE TABLE [DashboardVersions] (
        [Id] bigint NOT NULL IDENTITY,
        [DashboardId] bigint NOT NULL,
        [TenantId] bigint NOT NULL,
        [Version] int NOT NULL,
        [Code] nvarchar(128) NOT NULL,
        [Title] nvarchar(256) NOT NULL,
        [Description] nvarchar(1024) NULL,
        [ThemeKey] nvarchar(64) NULL,
        [DslVersion] nvarchar(16) NOT NULL,
        [DslJson] nvarchar(max) NOT NULL,
        [PublishedAt] datetime2 NOT NULL,
        [PublishedBy] nvarchar(128) NULL,
        [RolledBackFromVersion] int NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_DashboardVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DashboardVersions_Dashboards_DashboardId] FOREIGN KEY ([DashboardId]) REFERENCES [Dashboards] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema171 AS sysname;
    SET @defaultSchema171 = SCHEMA_NAME();
    DECLARE @description171 AS sql_variant;
    SET @description171 = N'仪表盘发布版本快照';
    EXEC sp_addextendedproperty 'MS_Description', @description171, 'SCHEMA', @defaultSchema171, 'TABLE', N'DashboardVersions';
    SET @description171 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description171, 'SCHEMA', @defaultSchema171, 'TABLE', N'DashboardVersions', 'COLUMN', N'Id';
    SET @description171 = N'作用域租户（0=全局模板）';
    EXEC sp_addextendedproperty 'MS_Description', @description171, 'SCHEMA', @defaultSchema171, 'TABLE', N'DashboardVersions', 'COLUMN', N'TenantId';
    SET @description171 = N'业务编码快照';
    EXEC sp_addextendedproperty 'MS_Description', @description171, 'SCHEMA', @defaultSchema171, 'TABLE', N'DashboardVersions', 'COLUMN', N'Code';
    SET @description171 = N'标题快照';
    EXEC sp_addextendedproperty 'MS_Description', @description171, 'SCHEMA', @defaultSchema171, 'TABLE', N'DashboardVersions', 'COLUMN', N'Title';
    SET @description171 = N'描述快照';
    EXEC sp_addextendedproperty 'MS_Description', @description171, 'SCHEMA', @defaultSchema171, 'TABLE', N'DashboardVersions', 'COLUMN', N'Description';
    SET @description171 = N'主题键快照';
    EXEC sp_addextendedproperty 'MS_Description', @description171, 'SCHEMA', @defaultSchema171, 'TABLE', N'DashboardVersions', 'COLUMN', N'ThemeKey';
    SET @description171 = N'DSL版本快照';
    EXEC sp_addextendedproperty 'MS_Description', @description171, 'SCHEMA', @defaultSchema171, 'TABLE', N'DashboardVersions', 'COLUMN', N'DslVersion';
    SET @description171 = N'发布时刻固化的DSL文档（只读快照）';
    EXEC sp_addextendedproperty 'MS_Description', @description171, 'SCHEMA', @defaultSchema171, 'TABLE', N'DashboardVersions', 'COLUMN', N'DslJson';
    SET @description171 = N'发布者';
    EXEC sp_addextendedproperty 'MS_Description', @description171, 'SCHEMA', @defaultSchema171, 'TABLE', N'DashboardVersions', 'COLUMN', N'PublishedBy';
    SET @description171 = N'回滚来源版本号（非回滚为null）';
    EXEC sp_addextendedproperty 'MS_Description', @description171, 'SCHEMA', @defaultSchema171, 'TABLE', N'DashboardVersions', 'COLUMN', N'RolledBackFromVersion';
    SET @description171 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description171, 'SCHEMA', @defaultSchema171, 'TABLE', N'DashboardVersions', 'COLUMN', N'CreatedTime';
    SET @description171 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description171, 'SCHEMA', @defaultSchema171, 'TABLE', N'DashboardVersions', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906131855_M7_01_DashboardVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DashboardVersions_DashboardId_Version] ON [DashboardVersions] ([DashboardId], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906131855_M7_01_DashboardVersion'
)
BEGIN
    CREATE INDEX [IX_DashboardVersions_TenantId_DashboardId] ON [DashboardVersions] ([TenantId], [DashboardId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906131855_M7_01_DashboardVersion'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260906131855_M7_01_DashboardVersion', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [AgentPlans] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Code] nvarchar(128) NOT NULL,
        [Name] nvarchar(256) NOT NULL,
        [Description] nvarchar(1024) NULL,
        [Status] nvarchar(32) NOT NULL,
        [DslVersion] nvarchar(16) NOT NULL,
        [DslJson] nvarchar(max) NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_AgentPlans] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema172 AS sysname;
    SET @defaultSchema172 = SCHEMA_NAME();
    DECLARE @description172 AS sql_variant;
    SET @description172 = N'Agent计划';
    EXEC sp_addextendedproperty 'MS_Description', @description172, 'SCHEMA', @defaultSchema172, 'TABLE', N'AgentPlans';
    SET @description172 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description172, 'SCHEMA', @defaultSchema172, 'TABLE', N'AgentPlans', 'COLUMN', N'Id';
    SET @description172 = N'所属租户（0=全局模板）';
    EXEC sp_addextendedproperty 'MS_Description', @description172, 'SCHEMA', @defaultSchema172, 'TABLE', N'AgentPlans', 'COLUMN', N'TenantId';
    SET @description172 = N'业务编码';
    EXEC sp_addextendedproperty 'MS_Description', @description172, 'SCHEMA', @defaultSchema172, 'TABLE', N'AgentPlans', 'COLUMN', N'Code';
    SET @description172 = N'名称';
    EXEC sp_addextendedproperty 'MS_Description', @description172, 'SCHEMA', @defaultSchema172, 'TABLE', N'AgentPlans', 'COLUMN', N'Name';
    SET @description172 = N'描述';
    EXEC sp_addextendedproperty 'MS_Description', @description172, 'SCHEMA', @defaultSchema172, 'TABLE', N'AgentPlans', 'COLUMN', N'Description';
    SET @description172 = N'状态';
    EXEC sp_addextendedproperty 'MS_Description', @description172, 'SCHEMA', @defaultSchema172, 'TABLE', N'AgentPlans', 'COLUMN', N'Status';
    SET @description172 = N'DSL版本';
    EXEC sp_addextendedproperty 'MS_Description', @description172, 'SCHEMA', @defaultSchema172, 'TABLE', N'AgentPlans', 'COLUMN', N'DslVersion';
    SET @description172 = N'DSL文档（结构化，非裸HTML）';
    EXEC sp_addextendedproperty 'MS_Description', @description172, 'SCHEMA', @defaultSchema172, 'TABLE', N'AgentPlans', 'COLUMN', N'DslJson';
    SET @description172 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description172, 'SCHEMA', @defaultSchema172, 'TABLE', N'AgentPlans', 'COLUMN', N'CreatedTime';
    SET @description172 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description172, 'SCHEMA', @defaultSchema172, 'TABLE', N'AgentPlans', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [AppPlans] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Code] nvarchar(128) NOT NULL,
        [Name] nvarchar(256) NOT NULL,
        [Description] nvarchar(1024) NULL,
        [Status] nvarchar(32) NOT NULL,
        [DslVersion] nvarchar(16) NOT NULL,
        [DslJson] nvarchar(max) NOT NULL,
        [ThemeKey] nvarchar(64) NULL,
        [PublishedDslJson] nvarchar(max) NULL,
        [PublishedVersion] int NOT NULL,
        [PublishedAt] datetime2 NULL,
        [PublishedBy] nvarchar(128) NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_AppPlans] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema173 AS sysname;
    SET @defaultSchema173 = SCHEMA_NAME();
    DECLARE @description173 AS sql_variant;
    SET @description173 = N'应用';
    EXEC sp_addextendedproperty 'MS_Description', @description173, 'SCHEMA', @defaultSchema173, 'TABLE', N'AppPlans';
    SET @description173 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description173, 'SCHEMA', @defaultSchema173, 'TABLE', N'AppPlans', 'COLUMN', N'Id';
    SET @description173 = N'所属租户（0=全局模板）';
    EXEC sp_addextendedproperty 'MS_Description', @description173, 'SCHEMA', @defaultSchema173, 'TABLE', N'AppPlans', 'COLUMN', N'TenantId';
    SET @description173 = N'业务编码';
    EXEC sp_addextendedproperty 'MS_Description', @description173, 'SCHEMA', @defaultSchema173, 'TABLE', N'AppPlans', 'COLUMN', N'Code';
    SET @description173 = N'名称';
    EXEC sp_addextendedproperty 'MS_Description', @description173, 'SCHEMA', @defaultSchema173, 'TABLE', N'AppPlans', 'COLUMN', N'Name';
    SET @description173 = N'描述';
    EXEC sp_addextendedproperty 'MS_Description', @description173, 'SCHEMA', @defaultSchema173, 'TABLE', N'AppPlans', 'COLUMN', N'Description';
    SET @description173 = N'状态';
    EXEC sp_addextendedproperty 'MS_Description', @description173, 'SCHEMA', @defaultSchema173, 'TABLE', N'AppPlans', 'COLUMN', N'Status';
    SET @description173 = N'DSL版本';
    EXEC sp_addextendedproperty 'MS_Description', @description173, 'SCHEMA', @defaultSchema173, 'TABLE', N'AppPlans', 'COLUMN', N'DslVersion';
    SET @description173 = N'DSL文档（结构化，非裸HTML）';
    EXEC sp_addextendedproperty 'MS_Description', @description173, 'SCHEMA', @defaultSchema173, 'TABLE', N'AppPlans', 'COLUMN', N'DslJson';
    SET @description173 = N'主题键';
    EXEC sp_addextendedproperty 'MS_Description', @description173, 'SCHEMA', @defaultSchema173, 'TABLE', N'AppPlans', 'COLUMN', N'ThemeKey';
    SET @description173 = N'已发布DSL快照（null=未发布）';
    EXEC sp_addextendedproperty 'MS_Description', @description173, 'SCHEMA', @defaultSchema173, 'TABLE', N'AppPlans', 'COLUMN', N'PublishedDslJson';
    SET @description173 = N'当前发布版本号（0=未发布）';
    EXEC sp_addextendedproperty 'MS_Description', @description173, 'SCHEMA', @defaultSchema173, 'TABLE', N'AppPlans', 'COLUMN', N'PublishedVersion';
    SET @description173 = N'最近发布时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description173, 'SCHEMA', @defaultSchema173, 'TABLE', N'AppPlans', 'COLUMN', N'PublishedAt';
    SET @description173 = N'最近发布者';
    EXEC sp_addextendedproperty 'MS_Description', @description173, 'SCHEMA', @defaultSchema173, 'TABLE', N'AppPlans', 'COLUMN', N'PublishedBy';
    SET @description173 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description173, 'SCHEMA', @defaultSchema173, 'TABLE', N'AppPlans', 'COLUMN', N'CreatedTime';
    SET @description173 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description173, 'SCHEMA', @defaultSchema173, 'TABLE', N'AppPlans', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [UserId] bigint NULL,
        [Actor] nvarchar(128) NOT NULL,
        [Action] nvarchar(128) NOT NULL,
        [EntityType] nvarchar(128) NOT NULL,
        [EntityId] nvarchar(256) NULL,
        [BeforeJson] nvarchar(max) NULL,
        [AfterJson] nvarchar(max) NULL,
        [Result] nvarchar(32) NOT NULL,
        [Message] nvarchar(max) NULL,
        [ManagementTargetTenantId] bigint NULL,
        [CorrelationId] nvarchar(max) NULL,
        [Timestamp] datetime2 NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema174 AS sysname;
    SET @defaultSchema174 = SCHEMA_NAME();
    DECLARE @description174 AS sql_variant;
    SET @description174 = N'审计日志';
    EXEC sp_addextendedproperty 'MS_Description', @description174, 'SCHEMA', @defaultSchema174, 'TABLE', N'AuditLogs';
    SET @description174 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description174, 'SCHEMA', @defaultSchema174, 'TABLE', N'AuditLogs', 'COLUMN', N'Id';
    SET @description174 = N'所属租户（0=平台级）';
    EXEC sp_addextendedproperty 'MS_Description', @description174, 'SCHEMA', @defaultSchema174, 'TABLE', N'AuditLogs', 'COLUMN', N'TenantId';
    SET @description174 = N'操作者标识';
    EXEC sp_addextendedproperty 'MS_Description', @description174, 'SCHEMA', @defaultSchema174, 'TABLE', N'AuditLogs', 'COLUMN', N'Actor';
    SET @description174 = N'动作类型';
    EXEC sp_addextendedproperty 'MS_Description', @description174, 'SCHEMA', @defaultSchema174, 'TABLE', N'AuditLogs', 'COLUMN', N'Action';
    SET @description174 = N'实体类型';
    EXEC sp_addextendedproperty 'MS_Description', @description174, 'SCHEMA', @defaultSchema174, 'TABLE', N'AuditLogs', 'COLUMN', N'EntityType';
    SET @description174 = N'实体Id';
    EXEC sp_addextendedproperty 'MS_Description', @description174, 'SCHEMA', @defaultSchema174, 'TABLE', N'AuditLogs', 'COLUMN', N'EntityId';
    SET @description174 = N'结果';
    EXEC sp_addextendedproperty 'MS_Description', @description174, 'SCHEMA', @defaultSchema174, 'TABLE', N'AuditLogs', 'COLUMN', N'Result';
    SET @description174 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description174, 'SCHEMA', @defaultSchema174, 'TABLE', N'AuditLogs', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [Dashboards] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Code] nvarchar(128) NOT NULL,
        [Title] nvarchar(256) NOT NULL,
        [Description] nvarchar(1024) NULL,
        [Status] nvarchar(32) NOT NULL,
        [DslVersion] nvarchar(16) NOT NULL,
        [DslJson] nvarchar(max) NOT NULL,
        [ThemeKey] nvarchar(64) NULL,
        [PublishedDslJson] nvarchar(max) NULL,
        [PublishedVersion] int NOT NULL,
        [PublishedAt] datetime2 NULL,
        [PublishedBy] nvarchar(128) NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_Dashboards] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema175 AS sysname;
    SET @defaultSchema175 = SCHEMA_NAME();
    DECLARE @description175 AS sql_variant;
    SET @description175 = N'仪表盘';
    EXEC sp_addextendedproperty 'MS_Description', @description175, 'SCHEMA', @defaultSchema175, 'TABLE', N'Dashboards';
    SET @description175 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description175, 'SCHEMA', @defaultSchema175, 'TABLE', N'Dashboards', 'COLUMN', N'Id';
    SET @description175 = N'所属租户（0=全局模板）';
    EXEC sp_addextendedproperty 'MS_Description', @description175, 'SCHEMA', @defaultSchema175, 'TABLE', N'Dashboards', 'COLUMN', N'TenantId';
    SET @description175 = N'业务编码';
    EXEC sp_addextendedproperty 'MS_Description', @description175, 'SCHEMA', @defaultSchema175, 'TABLE', N'Dashboards', 'COLUMN', N'Code';
    SET @description175 = N'标题';
    EXEC sp_addextendedproperty 'MS_Description', @description175, 'SCHEMA', @defaultSchema175, 'TABLE', N'Dashboards', 'COLUMN', N'Title';
    SET @description175 = N'描述';
    EXEC sp_addextendedproperty 'MS_Description', @description175, 'SCHEMA', @defaultSchema175, 'TABLE', N'Dashboards', 'COLUMN', N'Description';
    SET @description175 = N'状态';
    EXEC sp_addextendedproperty 'MS_Description', @description175, 'SCHEMA', @defaultSchema175, 'TABLE', N'Dashboards', 'COLUMN', N'Status';
    SET @description175 = N'DSL版本';
    EXEC sp_addextendedproperty 'MS_Description', @description175, 'SCHEMA', @defaultSchema175, 'TABLE', N'Dashboards', 'COLUMN', N'DslVersion';
    SET @description175 = N'DSL文档（结构化，非裸HTML）';
    EXEC sp_addextendedproperty 'MS_Description', @description175, 'SCHEMA', @defaultSchema175, 'TABLE', N'Dashboards', 'COLUMN', N'DslJson';
    SET @description175 = N'主题键';
    EXEC sp_addextendedproperty 'MS_Description', @description175, 'SCHEMA', @defaultSchema175, 'TABLE', N'Dashboards', 'COLUMN', N'ThemeKey';
    SET @description175 = N'已发布DSL快照（null=未发布）';
    EXEC sp_addextendedproperty 'MS_Description', @description175, 'SCHEMA', @defaultSchema175, 'TABLE', N'Dashboards', 'COLUMN', N'PublishedDslJson';
    SET @description175 = N'当前发布版本号（0=未发布）';
    EXEC sp_addextendedproperty 'MS_Description', @description175, 'SCHEMA', @defaultSchema175, 'TABLE', N'Dashboards', 'COLUMN', N'PublishedVersion';
    SET @description175 = N'最近发布时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description175, 'SCHEMA', @defaultSchema175, 'TABLE', N'Dashboards', 'COLUMN', N'PublishedAt';
    SET @description175 = N'最近发布者';
    EXEC sp_addextendedproperty 'MS_Description', @description175, 'SCHEMA', @defaultSchema175, 'TABLE', N'Dashboards', 'COLUMN', N'PublishedBy';
    SET @description175 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description175, 'SCHEMA', @defaultSchema175, 'TABLE', N'Dashboards', 'COLUMN', N'CreatedTime';
    SET @description175 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description175, 'SCHEMA', @defaultSchema175, 'TABLE', N'Dashboards', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [Permissions] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [Category] nvarchar(32) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema176 AS sysname;
    SET @defaultSchema176 = SCHEMA_NAME();
    DECLARE @description176 AS sql_variant;
    SET @description176 = N'权限';
    EXEC sp_addextendedproperty 'MS_Description', @description176, 'SCHEMA', @defaultSchema176, 'TABLE', N'Permissions';
    SET @description176 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description176, 'SCHEMA', @defaultSchema176, 'TABLE', N'Permissions', 'COLUMN', N'Id';
    SET @description176 = N'所属租户（0=全局权限）';
    EXEC sp_addextendedproperty 'MS_Description', @description176, 'SCHEMA', @defaultSchema176, 'TABLE', N'Permissions', 'COLUMN', N'TenantId';
    SET @description176 = N'权限码（同租户唯一）';
    EXEC sp_addextendedproperty 'MS_Description', @description176, 'SCHEMA', @defaultSchema176, 'TABLE', N'Permissions', 'COLUMN', N'Code';
    SET @description176 = N'权限名';
    EXEC sp_addextendedproperty 'MS_Description', @description176, 'SCHEMA', @defaultSchema176, 'TABLE', N'Permissions', 'COLUMN', N'Name';
    SET @description176 = N'权限分类';
    EXEC sp_addextendedproperty 'MS_Description', @description176, 'SCHEMA', @defaultSchema176, 'TABLE', N'Permissions', 'COLUMN', N'Category';
    SET @description176 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description176, 'SCHEMA', @defaultSchema176, 'TABLE', N'Permissions', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [QuotaPolicies] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [ResourceType] int NOT NULL,
        [Limit] bigint NOT NULL,
        [Window] int NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_QuotaPolicies] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema177 AS sysname;
    SET @defaultSchema177 = SCHEMA_NAME();
    DECLARE @description177 AS sql_variant;
    SET @description177 = N'配额策略';
    EXEC sp_addextendedproperty 'MS_Description', @description177, 'SCHEMA', @defaultSchema177, 'TABLE', N'QuotaPolicies';
    SET @description177 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description177, 'SCHEMA', @defaultSchema177, 'TABLE', N'QuotaPolicies', 'COLUMN', N'Id';
    SET @description177 = N'所属租户（0=平台默认）';
    EXEC sp_addextendedproperty 'MS_Description', @description177, 'SCHEMA', @defaultSchema177, 'TABLE', N'QuotaPolicies', 'COLUMN', N'TenantId';
    SET @description177 = N'资源类型';
    EXEC sp_addextendedproperty 'MS_Description', @description177, 'SCHEMA', @defaultSchema177, 'TABLE', N'QuotaPolicies', 'COLUMN', N'ResourceType';
    SET @description177 = N'上限';
    EXEC sp_addextendedproperty 'MS_Description', @description177, 'SCHEMA', @defaultSchema177, 'TABLE', N'QuotaPolicies', 'COLUMN', N'Limit';
    SET @description177 = N'周期窗口';
    EXEC sp_addextendedproperty 'MS_Description', @description177, 'SCHEMA', @defaultSchema177, 'TABLE', N'QuotaPolicies', 'COLUMN', N'Window';
    SET @description177 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description177, 'SCHEMA', @defaultSchema177, 'TABLE', N'QuotaPolicies', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [QuotaUsages] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [ResourceType] int NOT NULL,
        [Used] bigint NOT NULL,
        [PeriodKey] nvarchar(32) NOT NULL,
        [LastReset] datetime2 NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_QuotaUsages] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema178 AS sysname;
    SET @defaultSchema178 = SCHEMA_NAME();
    DECLARE @description178 AS sql_variant;
    SET @description178 = N'配额使用量';
    EXEC sp_addextendedproperty 'MS_Description', @description178, 'SCHEMA', @defaultSchema178, 'TABLE', N'QuotaUsages';
    SET @description178 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description178, 'SCHEMA', @defaultSchema178, 'TABLE', N'QuotaUsages', 'COLUMN', N'Id';
    SET @description178 = N'所属租户';
    EXEC sp_addextendedproperty 'MS_Description', @description178, 'SCHEMA', @defaultSchema178, 'TABLE', N'QuotaUsages', 'COLUMN', N'TenantId';
    SET @description178 = N'资源类型';
    EXEC sp_addextendedproperty 'MS_Description', @description178, 'SCHEMA', @defaultSchema178, 'TABLE', N'QuotaUsages', 'COLUMN', N'ResourceType';
    SET @description178 = N'已用';
    EXEC sp_addextendedproperty 'MS_Description', @description178, 'SCHEMA', @defaultSchema178, 'TABLE', N'QuotaUsages', 'COLUMN', N'Used';
    SET @description178 = N'周期键';
    EXEC sp_addextendedproperty 'MS_Description', @description178, 'SCHEMA', @defaultSchema178, 'TABLE', N'QuotaUsages', 'COLUMN', N'PeriodKey';
    SET @description178 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description178, 'SCHEMA', @defaultSchema178, 'TABLE', N'QuotaUsages', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema179 AS sysname;
    SET @defaultSchema179 = SCHEMA_NAME();
    DECLARE @description179 AS sql_variant;
    SET @description179 = N'角色';
    EXEC sp_addextendedproperty 'MS_Description', @description179, 'SCHEMA', @defaultSchema179, 'TABLE', N'Roles';
    SET @description179 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description179, 'SCHEMA', @defaultSchema179, 'TABLE', N'Roles', 'COLUMN', N'Id';
    SET @description179 = N'所属租户（0=全局角色）';
    EXEC sp_addextendedproperty 'MS_Description', @description179, 'SCHEMA', @defaultSchema179, 'TABLE', N'Roles', 'COLUMN', N'TenantId';
    SET @description179 = N'角色码（同租户唯一）';
    EXEC sp_addextendedproperty 'MS_Description', @description179, 'SCHEMA', @defaultSchema179, 'TABLE', N'Roles', 'COLUMN', N'Code';
    SET @description179 = N'角色名';
    EXEC sp_addextendedproperty 'MS_Description', @description179, 'SCHEMA', @defaultSchema179, 'TABLE', N'Roles', 'COLUMN', N'Name';
    SET @description179 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description179, 'SCHEMA', @defaultSchema179, 'TABLE', N'Roles', 'COLUMN', N'CreatedTime';
    SET @description179 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description179, 'SCHEMA', @defaultSchema179, 'TABLE', N'Roles', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [SemanticLabels] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [ConceptType] nvarchar(64) NOT NULL,
        [ConceptId] bigint NOT NULL,
        [Culture] nvarchar(16) NOT NULL,
        [LabelKind] nvarchar(32) NOT NULL,
        [Value] nvarchar(512) NOT NULL,
        [Source] nvarchar(32) NULL,
        [SortOrder] int NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_SemanticLabels] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema180 AS sysname;
    SET @defaultSchema180 = SCHEMA_NAME();
    DECLARE @description180 AS sql_variant;
    SET @description180 = N'语义多语言标签';
    EXEC sp_addextendedproperty 'MS_Description', @description180, 'SCHEMA', @defaultSchema180, 'TABLE', N'SemanticLabels';
    SET @description180 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description180, 'SCHEMA', @defaultSchema180, 'TABLE', N'SemanticLabels', 'COLUMN', N'Id';
    SET @description180 = N'所属租户（0=全局共享）';
    EXEC sp_addextendedproperty 'MS_Description', @description180, 'SCHEMA', @defaultSchema180, 'TABLE', N'SemanticLabels', 'COLUMN', N'TenantId';
    SET @description180 = N'概念类型';
    EXEC sp_addextendedproperty 'MS_Description', @description180, 'SCHEMA', @defaultSchema180, 'TABLE', N'SemanticLabels', 'COLUMN', N'ConceptType';
    SET @description180 = N'概念实体Id';
    EXEC sp_addextendedproperty 'MS_Description', @description180, 'SCHEMA', @defaultSchema180, 'TABLE', N'SemanticLabels', 'COLUMN', N'ConceptId';
    SET @description180 = N'语言标签';
    EXEC sp_addextendedproperty 'MS_Description', @description180, 'SCHEMA', @defaultSchema180, 'TABLE', N'SemanticLabels', 'COLUMN', N'Culture';
    SET @description180 = N'标签种类';
    EXEC sp_addextendedproperty 'MS_Description', @description180, 'SCHEMA', @defaultSchema180, 'TABLE', N'SemanticLabels', 'COLUMN', N'LabelKind';
    SET @description180 = N'标签文本';
    EXEC sp_addextendedproperty 'MS_Description', @description180, 'SCHEMA', @defaultSchema180, 'TABLE', N'SemanticLabels', 'COLUMN', N'Value';
    SET @description180 = N'来源';
    EXEC sp_addextendedproperty 'MS_Description', @description180, 'SCHEMA', @defaultSchema180, 'TABLE', N'SemanticLabels', 'COLUMN', N'Source';
    SET @description180 = N'排序';
    EXEC sp_addextendedproperty 'MS_Description', @description180, 'SCHEMA', @defaultSchema180, 'TABLE', N'SemanticLabels', 'COLUMN', N'SortOrder';
    SET @description180 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description180, 'SCHEMA', @defaultSchema180, 'TABLE', N'SemanticLabels', 'COLUMN', N'CreatedTime';
    SET @description180 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description180, 'SCHEMA', @defaultSchema180, 'TABLE', N'SemanticLabels', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [Tenants] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantCode] nvarchar(64) NOT NULL,
        [TenantName] nvarchar(128) NOT NULL,
        [Enabled] bit NOT NULL,
        [DisabledReason] nvarchar(512) NULL,
        [DisabledAt] datetime2 NULL,
        [DisabledByUserId] bigint NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema181 AS sysname;
    SET @defaultSchema181 = SCHEMA_NAME();
    DECLARE @description181 AS sql_variant;
    SET @description181 = N'租户';
    EXEC sp_addextendedproperty 'MS_Description', @description181, 'SCHEMA', @defaultSchema181, 'TABLE', N'Tenants';
    SET @description181 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description181, 'SCHEMA', @defaultSchema181, 'TABLE', N'Tenants', 'COLUMN', N'Id';
    SET @description181 = N'租户编码（规范化小写存储）';
    EXEC sp_addextendedproperty 'MS_Description', @description181, 'SCHEMA', @defaultSchema181, 'TABLE', N'Tenants', 'COLUMN', N'TenantCode';
    SET @description181 = N'租户名称';
    EXEC sp_addextendedproperty 'MS_Description', @description181, 'SCHEMA', @defaultSchema181, 'TABLE', N'Tenants', 'COLUMN', N'TenantName';
    SET @description181 = N'是否启用';
    EXEC sp_addextendedproperty 'MS_Description', @description181, 'SCHEMA', @defaultSchema181, 'TABLE', N'Tenants', 'COLUMN', N'Enabled';
    SET @description181 = N'停用原因';
    EXEC sp_addextendedproperty 'MS_Description', @description181, 'SCHEMA', @defaultSchema181, 'TABLE', N'Tenants', 'COLUMN', N'DisabledReason';
    SET @description181 = N'停用时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description181, 'SCHEMA', @defaultSchema181, 'TABLE', N'Tenants', 'COLUMN', N'DisabledAt';
    SET @description181 = N'停用操作者用户Id';
    EXEC sp_addextendedproperty 'MS_Description', @description181, 'SCHEMA', @defaultSchema181, 'TABLE', N'Tenants', 'COLUMN', N'DisabledByUserId';
    SET @description181 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description181, 'SCHEMA', @defaultSchema181, 'TABLE', N'Tenants', 'COLUMN', N'CreatedTime';
    SET @description181 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description181, 'SCHEMA', @defaultSchema181, 'TABLE', N'Tenants', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [Themes] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Key] nvarchar(64) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [IsBuiltIn] bit NOT NULL,
        [DslVersion] nvarchar(16) NOT NULL,
        [DslJson] nvarchar(max) NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_Themes] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema182 AS sysname;
    SET @defaultSchema182 = SCHEMA_NAME();
    DECLARE @description182 AS sql_variant;
    SET @description182 = N'主题';
    EXEC sp_addextendedproperty 'MS_Description', @description182, 'SCHEMA', @defaultSchema182, 'TABLE', N'Themes';
    SET @description182 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description182, 'SCHEMA', @defaultSchema182, 'TABLE', N'Themes', 'COLUMN', N'Id';
    SET @description182 = N'所属租户（0=内置/全局模板）';
    EXEC sp_addextendedproperty 'MS_Description', @description182, 'SCHEMA', @defaultSchema182, 'TABLE', N'Themes', 'COLUMN', N'TenantId';
    SET @description182 = N'主题键（同租户内唯一）';
    EXEC sp_addextendedproperty 'MS_Description', @description182, 'SCHEMA', @defaultSchema182, 'TABLE', N'Themes', 'COLUMN', N'Key';
    SET @description182 = N'主题名称';
    EXEC sp_addextendedproperty 'MS_Description', @description182, 'SCHEMA', @defaultSchema182, 'TABLE', N'Themes', 'COLUMN', N'Name';
    SET @description182 = N'是否内置主题';
    EXEC sp_addextendedproperty 'MS_Description', @description182, 'SCHEMA', @defaultSchema182, 'TABLE', N'Themes', 'COLUMN', N'IsBuiltIn';
    SET @description182 = N'DSL版本';
    EXEC sp_addextendedproperty 'MS_Description', @description182, 'SCHEMA', @defaultSchema182, 'TABLE', N'Themes', 'COLUMN', N'DslVersion';
    SET @description182 = N'主题DSL文档（结构化令牌，非CSS/HTML）';
    EXEC sp_addextendedproperty 'MS_Description', @description182, 'SCHEMA', @defaultSchema182, 'TABLE', N'Themes', 'COLUMN', N'DslJson';
    SET @description182 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description182, 'SCHEMA', @defaultSchema182, 'TABLE', N'Themes', 'COLUMN', N'CreatedTime';
    SET @description182 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description182, 'SCHEMA', @defaultSchema182, 'TABLE', N'Themes', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [UiLanguages] (
        [Id] bigint NOT NULL IDENTITY,
        [Culture] nvarchar(16) NOT NULL,
        [DisplayName] nvarchar(64) NOT NULL,
        [NativeName] nvarchar(64) NOT NULL,
        [Enabled] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_UiLanguages] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema183 AS sysname;
    SET @defaultSchema183 = SCHEMA_NAME();
    DECLARE @description183 AS sql_variant;
    SET @description183 = N'平台界面语言目录';
    EXEC sp_addextendedproperty 'MS_Description', @description183, 'SCHEMA', @defaultSchema183, 'TABLE', N'UiLanguages';
    SET @description183 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description183, 'SCHEMA', @defaultSchema183, 'TABLE', N'UiLanguages', 'COLUMN', N'Id';
    SET @description183 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description183, 'SCHEMA', @defaultSchema183, 'TABLE', N'UiLanguages', 'COLUMN', N'CreatedTime';
    SET @description183 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description183, 'SCHEMA', @defaultSchema183, 'TABLE', N'UiLanguages', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [UiTextResources] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Culture] nvarchar(16) NOT NULL,
        [ResourceKey] nvarchar(160) NOT NULL,
        [Value] nvarchar(2048) NOT NULL,
        [Description] nvarchar(256) NULL,
        [IsTranslated] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_UiTextResources] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema184 AS sysname;
    SET @defaultSchema184 = SCHEMA_NAME();
    DECLARE @description184 AS sql_variant;
    SET @description184 = N'平台及租户界面文本';
    EXEC sp_addextendedproperty 'MS_Description', @description184, 'SCHEMA', @defaultSchema184, 'TABLE', N'UiTextResources';
    SET @description184 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description184, 'SCHEMA', @defaultSchema184, 'TABLE', N'UiTextResources', 'COLUMN', N'Id';
    SET @description184 = N'是否已翻译（新建语言从其它语言复制键集合时标记待翻译）';
    EXEC sp_addextendedproperty 'MS_Description', @description184, 'SCHEMA', @defaultSchema184, 'TABLE', N'UiTextResources', 'COLUMN', N'IsTranslated';
    SET @description184 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description184, 'SCHEMA', @defaultSchema184, 'TABLE', N'UiTextResources', 'COLUMN', N'CreatedTime';
    SET @description184 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description184, 'SCHEMA', @defaultSchema184, 'TABLE', N'UiTextResources', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [UserLanguagePreferences] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [UserId] bigint NOT NULL,
        [Culture] nvarchar(16) NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_UserLanguagePreferences] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema185 AS sysname;
    SET @defaultSchema185 = SCHEMA_NAME();
    DECLARE @description185 AS sql_variant;
    SET @description185 = N'用户界面语言偏好';
    EXEC sp_addextendedproperty 'MS_Description', @description185, 'SCHEMA', @defaultSchema185, 'TABLE', N'UserLanguagePreferences';
    SET @description185 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description185, 'SCHEMA', @defaultSchema185, 'TABLE', N'UserLanguagePreferences', 'COLUMN', N'Id';
    SET @description185 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description185, 'SCHEMA', @defaultSchema185, 'TABLE', N'UserLanguagePreferences', 'COLUMN', N'CreatedTime';
    SET @description185 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description185, 'SCHEMA', @defaultSchema185, 'TABLE', N'UserLanguagePreferences', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [AppVersions] (
        [Id] bigint NOT NULL IDENTITY,
        [AppId] bigint NOT NULL,
        [TenantId] bigint NOT NULL,
        [Version] int NOT NULL,
        [Code] nvarchar(128) NOT NULL,
        [Name] nvarchar(256) NOT NULL,
        [Description] nvarchar(1024) NULL,
        [ThemeKey] nvarchar(64) NULL,
        [DslVersion] nvarchar(16) NOT NULL,
        [DslJson] nvarchar(max) NOT NULL,
        [PublishedAt] datetime2 NOT NULL,
        [PublishedBy] nvarchar(128) NULL,
        [RolledBackFromVersion] int NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_AppVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AppVersions_AppPlans_AppId] FOREIGN KEY ([AppId]) REFERENCES [AppPlans] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema186 AS sysname;
    SET @defaultSchema186 = SCHEMA_NAME();
    DECLARE @description186 AS sql_variant;
    SET @description186 = N'应用发布版本快照';
    EXEC sp_addextendedproperty 'MS_Description', @description186, 'SCHEMA', @defaultSchema186, 'TABLE', N'AppVersions';
    SET @description186 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description186, 'SCHEMA', @defaultSchema186, 'TABLE', N'AppVersions', 'COLUMN', N'Id';
    SET @description186 = N'作用域租户（0=全局模板）';
    EXEC sp_addextendedproperty 'MS_Description', @description186, 'SCHEMA', @defaultSchema186, 'TABLE', N'AppVersions', 'COLUMN', N'TenantId';
    SET @description186 = N'业务编码快照';
    EXEC sp_addextendedproperty 'MS_Description', @description186, 'SCHEMA', @defaultSchema186, 'TABLE', N'AppVersions', 'COLUMN', N'Code';
    SET @description186 = N'名称快照';
    EXEC sp_addextendedproperty 'MS_Description', @description186, 'SCHEMA', @defaultSchema186, 'TABLE', N'AppVersions', 'COLUMN', N'Name';
    SET @description186 = N'描述快照';
    EXEC sp_addextendedproperty 'MS_Description', @description186, 'SCHEMA', @defaultSchema186, 'TABLE', N'AppVersions', 'COLUMN', N'Description';
    SET @description186 = N'主题键快照';
    EXEC sp_addextendedproperty 'MS_Description', @description186, 'SCHEMA', @defaultSchema186, 'TABLE', N'AppVersions', 'COLUMN', N'ThemeKey';
    SET @description186 = N'DSL版本快照';
    EXEC sp_addextendedproperty 'MS_Description', @description186, 'SCHEMA', @defaultSchema186, 'TABLE', N'AppVersions', 'COLUMN', N'DslVersion';
    SET @description186 = N'发布时刻固化的DSL文档（只读快照）';
    EXEC sp_addextendedproperty 'MS_Description', @description186, 'SCHEMA', @defaultSchema186, 'TABLE', N'AppVersions', 'COLUMN', N'DslJson';
    SET @description186 = N'发布者';
    EXEC sp_addextendedproperty 'MS_Description', @description186, 'SCHEMA', @defaultSchema186, 'TABLE', N'AppVersions', 'COLUMN', N'PublishedBy';
    SET @description186 = N'回滚来源版本号（非回滚为null）';
    EXEC sp_addextendedproperty 'MS_Description', @description186, 'SCHEMA', @defaultSchema186, 'TABLE', N'AppVersions', 'COLUMN', N'RolledBackFromVersion';
    SET @description186 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description186, 'SCHEMA', @defaultSchema186, 'TABLE', N'AppVersions', 'COLUMN', N'CreatedTime';
    SET @description186 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description186, 'SCHEMA', @defaultSchema186, 'TABLE', N'AppVersions', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [DashboardVersions] (
        [Id] bigint NOT NULL IDENTITY,
        [DashboardId] bigint NOT NULL,
        [TenantId] bigint NOT NULL,
        [Version] int NOT NULL,
        [Code] nvarchar(128) NOT NULL,
        [Title] nvarchar(256) NOT NULL,
        [Description] nvarchar(1024) NULL,
        [ThemeKey] nvarchar(64) NULL,
        [DslVersion] nvarchar(16) NOT NULL,
        [DslJson] nvarchar(max) NOT NULL,
        [PublishedAt] datetime2 NOT NULL,
        [PublishedBy] nvarchar(128) NULL,
        [RolledBackFromVersion] int NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_DashboardVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DashboardVersions_Dashboards_DashboardId] FOREIGN KEY ([DashboardId]) REFERENCES [Dashboards] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema187 AS sysname;
    SET @defaultSchema187 = SCHEMA_NAME();
    DECLARE @description187 AS sql_variant;
    SET @description187 = N'仪表盘发布版本快照';
    EXEC sp_addextendedproperty 'MS_Description', @description187, 'SCHEMA', @defaultSchema187, 'TABLE', N'DashboardVersions';
    SET @description187 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description187, 'SCHEMA', @defaultSchema187, 'TABLE', N'DashboardVersions', 'COLUMN', N'Id';
    SET @description187 = N'作用域租户（0=全局模板）';
    EXEC sp_addextendedproperty 'MS_Description', @description187, 'SCHEMA', @defaultSchema187, 'TABLE', N'DashboardVersions', 'COLUMN', N'TenantId';
    SET @description187 = N'业务编码快照';
    EXEC sp_addextendedproperty 'MS_Description', @description187, 'SCHEMA', @defaultSchema187, 'TABLE', N'DashboardVersions', 'COLUMN', N'Code';
    SET @description187 = N'标题快照';
    EXEC sp_addextendedproperty 'MS_Description', @description187, 'SCHEMA', @defaultSchema187, 'TABLE', N'DashboardVersions', 'COLUMN', N'Title';
    SET @description187 = N'描述快照';
    EXEC sp_addextendedproperty 'MS_Description', @description187, 'SCHEMA', @defaultSchema187, 'TABLE', N'DashboardVersions', 'COLUMN', N'Description';
    SET @description187 = N'主题键快照';
    EXEC sp_addextendedproperty 'MS_Description', @description187, 'SCHEMA', @defaultSchema187, 'TABLE', N'DashboardVersions', 'COLUMN', N'ThemeKey';
    SET @description187 = N'DSL版本快照';
    EXEC sp_addextendedproperty 'MS_Description', @description187, 'SCHEMA', @defaultSchema187, 'TABLE', N'DashboardVersions', 'COLUMN', N'DslVersion';
    SET @description187 = N'发布时刻固化的DSL文档（只读快照）';
    EXEC sp_addextendedproperty 'MS_Description', @description187, 'SCHEMA', @defaultSchema187, 'TABLE', N'DashboardVersions', 'COLUMN', N'DslJson';
    SET @description187 = N'发布者';
    EXEC sp_addextendedproperty 'MS_Description', @description187, 'SCHEMA', @defaultSchema187, 'TABLE', N'DashboardVersions', 'COLUMN', N'PublishedBy';
    SET @description187 = N'回滚来源版本号（非回滚为null）';
    EXEC sp_addextendedproperty 'MS_Description', @description187, 'SCHEMA', @defaultSchema187, 'TABLE', N'DashboardVersions', 'COLUMN', N'RolledBackFromVersion';
    SET @description187 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description187, 'SCHEMA', @defaultSchema187, 'TABLE', N'DashboardVersions', 'COLUMN', N'CreatedTime';
    SET @description187 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description187, 'SCHEMA', @defaultSchema187, 'TABLE', N'DashboardVersions', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [RolePermissions] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [RoleId] bigint NOT NULL,
        [PermissionId] bigint NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema188 AS sysname;
    SET @defaultSchema188 = SCHEMA_NAME();
    DECLARE @description188 AS sql_variant;
    SET @description188 = N'角色-权限关联';
    EXEC sp_addextendedproperty 'MS_Description', @description188, 'SCHEMA', @defaultSchema188, 'TABLE', N'RolePermissions';
    SET @description188 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description188, 'SCHEMA', @defaultSchema188, 'TABLE', N'RolePermissions', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [BusinessDomains] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_BusinessDomains] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessDomains_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @defaultSchema189 AS sysname;
    SET @defaultSchema189 = SCHEMA_NAME();
    DECLARE @description189 AS sql_variant;
    SET @description189 = N'业务域';
    EXEC sp_addextendedproperty 'MS_Description', @description189, 'SCHEMA', @defaultSchema189, 'TABLE', N'BusinessDomains';
    SET @description189 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description189, 'SCHEMA', @defaultSchema189, 'TABLE', N'BusinessDomains', 'COLUMN', N'Id';
    SET @description189 = N'业务域名称';
    EXEC sp_addextendedproperty 'MS_Description', @description189, 'SCHEMA', @defaultSchema189, 'TABLE', N'BusinessDomains', 'COLUMN', N'Name';
    SET @description189 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description189, 'SCHEMA', @defaultSchema189, 'TABLE', N'BusinessDomains', 'COLUMN', N'CreatedTime';
    SET @description189 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description189, 'SCHEMA', @defaultSchema189, 'TABLE', N'BusinessDomains', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [DataSources] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [NormalizedName] nvarchar(128) NOT NULL,
        [DbType] nvarchar(32) NOT NULL,
        [ConnectionString] nvarchar(2048) NOT NULL,
        [Enabled] bit NOT NULL DEFAULT CAST(1 AS bit),
        [LastTestStatus] nvarchar(32) NULL,
        [LastTestTime] datetime2 NULL,
        [LastErrorCode] nvarchar(64) NULL,
        [LastScanAt] datetime2 NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_DataSources] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_DataSources_Id_TenantId] UNIQUE ([Id], [TenantId]),
        CONSTRAINT [FK_DataSources_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @defaultSchema190 AS sysname;
    SET @defaultSchema190 = SCHEMA_NAME();
    DECLARE @description190 AS sql_variant;
    SET @description190 = N'数据源';
    EXEC sp_addextendedproperty 'MS_Description', @description190, 'SCHEMA', @defaultSchema190, 'TABLE', N'DataSources';
    SET @description190 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description190, 'SCHEMA', @defaultSchema190, 'TABLE', N'DataSources', 'COLUMN', N'Id';
    SET @description190 = N'名称（展示用，保留原始大小写）';
    EXEC sp_addextendedproperty 'MS_Description', @description190, 'SCHEMA', @defaultSchema190, 'TABLE', N'DataSources', 'COLUMN', N'Name';
    SET @description190 = N'规范化名称（小写去空白），租户内唯一键';
    EXEC sp_addextendedproperty 'MS_Description', @description190, 'SCHEMA', @defaultSchema190, 'TABLE', N'DataSources', 'COLUMN', N'NormalizedName';
    SET @description190 = N'数据库类型(MYSQL/SQLSERVER/POSTGRESQL)';
    EXEC sp_addextendedproperty 'MS_Description', @description190, 'SCHEMA', @defaultSchema190, 'TABLE', N'DataSources', 'COLUMN', N'DbType';
    SET @description190 = N'连接字符串（敏感，禁止日志记录）';
    EXEC sp_addextendedproperty 'MS_Description', @description190, 'SCHEMA', @defaultSchema190, 'TABLE', N'DataSources', 'COLUMN', N'ConnectionString';
    SET @description190 = N'是否启用';
    EXEC sp_addextendedproperty 'MS_Description', @description190, 'SCHEMA', @defaultSchema190, 'TABLE', N'DataSources', 'COLUMN', N'Enabled';
    SET @description190 = N'最近连接测试状态(Ok/Failed/Unknown)';
    EXEC sp_addextendedproperty 'MS_Description', @description190, 'SCHEMA', @defaultSchema190, 'TABLE', N'DataSources', 'COLUMN', N'LastTestStatus';
    SET @description190 = N'最近连接测试时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description190, 'SCHEMA', @defaultSchema190, 'TABLE', N'DataSources', 'COLUMN', N'LastTestTime';
    SET @description190 = N'最近连接测试错误码(仅异常类型名,脱敏)';
    EXEC sp_addextendedproperty 'MS_Description', @description190, 'SCHEMA', @defaultSchema190, 'TABLE', N'DataSources', 'COLUMN', N'LastErrorCode';
    SET @description190 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description190, 'SCHEMA', @defaultSchema190, 'TABLE', N'DataSources', 'COLUMN', N'CreatedTime';
    SET @description190 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description190, 'SCHEMA', @defaultSchema190, 'TABLE', N'DataSources', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [TenantSettings] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Key] nvarchar(128) NOT NULL,
        [Value] nvarchar(max) NULL,
        [DataType] nvarchar(32) NULL,
        [IsLocked] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_TenantSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantSettings_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema191 AS sysname;
    SET @defaultSchema191 = SCHEMA_NAME();
    DECLARE @description191 AS sql_variant;
    SET @description191 = N'租户键值配置';
    EXEC sp_addextendedproperty 'MS_Description', @description191, 'SCHEMA', @defaultSchema191, 'TABLE', N'TenantSettings';
    SET @description191 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description191, 'SCHEMA', @defaultSchema191, 'TABLE', N'TenantSettings', 'COLUMN', N'Id';
    SET @description191 = N'配置键';
    EXEC sp_addextendedproperty 'MS_Description', @description191, 'SCHEMA', @defaultSchema191, 'TABLE', N'TenantSettings', 'COLUMN', N'Key';
    SET @description191 = N'配置值';
    EXEC sp_addextendedproperty 'MS_Description', @description191, 'SCHEMA', @defaultSchema191, 'TABLE', N'TenantSettings', 'COLUMN', N'Value';
    SET @description191 = N'值类型(string|int|bool|json)';
    EXEC sp_addextendedproperty 'MS_Description', @description191, 'SCHEMA', @defaultSchema191, 'TABLE', N'TenantSettings', 'COLUMN', N'DataType';
    SET @description191 = N'是否锁定(租户不可覆盖)';
    EXEC sp_addextendedproperty 'MS_Description', @description191, 'SCHEMA', @defaultSchema191, 'TABLE', N'TenantSettings', 'COLUMN', N'IsLocked';
    SET @description191 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description191, 'SCHEMA', @defaultSchema191, 'TABLE', N'TenantSettings', 'COLUMN', N'CreatedTime';
    SET @description191 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description191, 'SCHEMA', @defaultSchema191, 'TABLE', N'TenantSettings', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Username] nvarchar(128) NOT NULL,
        [NormalizedUsername] nvarchar(128) NULL,
        [DisplayName] nvarchar(128) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL DEFAULT CAST(0 AS bit),
        [PasswordHash] nvarchar(256) NULL,
        [SecurityStamp] nvarchar(64) NOT NULL,
        [Status] int NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @defaultSchema192 AS sysname;
    SET @defaultSchema192 = SCHEMA_NAME();
    DECLARE @description192 AS sql_variant;
    SET @description192 = N'用户';
    EXEC sp_addextendedproperty 'MS_Description', @description192, 'SCHEMA', @defaultSchema192, 'TABLE', N'Users';
    SET @description192 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description192, 'SCHEMA', @defaultSchema192, 'TABLE', N'Users', 'COLUMN', N'Id';
    SET @description192 = N'所属租户';
    EXEC sp_addextendedproperty 'MS_Description', @description192, 'SCHEMA', @defaultSchema192, 'TABLE', N'Users', 'COLUMN', N'TenantId';
    SET @description192 = N'登录名（展示用，大小写原始）';
    EXEC sp_addextendedproperty 'MS_Description', @description192, 'SCHEMA', @defaultSchema192, 'TABLE', N'Users', 'COLUMN', N'Username';
    SET @description192 = N'规范化登录名（小写，租户内唯一）';
    EXEC sp_addextendedproperty 'MS_Description', @description192, 'SCHEMA', @defaultSchema192, 'TABLE', N'Users', 'COLUMN', N'NormalizedUsername';
    SET @description192 = N'显示名';
    EXEC sp_addextendedproperty 'MS_Description', @description192, 'SCHEMA', @defaultSchema192, 'TABLE', N'Users', 'COLUMN', N'DisplayName';
    SET @description192 = N'邮箱';
    EXEC sp_addextendedproperty 'MS_Description', @description192, 'SCHEMA', @defaultSchema192, 'TABLE', N'Users', 'COLUMN', N'Email';
    SET @description192 = N'规范化邮箱（小写）';
    EXEC sp_addextendedproperty 'MS_Description', @description192, 'SCHEMA', @defaultSchema192, 'TABLE', N'Users', 'COLUMN', N'NormalizedEmail';
    SET @description192 = N'邮箱是否已验证';
    EXEC sp_addextendedproperty 'MS_Description', @description192, 'SCHEMA', @defaultSchema192, 'TABLE', N'Users', 'COLUMN', N'EmailConfirmed';
    SET @description192 = N'口令哈希（PBKDF2，可选）';
    EXEC sp_addextendedproperty 'MS_Description', @description192, 'SCHEMA', @defaultSchema192, 'TABLE', N'Users', 'COLUMN', N'PasswordHash';
    SET @description192 = N'安全戳（令牌吊销用）';
    EXEC sp_addextendedproperty 'MS_Description', @description192, 'SCHEMA', @defaultSchema192, 'TABLE', N'Users', 'COLUMN', N'SecurityStamp';
    SET @description192 = N'状态';
    EXEC sp_addextendedproperty 'MS_Description', @description192, 'SCHEMA', @defaultSchema192, 'TABLE', N'Users', 'COLUMN', N'Status';
    SET @description192 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description192, 'SCHEMA', @defaultSchema192, 'TABLE', N'Users', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [TenantUiLanguages] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [UiLanguageId] bigint NOT NULL,
        [Enabled] bit NOT NULL DEFAULT CAST(1 AS bit),
        [IsDefault] bit NOT NULL DEFAULT CAST(0 AS bit),
        [SortOrder] int NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_TenantUiLanguages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantUiLanguages_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TenantUiLanguages_UiLanguages_UiLanguageId] FOREIGN KEY ([UiLanguageId]) REFERENCES [UiLanguages] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @defaultSchema193 AS sysname;
    SET @defaultSchema193 = SCHEMA_NAME();
    DECLARE @description193 AS sql_variant;
    SET @description193 = N'租户界面语言关系（替代 localization:availableCultures/defaultCulture JSON）';
    EXEC sp_addextendedproperty 'MS_Description', @description193, 'SCHEMA', @defaultSchema193, 'TABLE', N'TenantUiLanguages';
    SET @description193 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description193, 'SCHEMA', @defaultSchema193, 'TABLE', N'TenantUiLanguages', 'COLUMN', N'Id';
    SET @description193 = N'所属租户';
    EXEC sp_addextendedproperty 'MS_Description', @description193, 'SCHEMA', @defaultSchema193, 'TABLE', N'TenantUiLanguages', 'COLUMN', N'TenantId';
    SET @description193 = N'平台语言目录 Id';
    EXEC sp_addextendedproperty 'MS_Description', @description193, 'SCHEMA', @defaultSchema193, 'TABLE', N'TenantUiLanguages', 'COLUMN', N'UiLanguageId';
    SET @description193 = N'是否启用（租户范围内）';
    EXEC sp_addextendedproperty 'MS_Description', @description193, 'SCHEMA', @defaultSchema193, 'TABLE', N'TenantUiLanguages', 'COLUMN', N'Enabled';
    SET @description193 = N'是否为租户默认语言（每租户恰一个）';
    EXEC sp_addextendedproperty 'MS_Description', @description193, 'SCHEMA', @defaultSchema193, 'TABLE', N'TenantUiLanguages', 'COLUMN', N'IsDefault';
    SET @description193 = N'排序';
    EXEC sp_addextendedproperty 'MS_Description', @description193, 'SCHEMA', @defaultSchema193, 'TABLE', N'TenantUiLanguages', 'COLUMN', N'SortOrder';
    SET @description193 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description193, 'SCHEMA', @defaultSchema193, 'TABLE', N'TenantUiLanguages', 'COLUMN', N'CreatedTime';
    SET @description193 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description193, 'SCHEMA', @defaultSchema193, 'TABLE', N'TenantUiLanguages', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [BusinessEntities] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [BusinessKey] nvarchar(200) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [DisplayName] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [BusinessDomain] nvarchar(max) NULL,
        [SemanticText] nvarchar(max) NULL,
        [Status] nvarchar(50) NOT NULL,
        [BusinessDomainId] bigint NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_BusinessEntities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessEntities_BusinessDomains_BusinessDomainId] FOREIGN KEY ([BusinessDomainId]) REFERENCES [BusinessDomains] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BusinessEntities_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @defaultSchema194 AS sysname;
    SET @defaultSchema194 = SCHEMA_NAME();
    DECLARE @description194 AS sql_variant;
    SET @description194 = N'业务实体';
    EXEC sp_addextendedproperty 'MS_Description', @description194, 'SCHEMA', @defaultSchema194, 'TABLE', N'BusinessEntities';
    SET @description194 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description194, 'SCHEMA', @defaultSchema194, 'TABLE', N'BusinessEntities', 'COLUMN', N'Id';
    SET @description194 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description194, 'SCHEMA', @defaultSchema194, 'TABLE', N'BusinessEntities', 'COLUMN', N'CreatedTime';
    SET @description194 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description194, 'SCHEMA', @defaultSchema194, 'TABLE', N'BusinessEntities', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [BusinessEntityDimensions] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [BusinessDomainId] bigint NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_BusinessEntityDimensions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessEntityDimensions_BusinessDomains_BusinessDomainId] FOREIGN KEY ([BusinessDomainId]) REFERENCES [BusinessDomains] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema195 AS sysname;
    SET @defaultSchema195 = SCHEMA_NAME();
    DECLARE @description195 AS sql_variant;
    SET @description195 = N'业务实体维度';
    EXEC sp_addextendedproperty 'MS_Description', @description195, 'SCHEMA', @defaultSchema195, 'TABLE', N'BusinessEntityDimensions';
    SET @description195 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description195, 'SCHEMA', @defaultSchema195, 'TABLE', N'BusinessEntityDimensions', 'COLUMN', N'Id';
    SET @description195 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description195, 'SCHEMA', @defaultSchema195, 'TABLE', N'BusinessEntityDimensions', 'COLUMN', N'CreatedTime';
    SET @description195 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description195, 'SCHEMA', @defaultSchema195, 'TABLE', N'BusinessEntityDimensions', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [DataSourceAccessGrants] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [DataSourceId] bigint NOT NULL,
        [SubjectType] int NOT NULL,
        [SubjectId] bigint NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_DataSourceAccessGrants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DataSourceAccessGrants_DataSources_DataSourceId] FOREIGN KEY ([DataSourceId]) REFERENCES [DataSources] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DataSourceAccessGrants_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema196 AS sysname;
    SET @defaultSchema196 = SCHEMA_NAME();
    DECLARE @description196 AS sql_variant;
    SET @description196 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description196, 'SCHEMA', @defaultSchema196, 'TABLE', N'DataSourceAccessGrants', 'COLUMN', N'Id';
    SET @description196 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description196, 'SCHEMA', @defaultSchema196, 'TABLE', N'DataSourceAccessGrants', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [MetadataScanJobs] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [DataSourceId] bigint NOT NULL,
        [TriggeredBy] nvarchar(64) NULL,
        [Status] nvarchar(16) NOT NULL DEFAULT N'Queued',
        [ProgressPercent] int NOT NULL DEFAULT 0,
        [StartedAt] datetime2 NULL,
        [FinishedAt] datetime2 NULL,
        [TablesScanned] int NOT NULL DEFAULT 0,
        [ColumnsScanned] int NOT NULL DEFAULT 0,
        [OrphansDetected] int NOT NULL DEFAULT 0,
        [ErrorCode] nvarchar(64) NULL,
        [ErrorMessage] nvarchar(2000) NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_MetadataScanJobs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MetadataScanJobs_DataSources_DataSourceId] FOREIGN KEY ([DataSourceId]) REFERENCES [DataSources] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema197 AS sysname;
    SET @defaultSchema197 = SCHEMA_NAME();
    DECLARE @description197 AS sql_variant;
    SET @description197 = N'元数据扫描任务';
    EXEC sp_addextendedproperty 'MS_Description', @description197, 'SCHEMA', @defaultSchema197, 'TABLE', N'MetadataScanJobs';
    SET @description197 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description197, 'SCHEMA', @defaultSchema197, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'Id';
    SET @description197 = N'触发用户标识';
    EXEC sp_addextendedproperty 'MS_Description', @description197, 'SCHEMA', @defaultSchema197, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'TriggeredBy';
    SET @description197 = N'扫描状态';
    EXEC sp_addextendedproperty 'MS_Description', @description197, 'SCHEMA', @defaultSchema197, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'Status';
    SET @description197 = N'进度百分比';
    EXEC sp_addextendedproperty 'MS_Description', @description197, 'SCHEMA', @defaultSchema197, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'ProgressPercent';
    SET @description197 = N'开始时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description197, 'SCHEMA', @defaultSchema197, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'StartedAt';
    SET @description197 = N'结束时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description197, 'SCHEMA', @defaultSchema197, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'FinishedAt';
    SET @description197 = N'已扫描表数';
    EXEC sp_addextendedproperty 'MS_Description', @description197, 'SCHEMA', @defaultSchema197, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'TablesScanned';
    SET @description197 = N'已扫描字段数';
    EXEC sp_addextendedproperty 'MS_Description', @description197, 'SCHEMA', @defaultSchema197, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'ColumnsScanned';
    SET @description197 = N'孤儿对象数';
    EXEC sp_addextendedproperty 'MS_Description', @description197, 'SCHEMA', @defaultSchema197, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'OrphansDetected';
    SET @description197 = N'错误码(仅异常类型名,脱敏)';
    EXEC sp_addextendedproperty 'MS_Description', @description197, 'SCHEMA', @defaultSchema197, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'ErrorCode';
    SET @description197 = N'错误摘要(脱敏,不含连接串)';
    EXEC sp_addextendedproperty 'MS_Description', @description197, 'SCHEMA', @defaultSchema197, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'ErrorMessage';
    SET @description197 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description197, 'SCHEMA', @defaultSchema197, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'CreatedTime';
    SET @description197 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description197, 'SCHEMA', @defaultSchema197, 'TABLE', N'MetadataScanJobs', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [MetadataTables] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [DataSourceId] bigint NOT NULL,
        [TableName] nvarchar(128) NOT NULL,
        [CatalogName] nvarchar(128) NULL,
        [SchemaName] nvarchar(128) NULL,
        [TableComment] nvarchar(max) NULL,
        [BusinessDomain] nvarchar(max) NULL,
        [SearchText] nvarchar(max) NULL,
        [VectorId] nvarchar(max) NULL,
        [EmbeddingModel] nvarchar(128) NULL,
        [VectorDimension] int NULL,
        [VectorSyncTime] datetime2 NULL,
        [VectorStatus] nvarchar(16) NULL,
        [VectorErrorCode] nvarchar(64) NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_MetadataTables] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MetadataTables_DataSources_DataSourceId_TenantId] FOREIGN KEY ([DataSourceId], [TenantId]) REFERENCES [DataSources] ([Id], [TenantId]) ON DELETE CASCADE,
        CONSTRAINT [FK_MetadataTables_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @defaultSchema198 AS sysname;
    SET @defaultSchema198 = SCHEMA_NAME();
    DECLARE @description198 AS sql_variant;
    SET @description198 = N'元数据表';
    EXEC sp_addextendedproperty 'MS_Description', @description198, 'SCHEMA', @defaultSchema198, 'TABLE', N'MetadataTables';
    SET @description198 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description198, 'SCHEMA', @defaultSchema198, 'TABLE', N'MetadataTables', 'COLUMN', N'Id';
    SET @description198 = N'表名';
    EXEC sp_addextendedproperty 'MS_Description', @description198, 'SCHEMA', @defaultSchema198, 'TABLE', N'MetadataTables', 'COLUMN', N'TableName';
    SET @description198 = N'目录名';
    EXEC sp_addextendedproperty 'MS_Description', @description198, 'SCHEMA', @defaultSchema198, 'TABLE', N'MetadataTables', 'COLUMN', N'CatalogName';
    SET @description198 = N'模式名';
    EXEC sp_addextendedproperty 'MS_Description', @description198, 'SCHEMA', @defaultSchema198, 'TABLE', N'MetadataTables', 'COLUMN', N'SchemaName';
    SET @description198 = N'Embedding文本';
    EXEC sp_addextendedproperty 'MS_Description', @description198, 'SCHEMA', @defaultSchema198, 'TABLE', N'MetadataTables', 'COLUMN', N'SearchText';
    SET @description198 = N'Qdrant向量ID';
    EXEC sp_addextendedproperty 'MS_Description', @description198, 'SCHEMA', @defaultSchema198, 'TABLE', N'MetadataTables', 'COLUMN', N'VectorId';
    SET @description198 = N'Embedding模型';
    EXEC sp_addextendedproperty 'MS_Description', @description198, 'SCHEMA', @defaultSchema198, 'TABLE', N'MetadataTables', 'COLUMN', N'EmbeddingModel';
    SET @description198 = N'向量维度';
    EXEC sp_addextendedproperty 'MS_Description', @description198, 'SCHEMA', @defaultSchema198, 'TABLE', N'MetadataTables', 'COLUMN', N'VectorDimension';
    SET @description198 = N'向量同步时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description198, 'SCHEMA', @defaultSchema198, 'TABLE', N'MetadataTables', 'COLUMN', N'VectorSyncTime';
    SET @description198 = N'向量状态';
    EXEC sp_addextendedproperty 'MS_Description', @description198, 'SCHEMA', @defaultSchema198, 'TABLE', N'MetadataTables', 'COLUMN', N'VectorStatus';
    SET @description198 = N'向量错误码';
    EXEC sp_addextendedproperty 'MS_Description', @description198, 'SCHEMA', @defaultSchema198, 'TABLE', N'MetadataTables', 'COLUMN', N'VectorErrorCode';
    SET @description198 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description198, 'SCHEMA', @defaultSchema198, 'TABLE', N'MetadataTables', 'COLUMN', N'CreatedTime';
    SET @description198 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description198, 'SCHEMA', @defaultSchema198, 'TABLE', N'MetadataTables', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [PlatformAdminTenantScopes] (
        [Id] bigint NOT NULL IDENTITY,
        [AdminUserId] bigint NOT NULL,
        [TenantId] bigint NOT NULL,
        [GrantedAt] datetime2 NOT NULL,
        [GrantedBy] nvarchar(128) NULL,
        CONSTRAINT [PK_PlatformAdminTenantScopes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlatformAdminTenantScopes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PlatformAdminTenantScopes_Users_AdminUserId] FOREIGN KEY ([AdminUserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema199 AS sysname;
    SET @defaultSchema199 = SCHEMA_NAME();
    DECLARE @description199 AS sql_variant;
    SET @description199 = N'平台管理员租户范围绑定';
    EXEC sp_addextendedproperty 'MS_Description', @description199, 'SCHEMA', @defaultSchema199, 'TABLE', N'PlatformAdminTenantScopes';
    SET @description199 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description199, 'SCHEMA', @defaultSchema199, 'TABLE', N'PlatformAdminTenantScopes', 'COLUMN', N'Id';
    SET @description199 = N'授权时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description199, 'SCHEMA', @defaultSchema199, 'TABLE', N'PlatformAdminTenantScopes', 'COLUMN', N'GrantedAt';
    SET @description199 = N'授权操作者';
    EXEC sp_addextendedproperty 'MS_Description', @description199, 'SCHEMA', @defaultSchema199, 'TABLE', N'PlatformAdminTenantScopes', 'COLUMN', N'GrantedBy';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [UserId] bigint NOT NULL,
        [RoleId] bigint NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema200 AS sysname;
    SET @defaultSchema200 = SCHEMA_NAME();
    DECLARE @description200 AS sql_variant;
    SET @description200 = N'用户-角色关联';
    EXEC sp_addextendedproperty 'MS_Description', @description200, 'SCHEMA', @defaultSchema200, 'TABLE', N'UserRoles';
    SET @description200 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description200, 'SCHEMA', @defaultSchema200, 'TABLE', N'UserRoles', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [UserTenants] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [TenantId] bigint NOT NULL,
        [IsDefault] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedByUserId] bigint NULL,
        CONSTRAINT [PK_UserTenants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserTenants_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserTenants_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema201 AS sysname;
    SET @defaultSchema201 = SCHEMA_NAME();
    DECLARE @description201 AS sql_variant;
    SET @description201 = N'用户—租户成员关系（多租户切换）';
    EXEC sp_addextendedproperty 'MS_Description', @description201, 'SCHEMA', @defaultSchema201, 'TABLE', N'UserTenants';
    SET @description201 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description201, 'SCHEMA', @defaultSchema201, 'TABLE', N'UserTenants', 'COLUMN', N'Id';
    SET @description201 = N'操作者用户 Id（管理员代加成员）';
    EXEC sp_addextendedproperty 'MS_Description', @description201, 'SCHEMA', @defaultSchema201, 'TABLE', N'UserTenants', 'COLUMN', N'CreatedByUserId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [BusinessEntityAttributes] (
        [Id] bigint NOT NULL IDENTITY,
        [BusinessEntityId] bigint NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [DisplayName] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [SemanticType] nvarchar(max) NULL,
        [IsNullable] bit NOT NULL,
        [IsIdentifier] bit NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_BusinessEntityAttributes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessEntityAttributes_BusinessEntities_BusinessEntityId] FOREIGN KEY ([BusinessEntityId]) REFERENCES [BusinessEntities] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema202 AS sysname;
    SET @defaultSchema202 = SCHEMA_NAME();
    DECLARE @description202 AS sql_variant;
    SET @description202 = N'业务实体属性';
    EXEC sp_addextendedproperty 'MS_Description', @description202, 'SCHEMA', @defaultSchema202, 'TABLE', N'BusinessEntityAttributes';
    SET @description202 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description202, 'SCHEMA', @defaultSchema202, 'TABLE', N'BusinessEntityAttributes', 'COLUMN', N'Id';
    SET @description202 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description202, 'SCHEMA', @defaultSchema202, 'TABLE', N'BusinessEntityAttributes', 'COLUMN', N'CreatedTime';
    SET @description202 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description202, 'SCHEMA', @defaultSchema202, 'TABLE', N'BusinessEntityAttributes', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [BusinessEntityKeys] (
        [Id] bigint NOT NULL IDENTITY,
        [BusinessEntityId] bigint NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [DisplayName] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [IsPrimary] bit NOT NULL,
        [KeyType] nvarchar(max) NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_BusinessEntityKeys] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessEntityKeys_BusinessEntities_BusinessEntityId] FOREIGN KEY ([BusinessEntityId]) REFERENCES [BusinessEntities] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema203 AS sysname;
    SET @defaultSchema203 = SCHEMA_NAME();
    DECLARE @description203 AS sql_variant;
    SET @description203 = N'业务实体键';
    EXEC sp_addextendedproperty 'MS_Description', @description203, 'SCHEMA', @defaultSchema203, 'TABLE', N'BusinessEntityKeys';
    SET @description203 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description203, 'SCHEMA', @defaultSchema203, 'TABLE', N'BusinessEntityKeys', 'COLUMN', N'Id';
    SET @description203 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description203, 'SCHEMA', @defaultSchema203, 'TABLE', N'BusinessEntityKeys', 'COLUMN', N'CreatedTime';
    SET @description203 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description203, 'SCHEMA', @defaultSchema203, 'TABLE', N'BusinessEntityKeys', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [BusinessEntityMetrics] (
        [Id] bigint NOT NULL IDENTITY,
        [BusinessEntityId] bigint NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [DisplayName] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [SemanticType] nvarchar(max) NULL,
        [Aggregation] nvarchar(50) NULL,
        [IsCalculated] bit NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_BusinessEntityMetrics] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessEntityMetrics_BusinessEntities_BusinessEntityId] FOREIGN KEY ([BusinessEntityId]) REFERENCES [BusinessEntities] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema204 AS sysname;
    SET @defaultSchema204 = SCHEMA_NAME();
    DECLARE @description204 AS sql_variant;
    SET @description204 = N'业务实体指标';
    EXEC sp_addextendedproperty 'MS_Description', @description204, 'SCHEMA', @defaultSchema204, 'TABLE', N'BusinessEntityMetrics';
    SET @description204 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description204, 'SCHEMA', @defaultSchema204, 'TABLE', N'BusinessEntityMetrics', 'COLUMN', N'Id';
    SET @description204 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description204, 'SCHEMA', @defaultSchema204, 'TABLE', N'BusinessEntityMetrics', 'COLUMN', N'CreatedTime';
    SET @description204 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description204, 'SCHEMA', @defaultSchema204, 'TABLE', N'BusinessEntityMetrics', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [BusinessEntityRelationships] (
        [Id] bigint NOT NULL IDENTITY,
        [SourceEntityId] bigint NOT NULL,
        [TargetEntityId] bigint NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [DisplayName] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [RelationshipType] nvarchar(max) NULL,
        [Cardinality] nvarchar(max) NULL,
        [IsRequired] bit NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_BusinessEntityRelationships] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessEntityRelationships_BusinessEntities_SourceEntityId] FOREIGN KEY ([SourceEntityId]) REFERENCES [BusinessEntities] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BusinessEntityRelationships_BusinessEntities_TargetEntityId] FOREIGN KEY ([TargetEntityId]) REFERENCES [BusinessEntities] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @defaultSchema205 AS sysname;
    SET @defaultSchema205 = SCHEMA_NAME();
    DECLARE @description205 AS sql_variant;
    SET @description205 = N'业务实体关系';
    EXEC sp_addextendedproperty 'MS_Description', @description205, 'SCHEMA', @defaultSchema205, 'TABLE', N'BusinessEntityRelationships';
    SET @description205 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description205, 'SCHEMA', @defaultSchema205, 'TABLE', N'BusinessEntityRelationships', 'COLUMN', N'Id';
    SET @description205 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description205, 'SCHEMA', @defaultSchema205, 'TABLE', N'BusinessEntityRelationships', 'COLUMN', N'CreatedTime';
    SET @description205 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description205, 'SCHEMA', @defaultSchema205, 'TABLE', N'BusinessEntityRelationships', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [MetadataColumns] (
        [Id] bigint NOT NULL IDENTITY,
        [MetadataTableId] bigint NOT NULL,
        [BusinessKey] nvarchar(max) NULL,
        [ColumnName] nvarchar(128) NOT NULL,
        [Ordinal] int NOT NULL DEFAULT 0,
        [NativeType] nvarchar(64) NULL,
        [Precision] int NULL,
        [Scale] int NULL,
        [ColumnComment] nvarchar(max) NULL,
        [DataType] nvarchar(max) NULL,
        [Length] bigint NULL,
        [IsNullable] bit NULL,
        [IsPrimaryKey] bit NULL,
        [SearchText] nvarchar(max) NULL,
        [VectorId] nvarchar(max) NULL,
        [EmbeddingModel] nvarchar(128) NULL,
        [VectorDimension] int NULL,
        [VectorSyncTime] datetime2 NULL,
        [VectorStatus] nvarchar(16) NULL,
        [VectorErrorCode] nvarchar(64) NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_MetadataColumns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MetadataColumns_MetadataTables_MetadataTableId] FOREIGN KEY ([MetadataTableId]) REFERENCES [MetadataTables] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema206 AS sysname;
    SET @defaultSchema206 = SCHEMA_NAME();
    DECLARE @description206 AS sql_variant;
    SET @description206 = N'元数据字段';
    EXEC sp_addextendedproperty 'MS_Description', @description206, 'SCHEMA', @defaultSchema206, 'TABLE', N'MetadataColumns';
    SET @description206 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description206, 'SCHEMA', @defaultSchema206, 'TABLE', N'MetadataColumns', 'COLUMN', N'Id';
    SET @description206 = N'字段业务唯一标识';
    EXEC sp_addextendedproperty 'MS_Description', @description206, 'SCHEMA', @defaultSchema206, 'TABLE', N'MetadataColumns', 'COLUMN', N'BusinessKey';
    SET @description206 = N'列名';
    EXEC sp_addextendedproperty 'MS_Description', @description206, 'SCHEMA', @defaultSchema206, 'TABLE', N'MetadataColumns', 'COLUMN', N'ColumnName';
    SET @description206 = N'列序号';
    EXEC sp_addextendedproperty 'MS_Description', @description206, 'SCHEMA', @defaultSchema206, 'TABLE', N'MetadataColumns', 'COLUMN', N'Ordinal';
    SET @description206 = N'原生类型';
    EXEC sp_addextendedproperty 'MS_Description', @description206, 'SCHEMA', @defaultSchema206, 'TABLE', N'MetadataColumns', 'COLUMN', N'NativeType';
    SET @description206 = N'精度';
    EXEC sp_addextendedproperty 'MS_Description', @description206, 'SCHEMA', @defaultSchema206, 'TABLE', N'MetadataColumns', 'COLUMN', N'Precision';
    SET @description206 = N'小数位';
    EXEC sp_addextendedproperty 'MS_Description', @description206, 'SCHEMA', @defaultSchema206, 'TABLE', N'MetadataColumns', 'COLUMN', N'Scale';
    SET @description206 = N'字段Embedding文本';
    EXEC sp_addextendedproperty 'MS_Description', @description206, 'SCHEMA', @defaultSchema206, 'TABLE', N'MetadataColumns', 'COLUMN', N'SearchText';
    SET @description206 = N'Qdrant字段向量ID';
    EXEC sp_addextendedproperty 'MS_Description', @description206, 'SCHEMA', @defaultSchema206, 'TABLE', N'MetadataColumns', 'COLUMN', N'VectorId';
    SET @description206 = N'Embedding模型';
    EXEC sp_addextendedproperty 'MS_Description', @description206, 'SCHEMA', @defaultSchema206, 'TABLE', N'MetadataColumns', 'COLUMN', N'EmbeddingModel';
    SET @description206 = N'向量维度';
    EXEC sp_addextendedproperty 'MS_Description', @description206, 'SCHEMA', @defaultSchema206, 'TABLE', N'MetadataColumns', 'COLUMN', N'VectorDimension';
    SET @description206 = N'向量同步时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description206, 'SCHEMA', @defaultSchema206, 'TABLE', N'MetadataColumns', 'COLUMN', N'VectorSyncTime';
    SET @description206 = N'向量状态';
    EXEC sp_addextendedproperty 'MS_Description', @description206, 'SCHEMA', @defaultSchema206, 'TABLE', N'MetadataColumns', 'COLUMN', N'VectorStatus';
    SET @description206 = N'向量错误码';
    EXEC sp_addextendedproperty 'MS_Description', @description206, 'SCHEMA', @defaultSchema206, 'TABLE', N'MetadataColumns', 'COLUMN', N'VectorErrorCode';
    SET @description206 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description206, 'SCHEMA', @defaultSchema206, 'TABLE', N'MetadataColumns', 'COLUMN', N'CreatedTime';
    SET @description206 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description206, 'SCHEMA', @defaultSchema206, 'TABLE', N'MetadataColumns', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [LearningRecords] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Question] nvarchar(max) NULL,
        [MetadataColumnId] bigint NULL,
        [Correct] bit NULL,
        [Feedback] nvarchar(max) NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_LearningRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LearningRecords_MetadataColumns_MetadataColumnId] FOREIGN KEY ([MetadataColumnId]) REFERENCES [MetadataColumns] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_LearningRecords_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema207 AS sysname;
    SET @defaultSchema207 = SCHEMA_NAME();
    DECLARE @description207 AS sql_variant;
    SET @description207 = N'学习记录';
    EXEC sp_addextendedproperty 'MS_Description', @description207, 'SCHEMA', @defaultSchema207, 'TABLE', N'LearningRecords';
    SET @description207 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description207, 'SCHEMA', @defaultSchema207, 'TABLE', N'LearningRecords', 'COLUMN', N'Id';
    SET @description207 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description207, 'SCHEMA', @defaultSchema207, 'TABLE', N'LearningRecords', 'COLUMN', N'CreatedTime';
    SET @description207 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description207, 'SCHEMA', @defaultSchema207, 'TABLE', N'LearningRecords', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [MetadataSemantics] (
        [Id] bigint NOT NULL IDENTITY,
        [MetadataColumnId] bigint NULL,
        [BusinessMeaning] nvarchar(max) NULL,
        [Keywords] nvarchar(2048) NULL,
        [Synonyms] nvarchar(2048) NULL,
        [ExampleQuestions] nvarchar(2048) NULL,
        [BusinessDomain] nvarchar(max) NULL,
        [BusinessDomainId] bigint NULL,
        [Confidence] decimal(5,4) NULL,
        [Source] nvarchar(16) NOT NULL DEFAULT N'Manual',
        [SearchText] nvarchar(max) NULL,
        [VectorId] nvarchar(max) NULL,
        [EmbeddingModel] nvarchar(128) NULL,
        [VectorDimension] int NULL,
        [VectorSyncTime] datetime2 NULL,
        [VectorStatus] nvarchar(16) NULL,
        [VectorErrorCode] nvarchar(64) NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_MetadataSemantics] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_MetadataSemantics_Confidence] CHECK ([Confidence] IS NULL OR ([Confidence] >= 0 AND [Confidence] <= 1)),
        CONSTRAINT [FK_MetadataSemantics_BusinessDomains_BusinessDomainId] FOREIGN KEY ([BusinessDomainId]) REFERENCES [BusinessDomains] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_MetadataSemantics_MetadataColumns_MetadataColumnId] FOREIGN KEY ([MetadataColumnId]) REFERENCES [MetadataColumns] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema208 AS sysname;
    SET @defaultSchema208 = SCHEMA_NAME();
    DECLARE @description208 AS sql_variant;
    SET @description208 = N'字段AI语义';
    EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', @defaultSchema208, 'TABLE', N'MetadataSemantics';
    SET @description208 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', @defaultSchema208, 'TABLE', N'MetadataSemantics', 'COLUMN', N'Id';
    SET @description208 = N'业务含义';
    EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', @defaultSchema208, 'TABLE', N'MetadataSemantics', 'COLUMN', N'BusinessMeaning';
    SET @description208 = N'关键词';
    EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', @defaultSchema208, 'TABLE', N'MetadataSemantics', 'COLUMN', N'Keywords';
    SET @description208 = N'同义词';
    EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', @defaultSchema208, 'TABLE', N'MetadataSemantics', 'COLUMN', N'Synonyms';
    SET @description208 = N'示例问题';
    EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', @defaultSchema208, 'TABLE', N'MetadataSemantics', 'COLUMN', N'ExampleQuestions';
    SET @description208 = N'业务域';
    EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', @defaultSchema208, 'TABLE', N'MetadataSemantics', 'COLUMN', N'BusinessDomain';
    SET @description208 = N'业务域Id（FK 权威，逐步替代字符串 BusinessDomain）';
    EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', @defaultSchema208, 'TABLE', N'MetadataSemantics', 'COLUMN', N'BusinessDomainId';
    SET @description208 = N'AI生成置信度(0-1)';
    EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', @defaultSchema208, 'TABLE', N'MetadataSemantics', 'COLUMN', N'Confidence';
    SET @description208 = N'来源';
    EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', @defaultSchema208, 'TABLE', N'MetadataSemantics', 'COLUMN', N'Source';
    SET @description208 = N'语义Embedding文本';
    EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', @defaultSchema208, 'TABLE', N'MetadataSemantics', 'COLUMN', N'SearchText';
    SET @description208 = N'Qdrant语义向量ID';
    EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', @defaultSchema208, 'TABLE', N'MetadataSemantics', 'COLUMN', N'VectorId';
    SET @description208 = N'Embedding模型';
    EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', @defaultSchema208, 'TABLE', N'MetadataSemantics', 'COLUMN', N'EmbeddingModel';
    SET @description208 = N'向量维度';
    EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', @defaultSchema208, 'TABLE', N'MetadataSemantics', 'COLUMN', N'VectorDimension';
    SET @description208 = N'向量同步时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', @defaultSchema208, 'TABLE', N'MetadataSemantics', 'COLUMN', N'VectorSyncTime';
    SET @description208 = N'向量状态';
    EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', @defaultSchema208, 'TABLE', N'MetadataSemantics', 'COLUMN', N'VectorStatus';
    SET @description208 = N'向量错误码';
    EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', @defaultSchema208, 'TABLE', N'MetadataSemantics', 'COLUMN', N'VectorErrorCode';
    SET @description208 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', @defaultSchema208, 'TABLE', N'MetadataSemantics', 'COLUMN', N'CreatedTime';
    SET @description208 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', @defaultSchema208, 'TABLE', N'MetadataSemantics', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [PhysicalBindings] (
        [Id] bigint NOT NULL IDENTITY,
        [DataSourceId] bigint NOT NULL,
        [MetadataTableId] bigint NOT NULL,
        [MetadataColumnId] bigint NOT NULL,
        [PhysicalRole] nvarchar(50) NULL,
        [BindingType] nvarchar(50) NULL,
        [Priority] int NOT NULL,
        [IsActive] bit NOT NULL,
        [BusinessEntityKeyId] bigint NULL,
        [BusinessEntityAttributeId] bigint NULL,
        [BusinessEntityMetricId] bigint NULL,
        [BusinessEntityRelationshipId] bigint NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_PhysicalBindings] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PhysicalBindings_PriorityNonNeg] CHECK ([Priority] >= 0),
        CONSTRAINT [FK_PhysicalBindings_BusinessEntityAttributes_BusinessEntityAttributeId] FOREIGN KEY ([BusinessEntityAttributeId]) REFERENCES [BusinessEntityAttributes] ([Id]),
        CONSTRAINT [FK_PhysicalBindings_BusinessEntityKeys_BusinessEntityKeyId] FOREIGN KEY ([BusinessEntityKeyId]) REFERENCES [BusinessEntityKeys] ([Id]),
        CONSTRAINT [FK_PhysicalBindings_BusinessEntityMetrics_BusinessEntityMetricId] FOREIGN KEY ([BusinessEntityMetricId]) REFERENCES [BusinessEntityMetrics] ([Id]),
        CONSTRAINT [FK_PhysicalBindings_BusinessEntityRelationships_BusinessEntityRelationshipId] FOREIGN KEY ([BusinessEntityRelationshipId]) REFERENCES [BusinessEntityRelationships] ([Id]),
        CONSTRAINT [FK_PhysicalBindings_DataSources_DataSourceId] FOREIGN KEY ([DataSourceId]) REFERENCES [DataSources] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PhysicalBindings_MetadataColumns_MetadataColumnId] FOREIGN KEY ([MetadataColumnId]) REFERENCES [MetadataColumns] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PhysicalBindings_MetadataTables_MetadataTableId] FOREIGN KEY ([MetadataTableId]) REFERENCES [MetadataTables] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @defaultSchema209 AS sysname;
    SET @defaultSchema209 = SCHEMA_NAME();
    DECLARE @description209 AS sql_variant;
    SET @description209 = N'业务语义到物理元数据的映射';
    EXEC sp_addextendedproperty 'MS_Description', @description209, 'SCHEMA', @defaultSchema209, 'TABLE', N'PhysicalBindings';
    SET @description209 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description209, 'SCHEMA', @defaultSchema209, 'TABLE', N'PhysicalBindings', 'COLUMN', N'Id';
    SET @description209 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description209, 'SCHEMA', @defaultSchema209, 'TABLE', N'PhysicalBindings', 'COLUMN', N'CreatedTime';
    SET @description209 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description209, 'SCHEMA', @defaultSchema209, 'TABLE', N'PhysicalBindings', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE TABLE [RowLevelSecurityPolicies] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [DataSourceId] bigint NOT NULL,
        [MetadataTableId] bigint NOT NULL,
        [MetadataColumnId] bigint NOT NULL,
        [SubjectType] int NOT NULL,
        [SubjectId] bigint NULL,
        [SubjectKey] nvarchar(128) NULL,
        [SubjectValue] nvarchar(512) NULL,
        [Effect] int NOT NULL,
        [Operator] nvarchar(16) NOT NULL,
        [Value] nvarchar(2048) NOT NULL,
        [Enabled] bit NOT NULL,
        [Version] bigint NOT NULL,
        [UpdatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_RowLevelSecurityPolicies] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_RlsPolicies_Operator] CHECK ([Operator] IN ('=', '!=', '>', '>=', '<', '<=', 'LIKE', 'IN', 'IS NULL', 'IS NOT NULL')),
        CONSTRAINT [CK_RlsPolicies_SubjectConsistency] CHECK (([SubjectType] = 0 AND [SubjectId] IS NULL AND [SubjectKey] IS NULL) OR ([SubjectType] = 1 AND [SubjectId] IS NOT NULL) OR ([SubjectType] = 2 AND [SubjectId] IS NOT NULL) OR ([SubjectType] = 3 AND [SubjectKey] IS NOT NULL) OR ([SubjectType] NOT IN (0,1,2,3))),
        CONSTRAINT [FK_RowLevelSecurityPolicies_DataSources_DataSourceId] FOREIGN KEY ([DataSourceId]) REFERENCES [DataSources] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RowLevelSecurityPolicies_MetadataColumns_MetadataColumnId] FOREIGN KEY ([MetadataColumnId]) REFERENCES [MetadataColumns] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RowLevelSecurityPolicies_MetadataTables_MetadataTableId] FOREIGN KEY ([MetadataTableId]) REFERENCES [MetadataTables] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema210 AS sysname;
    SET @defaultSchema210 = SCHEMA_NAME();
    DECLARE @description210 AS sql_variant;
    SET @description210 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description210, 'SCHEMA', @defaultSchema210, 'TABLE', N'RowLevelSecurityPolicies', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AgentPlans_TenantId_Code] ON [AgentPlans] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_AgentPlans_TenantId_Status] ON [AgentPlans] ([TenantId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AppPlans_TenantId_Code] ON [AppPlans] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_AppPlans_TenantId_Status] ON [AppPlans] ([TenantId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AppVersions_AppId_Version] ON [AppVersions] ([AppId], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_AppVersions_TenantId_AppId] ON [AppVersions] ([TenantId], [AppId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_TenantId] ON [AuditLogs] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_TenantId_Action] ON [AuditLogs] ([TenantId], [Action]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_TenantId_EntityType] ON [AuditLogs] ([TenantId], [EntityType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Timestamp] ON [AuditLogs] ([Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_BusinessDomains_TenantId] ON [BusinessDomains] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessDomains_TenantId_Name] ON [BusinessDomains] ([TenantId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_BusinessEntities_BusinessDomainId] ON [BusinessEntities] ([BusinessDomainId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessEntities_TenantId_BusinessKey] ON [BusinessEntities] ([TenantId], [BusinessKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_BusinessEntityAttributes_BusinessEntityId_IsIdentifier] ON [BusinessEntityAttributes] ([BusinessEntityId], [IsIdentifier]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessEntityAttributes_BusinessEntityId_Name] ON [BusinessEntityAttributes] ([BusinessEntityId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_BusinessEntityDimensions_BusinessDomainId] ON [BusinessEntityDimensions] ([BusinessDomainId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessEntityDimensions_TenantId_BusinessDomainId_Name] ON [BusinessEntityDimensions] ([TenantId], [BusinessDomainId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_BusinessEntityKeys_BusinessEntityId_IsPrimary] ON [BusinessEntityKeys] ([BusinessEntityId], [IsPrimary]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessEntityKeys_BusinessEntityId_Name] ON [BusinessEntityKeys] ([BusinessEntityId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessEntityMetrics_BusinessEntityId_Name] ON [BusinessEntityMetrics] ([BusinessEntityId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessEntityRelationships_SourceEntityId_TargetEntityId_Name] ON [BusinessEntityRelationships] ([SourceEntityId], [TargetEntityId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_BusinessEntityRelationships_TargetEntityId] ON [BusinessEntityRelationships] ([TargetEntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Dashboards_TenantId_Code] ON [Dashboards] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_Dashboards_TenantId_Status] ON [Dashboards] ([TenantId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DashboardVersions_DashboardId_Version] ON [DashboardVersions] ([DashboardId], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_DashboardVersions_TenantId_DashboardId] ON [DashboardVersions] ([TenantId], [DashboardId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_DataSourceAccessGrants_DataSourceId] ON [DataSourceAccessGrants] ([DataSourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DataSourceAccessGrants_TenantId_DataSourceId_SubjectType_SubjectId] ON [DataSourceAccessGrants] ([TenantId], [DataSourceId], [SubjectType], [SubjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_DataSourceAccessGrants_TenantId_SubjectType_SubjectId] ON [DataSourceAccessGrants] ([TenantId], [SubjectType], [SubjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_DataSources_TenantId] ON [DataSources] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DataSources_TenantId_NormalizedName] ON [DataSources] ([TenantId], [NormalizedName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_LearningRecords_MetadataColumnId] ON [LearningRecords] ([MetadataColumnId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_LearningRecords_TenantId] ON [LearningRecords] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MetadataColumns_MetadataTableId_ColumnName] ON [MetadataColumns] ([MetadataTableId], [ColumnName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_MetadataScanJobs_DataSourceId] ON [MetadataScanJobs] ([DataSourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_MetadataScanJobs_Status] ON [MetadataScanJobs] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_MetadataScanJobs_TenantId] ON [MetadataScanJobs] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_MetadataSemantics_BusinessDomainId] ON [MetadataSemantics] ([BusinessDomainId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MetadataSemantics_MetadataColumnId] ON [MetadataSemantics] ([MetadataColumnId]) WHERE [MetadataColumnId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_MetadataTables_DataSourceId] ON [MetadataTables] ([DataSourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MetadataTables_DataSourceId_CatalogName_SchemaName_TableName] ON [MetadataTables] ([DataSourceId], [CatalogName], [SchemaName], [TableName]) WHERE [CatalogName] IS NOT NULL AND [SchemaName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_MetadataTables_DataSourceId_TenantId] ON [MetadataTables] ([DataSourceId], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_MetadataTables_TenantId] ON [MetadataTables] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Permissions_TenantId_Code] ON [Permissions] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_PhysicalBindings_BusinessEntityAttributeId_IsActive] ON [PhysicalBindings] ([BusinessEntityAttributeId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_PhysicalBindings_BusinessEntityKeyId_BusinessEntityAttributeId_BusinessEntityMetricId_BusinessEntityRelationshipId] ON [PhysicalBindings] ([BusinessEntityKeyId], [BusinessEntityAttributeId], [BusinessEntityMetricId], [BusinessEntityRelationshipId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_PhysicalBindings_BusinessEntityKeyId_IsActive] ON [PhysicalBindings] ([BusinessEntityKeyId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_PhysicalBindings_BusinessEntityMetricId_IsActive] ON [PhysicalBindings] ([BusinessEntityMetricId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_PhysicalBindings_BusinessEntityRelationshipId_PhysicalRole_IsActive] ON [PhysicalBindings] ([BusinessEntityRelationshipId], [PhysicalRole], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_PhysicalBindings_DataSourceId_MetadataTableId_MetadataColumnId] ON [PhysicalBindings] ([DataSourceId], [MetadataTableId], [MetadataColumnId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_PhysicalBindings_DataSourceId_MetadataTableId_MetadataColumnId_Priority] ON [PhysicalBindings] ([DataSourceId], [MetadataTableId], [MetadataColumnId], [Priority]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_PhysicalBindings_MetadataColumnId] ON [PhysicalBindings] ([MetadataColumnId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_PhysicalBindings_MetadataTableId] ON [PhysicalBindings] ([MetadataTableId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_PlatformAdminTenantScopes_AdminUserId] ON [PlatformAdminTenantScopes] ([AdminUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PlatformAdminTenantScopes_AdminUserId_TenantId] ON [PlatformAdminTenantScopes] ([AdminUserId], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_PlatformAdminTenantScopes_TenantId] ON [PlatformAdminTenantScopes] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_QuotaPolicies_TenantId_ResourceType] ON [QuotaPolicies] ([TenantId], [ResourceType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_QuotaUsages_TenantId_ResourceType] ON [QuotaUsages] ([TenantId], [ResourceType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_PermissionId] ON [RolePermissions] ([PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_RoleId] ON [RolePermissions] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RolePermissions_TenantId_RoleId_PermissionId] ON [RolePermissions] ([TenantId], [RoleId], [PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_TenantId_Code] ON [Roles] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_RowLevelSecurityPolicies_DataSourceId] ON [RowLevelSecurityPolicies] ([DataSourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_RowLevelSecurityPolicies_MetadataColumnId] ON [RowLevelSecurityPolicies] ([MetadataColumnId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_RowLevelSecurityPolicies_MetadataTableId] ON [RowLevelSecurityPolicies] ([MetadataTableId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_RowLevelSecurityPolicies_TenantId_DataSourceId_MetadataTableId_Enabled] ON [RowLevelSecurityPolicies] ([TenantId], [DataSourceId], [MetadataTableId], [Enabled]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_RowLevelSecurityPolicies_TenantId_SubjectType_SubjectId] ON [RowLevelSecurityPolicies] ([TenantId], [SubjectType], [SubjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_SemanticLabels_ConceptType_ConceptId_Culture] ON [SemanticLabels] ([ConceptType], [ConceptId], [Culture]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SemanticLabels_TenantId_ConceptType_ConceptId_Culture_LabelKind_SortOrder] ON [SemanticLabels] ([TenantId], [ConceptType], [ConceptId], [Culture], [LabelKind], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tenants_TenantCode] ON [Tenants] ([TenantCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TenantSettings_TenantId_Key] ON [TenantSettings] ([TenantId], [Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_TenantUiLanguages_TenantId_IsDefault] ON [TenantUiLanguages] ([TenantId], [IsDefault]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TenantUiLanguages_TenantId_UiLanguageId] ON [TenantUiLanguages] ([TenantId], [UiLanguageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_TenantUiLanguages_UiLanguageId] ON [TenantUiLanguages] ([UiLanguageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_Themes_TenantId] ON [Themes] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Themes_TenantId_Key] ON [Themes] ([TenantId], [Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UiLanguages_Culture] ON [UiLanguages] ([Culture]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UiTextResources_TenantId_Culture_ResourceKey] ON [UiTextResources] ([TenantId], [Culture], [ResourceKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserLanguagePreferences_TenantId_UserId] ON [UserLanguagePreferences] ([TenantId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserRoles_TenantId_UserId_RoleId] ON [UserRoles] ([TenantId], [UserId], [RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_UserRoles_UserId] ON [UserRoles] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_Users_TenantId] ON [Users] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_TenantId_NormalizedUsername] ON [Users] ([TenantId], [NormalizedUsername]) WHERE [NormalizedUsername] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_UserTenants_TenantId] ON [UserTenants] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE INDEX [IX_UserTenants_UserId] ON [UserTenants] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserTenants_UserId_TenantId] ON [UserTenants] ([UserId], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906141902_M7_02_AppVersion'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260906141902_M7_02_AppVersion', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907004450_M7_03_AgentRuntime'
)
BEGIN
    CREATE TABLE [AgentRuns] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [PlanCode] nvarchar(128) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [CurrentStepOrder] int NOT NULL,
        [Actor] nvarchar(256) NULL,
        [GrantedToolsJson] nvarchar(max) NULL,
        [Approved] bit NOT NULL,
        [StartedAt] datetime2 NULL,
        [FinishedAt] datetime2 NULL,
        [ResultSummary] nvarchar(1024) NULL,
        [StepLogJson] nvarchar(max) NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_AgentRuns] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema211 AS sysname;
    SET @defaultSchema211 = SCHEMA_NAME();
    DECLARE @description211 AS sql_variant;
    SET @description211 = N'Agent运行记录（M7-03 Agent Runtime）';
    EXEC sp_addextendedproperty 'MS_Description', @description211, 'SCHEMA', @defaultSchema211, 'TABLE', N'AgentRuns';
    SET @description211 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description211, 'SCHEMA', @defaultSchema211, 'TABLE', N'AgentRuns', 'COLUMN', N'Id';
    SET @description211 = N'所属租户（运行恒归属某一租户）';
    EXEC sp_addextendedproperty 'MS_Description', @description211, 'SCHEMA', @defaultSchema211, 'TABLE', N'AgentRuns', 'COLUMN', N'TenantId';
    SET @description211 = N'Agent编码';
    EXEC sp_addextendedproperty 'MS_Description', @description211, 'SCHEMA', @defaultSchema211, 'TABLE', N'AgentRuns', 'COLUMN', N'PlanCode';
    SET @description211 = N'运行状态';
    EXEC sp_addextendedproperty 'MS_Description', @description211, 'SCHEMA', @defaultSchema211, 'TABLE', N'AgentRuns', 'COLUMN', N'Status';
    SET @description211 = N'触发者';
    EXEC sp_addextendedproperty 'MS_Description', @description211, 'SCHEMA', @defaultSchema211, 'TABLE', N'AgentRuns', 'COLUMN', N'Actor';
    SET @description211 = N'授权工具集合（JSON）';
    EXEC sp_addextendedproperty 'MS_Description', @description211, 'SCHEMA', @defaultSchema211, 'TABLE', N'AgentRuns', 'COLUMN', N'GrantedToolsJson';
    SET @description211 = N'结果摘要';
    EXEC sp_addextendedproperty 'MS_Description', @description211, 'SCHEMA', @defaultSchema211, 'TABLE', N'AgentRuns', 'COLUMN', N'ResultSummary';
    SET @description211 = N'每步执行结果（JSON 信封）';
    EXEC sp_addextendedproperty 'MS_Description', @description211, 'SCHEMA', @defaultSchema211, 'TABLE', N'AgentRuns', 'COLUMN', N'StepLogJson';
    SET @description211 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description211, 'SCHEMA', @defaultSchema211, 'TABLE', N'AgentRuns', 'COLUMN', N'CreatedTime';
    SET @description211 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description211, 'SCHEMA', @defaultSchema211, 'TABLE', N'AgentRuns', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907004450_M7_03_AgentRuntime'
)
BEGIN
    CREATE INDEX [IX_AgentRuns_Status] ON [AgentRuns] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907004450_M7_03_AgentRuntime'
)
BEGIN
    CREATE INDEX [IX_AgentRuns_TenantId_PlanCode] ON [AgentRuns] ([TenantId], [PlanCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907004450_M7_03_AgentRuntime'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260907004450_M7_03_AgentRuntime', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907035855_P7_3_ModelAccounts'
)
BEGIN
    CREATE TABLE [ModelAccounts] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Provider] nvarchar(64) NOT NULL,
        [ModelId] nvarchar(128) NOT NULL,
        [DisplayName] nvarchar(128) NOT NULL,
        [EncryptedKey] nvarchar(2048) NOT NULL,
        [MaskedKey] nvarchar(64) NOT NULL,
        [Note] nvarchar(512) NULL,
        [IsDefault] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_ModelAccounts] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema212 AS sysname;
    SET @defaultSchema212 = SCHEMA_NAME();
    DECLARE @description212 AS sql_variant;
    SET @description212 = N'模型账号绑定（BYO 加密存储）';
    EXEC sp_addextendedproperty 'MS_Description', @description212, 'SCHEMA', @defaultSchema212, 'TABLE', N'ModelAccounts';
    SET @description212 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description212, 'SCHEMA', @defaultSchema212, 'TABLE', N'ModelAccounts', 'COLUMN', N'Id';
    SET @description212 = N'所属租户（>0）';
    EXEC sp_addextendedproperty 'MS_Description', @description212, 'SCHEMA', @defaultSchema212, 'TABLE', N'ModelAccounts', 'COLUMN', N'TenantId';
    SET @description212 = N'供应商标识(Qwen/OpenAI/...)';
    EXEC sp_addextendedproperty 'MS_Description', @description212, 'SCHEMA', @defaultSchema212, 'TABLE', N'ModelAccounts', 'COLUMN', N'Provider';
    SET @description212 = N'模型标识(qwen-plus/gpt-4o/...)';
    EXEC sp_addextendedproperty 'MS_Description', @description212, 'SCHEMA', @defaultSchema212, 'TABLE', N'ModelAccounts', 'COLUMN', N'ModelId';
    SET @description212 = N'展示名';
    EXEC sp_addextendedproperty 'MS_Description', @description212, 'SCHEMA', @defaultSchema212, 'TABLE', N'ModelAccounts', 'COLUMN', N'DisplayName';
    SET @description212 = N'AES-GCM 密文(禁止日志记录/明文下发)';
    EXEC sp_addextendedproperty 'MS_Description', @description212, 'SCHEMA', @defaultSchema212, 'TABLE', N'ModelAccounts', 'COLUMN', N'EncryptedKey';
    SET @description212 = N'展示掩码(如 sk-***1234)';
    EXEC sp_addextendedproperty 'MS_Description', @description212, 'SCHEMA', @defaultSchema212, 'TABLE', N'ModelAccounts', 'COLUMN', N'MaskedKey';
    SET @description212 = N'备注';
    EXEC sp_addextendedproperty 'MS_Description', @description212, 'SCHEMA', @defaultSchema212, 'TABLE', N'ModelAccounts', 'COLUMN', N'Note';
    SET @description212 = N'是否为租户默认模型(每租户至多一个)';
    EXEC sp_addextendedproperty 'MS_Description', @description212, 'SCHEMA', @defaultSchema212, 'TABLE', N'ModelAccounts', 'COLUMN', N'IsDefault';
    SET @description212 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description212, 'SCHEMA', @defaultSchema212, 'TABLE', N'ModelAccounts', 'COLUMN', N'CreatedTime';
    SET @description212 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description212, 'SCHEMA', @defaultSchema212, 'TABLE', N'ModelAccounts', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907035855_P7_3_ModelAccounts'
)
BEGIN
    CREATE INDEX [IX_ModelAccounts_TenantId] ON [ModelAccounts] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907035855_P7_3_ModelAccounts'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ModelAccounts_TenantId_Provider_ModelId] ON [ModelAccounts] ([TenantId], [Provider], [ModelId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907035855_P7_3_ModelAccounts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260907035855_P7_3_ModelAccounts', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907053931_M7_09_CustomComponents'
)
BEGIN
    CREATE TABLE [CustomComponents] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [Key] nvarchar(64) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [Description] nvarchar(1024) NULL,
        [ComponentType] nvarchar(32) NOT NULL,
        [DslVersion] nvarchar(16) NOT NULL,
        [DslJson] nvarchar(max) NOT NULL,
        [PublishedDslJson] nvarchar(max) NULL,
        [PublishedVersion] int NOT NULL,
        [PublishedAt] datetime2 NULL,
        [PublishedBy] nvarchar(128) NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_CustomComponents] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema213 AS sysname;
    SET @defaultSchema213 = SCHEMA_NAME();
    DECLARE @description213 AS sql_variant;
    SET @description213 = N'租户自定义组件定义';
    EXEC sp_addextendedproperty 'MS_Description', @description213, 'SCHEMA', @defaultSchema213, 'TABLE', N'CustomComponents';
    SET @description213 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description213, 'SCHEMA', @defaultSchema213, 'TABLE', N'CustomComponents', 'COLUMN', N'Id';
    SET @description213 = N'结构化组件草稿DSL（禁止HTML/脚本）';
    EXEC sp_addextendedproperty 'MS_Description', @description213, 'SCHEMA', @defaultSchema213, 'TABLE', N'CustomComponents', 'COLUMN', N'DslJson';
    SET @description213 = N'已发布组件DSL快照';
    EXEC sp_addextendedproperty 'MS_Description', @description213, 'SCHEMA', @defaultSchema213, 'TABLE', N'CustomComponents', 'COLUMN', N'PublishedDslJson';
    SET @description213 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description213, 'SCHEMA', @defaultSchema213, 'TABLE', N'CustomComponents', 'COLUMN', N'CreatedTime';
    SET @description213 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description213, 'SCHEMA', @defaultSchema213, 'TABLE', N'CustomComponents', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907053931_M7_09_CustomComponents'
)
BEGIN
    CREATE TABLE [CustomComponentVersions] (
        [Id] bigint NOT NULL IDENTITY,
        [ComponentId] bigint NOT NULL,
        [TenantId] bigint NOT NULL,
        [Version] int NOT NULL,
        [Key] nvarchar(64) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [Description] nvarchar(1024) NULL,
        [ComponentType] nvarchar(32) NOT NULL,
        [DslVersion] nvarchar(16) NOT NULL,
        [DslJson] nvarchar(max) NOT NULL,
        [PublishedAt] datetime2 NOT NULL,
        [PublishedBy] nvarchar(128) NULL,
        [RolledBackFromVersion] int NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [RowVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint),
        CONSTRAINT [PK_CustomComponentVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomComponentVersions_CustomComponents_ComponentId] FOREIGN KEY ([ComponentId]) REFERENCES [CustomComponents] ([Id]) ON DELETE CASCADE
    );
    DECLARE @defaultSchema214 AS sysname;
    SET @defaultSchema214 = SCHEMA_NAME();
    DECLARE @description214 AS sql_variant;
    SET @description214 = N'自定义组件发布版本快照';
    EXEC sp_addextendedproperty 'MS_Description', @description214, 'SCHEMA', @defaultSchema214, 'TABLE', N'CustomComponentVersions';
    SET @description214 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description214, 'SCHEMA', @defaultSchema214, 'TABLE', N'CustomComponentVersions', 'COLUMN', N'Id';
    SET @description214 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description214, 'SCHEMA', @defaultSchema214, 'TABLE', N'CustomComponentVersions', 'COLUMN', N'CreatedTime';
    SET @description214 = N'乐观并发版本(ETag)，每次更新自增';
    EXEC sp_addextendedproperty 'MS_Description', @description214, 'SCHEMA', @defaultSchema214, 'TABLE', N'CustomComponentVersions', 'COLUMN', N'RowVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907053931_M7_09_CustomComponents'
)
BEGIN
    CREATE INDEX [IX_CustomComponents_TenantId_ComponentType] ON [CustomComponents] ([TenantId], [ComponentType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907053931_M7_09_CustomComponents'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CustomComponents_TenantId_Key] ON [CustomComponents] ([TenantId], [Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907053931_M7_09_CustomComponents'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CustomComponentVersions_ComponentId_Version] ON [CustomComponentVersions] ([ComponentId], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907053931_M7_09_CustomComponents'
)
BEGIN
    CREATE INDEX [IX_CustomComponentVersions_TenantId_ComponentId] ON [CustomComponentVersions] ([TenantId], [ComponentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907053931_M7_09_CustomComponents'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260907053931_M7_09_CustomComponents', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908051650_M7_11_AskQuerySnapshot'
)
BEGIN
    CREATE TABLE [AskQuerySnapshots] (
        [TurnId] nvarchar(64) NOT NULL,
        [TenantId] bigint NOT NULL,
        [UserId] bigint NOT NULL,
        [DataSourceId] bigint NOT NULL,
        [EntityCode] nvarchar(128) NULL,
        [QueryPlanJson] nvarchar(max) NOT NULL,
        [RequestHash] nvarchar(128) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_AskQuerySnapshots] PRIMARY KEY ([TurnId])
    );
    DECLARE @defaultSchema215 AS sysname;
    SET @defaultSchema215 = SCHEMA_NAME();
    DECLARE @description215 AS sql_variant;
    SET @description215 = N'Ask 查询快照（应用运行时引用）';
    EXEC sp_addextendedproperty 'MS_Description', @description215, 'SCHEMA', @defaultSchema215, 'TABLE', N'AskQuerySnapshots';
    SET @description215 = N'查询引用标识(GUID)';
    EXEC sp_addextendedproperty 'MS_Description', @description215, 'SCHEMA', @defaultSchema215, 'TABLE', N'AskQuerySnapshots', 'COLUMN', N'TurnId';
    SET @description215 = N'所属租户';
    EXEC sp_addextendedproperty 'MS_Description', @description215, 'SCHEMA', @defaultSchema215, 'TABLE', N'AskQuerySnapshots', 'COLUMN', N'TenantId';
    SET @description215 = N'快照创建者';
    EXEC sp_addextendedproperty 'MS_Description', @description215, 'SCHEMA', @defaultSchema215, 'TABLE', N'AskQuerySnapshots', 'COLUMN', N'UserId';
    SET @description215 = N'解析数据源Id（运行时硬约束）';
    EXEC sp_addextendedproperty 'MS_Description', @description215, 'SCHEMA', @defaultSchema215, 'TABLE', N'AskQuerySnapshots', 'COLUMN', N'DataSourceId';
    SET @description215 = N'主表业务实体语义名';
    EXEC sp_addextendedproperty 'MS_Description', @description215, 'SCHEMA', @defaultSchema215, 'TABLE', N'AskQuerySnapshots', 'COLUMN', N'EntityCode';
    SET @description215 = N'允许查询的QueryPlan语义(JSON,RLS注入前截取)';
    EXEC sp_addextendedproperty 'MS_Description', @description215, 'SCHEMA', @defaultSchema215, 'TABLE', N'AskQuerySnapshots', 'COLUMN', N'QueryPlanJson';
    SET @description215 = N'请求摘要哈希(创建幂等冲突检测)';
    EXEC sp_addextendedproperty 'MS_Description', @description215, 'SCHEMA', @defaultSchema215, 'TABLE', N'AskQuerySnapshots', 'COLUMN', N'RequestHash';
    SET @description215 = N'过期时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description215, 'SCHEMA', @defaultSchema215, 'TABLE', N'AskQuerySnapshots', 'COLUMN', N'ExpiresAt';
    SET @description215 = N'创建时间';
    EXEC sp_addextendedproperty 'MS_Description', @description215, 'SCHEMA', @defaultSchema215, 'TABLE', N'AskQuerySnapshots', 'COLUMN', N'CreatedTime';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908051650_M7_11_AskQuerySnapshot'
)
BEGIN
    CREATE INDEX [IX_AskQuerySnapshots_ExpiresAt] ON [AskQuerySnapshots] ([ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908051650_M7_11_AskQuerySnapshot'
)
BEGIN
    CREATE INDEX [IX_AskQuerySnapshots_TenantId_UserId] ON [AskQuerySnapshots] ([TenantId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908051650_M7_11_AskQuerySnapshot'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260908051650_M7_11_AskQuerySnapshot', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909025001_M7_11_PublishIdempotency'
)
BEGIN
    ALTER TABLE [AppPlans] ADD [DraftRevision] int NOT NULL DEFAULT 1;
    DECLARE @defaultSchema216 AS sysname;
    SET @defaultSchema216 = SCHEMA_NAME();
    DECLARE @description216 AS sql_variant;
    SET @description216 = N'草稿修订乐观并发令牌(每次编辑+1,发布校验用)';
    EXEC sp_addextendedproperty 'MS_Description', @description216, 'SCHEMA', @defaultSchema216, 'TABLE', N'AppPlans', 'COLUMN', N'DraftRevision';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909025001_M7_11_PublishIdempotency'
)
BEGIN
    CREATE TABLE [AppPublishIdempotencies] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] bigint NOT NULL,
        [AppCode] nvarchar(128) NOT NULL,
        [IdempotencyKey] nvarchar(128) NOT NULL,
        [ExpectedDraftRevision] int NOT NULL,
        [PublishedVersion] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AppPublishIdempotencies] PRIMARY KEY ([Id])
    );
    DECLARE @defaultSchema217 AS sysname;
    SET @defaultSchema217 = SCHEMA_NAME();
    DECLARE @description217 AS sql_variant;
    SET @description217 = N'应用发布/回滚幂等记录';
    EXEC sp_addextendedproperty 'MS_Description', @description217, 'SCHEMA', @defaultSchema217, 'TABLE', N'AppPublishIdempotencies';
    SET @description217 = N'主键';
    EXEC sp_addextendedproperty 'MS_Description', @description217, 'SCHEMA', @defaultSchema217, 'TABLE', N'AppPublishIdempotencies', 'COLUMN', N'Id';
    SET @description217 = N'作用域租户';
    EXEC sp_addextendedproperty 'MS_Description', @description217, 'SCHEMA', @defaultSchema217, 'TABLE', N'AppPublishIdempotencies', 'COLUMN', N'TenantId';
    SET @description217 = N'应用业务编码';
    EXEC sp_addextendedproperty 'MS_Description', @description217, 'SCHEMA', @defaultSchema217, 'TABLE', N'AppPublishIdempotencies', 'COLUMN', N'AppCode';
    SET @description217 = N'客户端幂等键(UUID)';
    EXEC sp_addextendedproperty 'MS_Description', @description217, 'SCHEMA', @defaultSchema217, 'TABLE', N'AppPublishIdempotencies', 'COLUMN', N'IdempotencyKey';
    SET @description217 = N'发布时的期望草稿修订号';
    EXEC sp_addextendedproperty 'MS_Description', @description217, 'SCHEMA', @defaultSchema217, 'TABLE', N'AppPublishIdempotencies', 'COLUMN', N'ExpectedDraftRevision';
    SET @description217 = N'成功发布固化的版本号(同键重入返回此值)';
    EXEC sp_addextendedproperty 'MS_Description', @description217, 'SCHEMA', @defaultSchema217, 'TABLE', N'AppPublishIdempotencies', 'COLUMN', N'PublishedVersion';
    SET @description217 = N'记录创建时间(UTC)';
    EXEC sp_addextendedproperty 'MS_Description', @description217, 'SCHEMA', @defaultSchema217, 'TABLE', N'AppPublishIdempotencies', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909025001_M7_11_PublishIdempotency'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AppPublishIdempotencies_Tenant_AppCode_Key] ON [AppPublishIdempotencies] ([TenantId], [AppCode], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909025001_M7_11_PublishIdempotency'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260909025001_M7_11_PublishIdempotency', N'10.0.11');
END;

COMMIT;
GO

