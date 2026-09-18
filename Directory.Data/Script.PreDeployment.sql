SET XACT_ABORT ON;

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[UserCorrections]')
      AND name = N'UserId'
      AND TYPE_NAME(system_type_id) = N'nvarchar')
BEGIN
    EXEC (N'
        IF EXISTS (
            SELECT 1 FROM [dbo].[UserCorrections]
            WHERE ([UserId] <> N''system'' AND TRY_CONVERT(UNIQUEIDENTIFIER, [UserId]) IS NULL)
               OR ([ReviewedBy] <> N''system-auto-merge'' AND TRY_CONVERT(UNIQUEIDENTIFIER, [ReviewedBy]) IS NULL))
        BEGIN
            THROW 50001, N''UserCorrections holds a UserId that is neither a GUID nor system, or a ReviewedBy that is neither a GUID nor system-auto-merge. Correct those rows before publishing.'', 1;
        END;');

    BEGIN TRANSACTION;

    IF OBJECT_ID(N'[dbo].[UserCorrections_GuidMigration]') IS NULL
    BEGIN
        CREATE TABLE [dbo].[UserCorrections_GuidMigration]
        (
            [Id]          UNIQUEIDENTIFIER   NOT NULL PRIMARY KEY,
            [ChurchId]    UNIQUEIDENTIFIER   NOT NULL,
            [UserId]      UNIQUEIDENTIFIER   NULL,
            [Field]       NVARCHAR (100)     NOT NULL,
            [OldValue]    NVARCHAR (1000)    NULL,
            [NewValue]    NVARCHAR (1000)    NOT NULL,
            [Status]      INT                NOT NULL,
            [ReviewedBy]  UNIQUEIDENTIFIER   NULL,
            [ReviewedAt]  DATETIMEOFFSET (7) NULL,
            [CreatedAt]   DATETIMEOFFSET (7) NOT NULL
        );
    END;

    EXEC (N'
        INSERT INTO [dbo].[UserCorrections_GuidMigration]
            ([Id], [ChurchId], [UserId], [Field], [OldValue], [NewValue], [Status], [ReviewedBy], [ReviewedAt], [CreatedAt])
        SELECT
            [Id],
            [ChurchId],
            CASE WHEN [UserId] = N''system'' THEN NULL ELSE CONVERT(UNIQUEIDENTIFIER, [UserId]) END,
            [Field],
            [OldValue],
            [NewValue],
            [Status],
            CASE WHEN [ReviewedBy] = N''system-auto-merge'' THEN NULL ELSE CONVERT(UNIQUEIDENTIFIER, [ReviewedBy]) END,
            [ReviewedAt],
            [CreatedAt]
        FROM [dbo].[UserCorrections];');

    DELETE FROM [dbo].[UserCorrections];

    COMMIT TRANSACTION;
END;

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[MergeAuditLog]')
      AND name = N'MergedBy'
      AND TYPE_NAME(system_type_id) = N'nvarchar')
BEGIN
    EXEC (N'
        IF EXISTS (SELECT 1 FROM [dbo].[MergeAuditLog] WHERE [MergedBy] <> N''system-auto-merge'' AND TRY_CONVERT(UNIQUEIDENTIFIER, [MergedBy]) IS NULL)
        BEGIN
            THROW 50002, N''MergeAuditLog holds a MergedBy that is neither a GUID nor system-auto-merge. Correct those rows before publishing.'', 1;
        END;');

    BEGIN TRANSACTION;

    IF OBJECT_ID(N'[dbo].[MergeAuditLog_GuidMigration]') IS NULL
    BEGIN
        CREATE TABLE [dbo].[MergeAuditLog_GuidMigration]
        (
            [Id]               UNIQUEIDENTIFIER   NOT NULL PRIMARY KEY,
            [SurvivingId]      UNIQUEIDENTIFIER   NOT NULL,
            [AbsorbedId]       UNIQUEIDENTIFIER   NOT NULL,
            [MergedBy]         UNIQUEIDENTIFIER   NULL,
            [MergedAt]         DATETIMEOFFSET (7) NOT NULL,
            [FieldsOverridden] NVARCHAR (MAX)     NULL
        );
    END;

    EXEC (N'
        INSERT INTO [dbo].[MergeAuditLog_GuidMigration]
            ([Id], [SurvivingId], [AbsorbedId], [MergedBy], [MergedAt], [FieldsOverridden])
        SELECT [Id], [SurvivingId], [AbsorbedId], CASE WHEN [MergedBy] = N''system-auto-merge'' THEN NULL ELSE CONVERT(UNIQUEIDENTIFIER, [MergedBy]) END, [MergedAt], [FieldsOverridden]
        FROM [dbo].[MergeAuditLog];');

    DELETE FROM [dbo].[MergeAuditLog];

    COMMIT TRANSACTION;
END;
