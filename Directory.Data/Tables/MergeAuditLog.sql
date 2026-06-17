CREATE TABLE [dbo].[MergeAuditLog]
(
    [Id]               UNIQUEIDENTIFIER NOT NULL,
    [SurvivingId]      UNIQUEIDENTIFIER NOT NULL,
    [AbsorbedId]       UNIQUEIDENTIFIER NOT NULL,
    [MergedBy]         NVARCHAR (100)   NOT NULL,
    [MergedAt]         DATETIME2 (7)    NOT NULL,
    [FieldsOverridden] NVARCHAR (MAX)   NULL,
    CONSTRAINT [PK_MergeAuditLog] PRIMARY KEY CLUSTERED ([Id] ASC)
);
