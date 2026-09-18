CREATE TABLE [dbo].[MergeAuditLog]
(
    [Id]               UNIQUEIDENTIFIER NOT NULL,
    [SurvivingId]      UNIQUEIDENTIFIER NOT NULL,
    [AbsorbedId]       UNIQUEIDENTIFIER NOT NULL,
    [MergedBy]         UNIQUEIDENTIFIER NULL,
    [MergedAt]         DATETIMEOFFSET (7) NOT NULL,
    [FieldsOverridden] NVARCHAR (MAX)   NULL,
    CONSTRAINT [PK_MergeAuditLog] PRIMARY KEY CLUSTERED ([Id] ASC)
);
