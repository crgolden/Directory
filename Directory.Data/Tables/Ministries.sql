CREATE TABLE [dbo].[Ministries]
(
    [Id]          UNIQUEIDENTIFIER NOT NULL,
    [ChurchId]    UNIQUEIDENTIFIER NOT NULL,
    [Name]        NVARCHAR (200)   NOT NULL,
    [Description] NVARCHAR (1000)  NULL,
    [CreatedAt]   DATETIME2 (7)    NOT NULL,
    [UpdatedAt]   DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Ministries] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Ministries_Directory] FOREIGN KEY ([ChurchId]) REFERENCES [dbo].[Directory] ([Id]) ON DELETE CASCADE
);

GO
CREATE INDEX [IX_Ministries_ChurchId]
    ON [dbo].[Ministries] ([ChurchId] ASC);
