CREATE TABLE [dbo].[UserCorrections]
(
    [Id]          UNIQUEIDENTIFIER NOT NULL,
    [ChurchId]    UNIQUEIDENTIFIER NOT NULL,
    [UserId]      NVARCHAR (100)   NOT NULL,
    [Field]       NVARCHAR (100)   NOT NULL,
    [OldValue]    NVARCHAR (1000)  NULL,
    [NewValue]    NVARCHAR (1000)  NOT NULL,
    [Status]      INT              NOT NULL DEFAULT (0),
    [ReviewedBy]  NVARCHAR (100)   NULL,
    [ReviewedAt]  DATETIME2 (7)    NULL,
    [CreatedAt]   DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_UserCorrections] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_UserCorrections_Churches] FOREIGN KEY ([ChurchId]) REFERENCES [dbo].[Churches] ([Id]) ON DELETE CASCADE
);

GO
CREATE INDEX [IX_UserCorrections_ChurchId]
    ON [dbo].[UserCorrections] ([ChurchId] ASC);

GO
CREATE INDEX [IX_UserCorrections_Status]
    ON [dbo].[UserCorrections] ([Status] ASC);
