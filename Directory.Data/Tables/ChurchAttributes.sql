CREATE TABLE [dbo].[ChurchAttributes]
(
    [Id]         UNIQUEIDENTIFIER NOT NULL,
    [ChurchId]   UNIQUEIDENTIFIER NOT NULL,
    [Key]        NVARCHAR (100)   NOT NULL,
    [Value]      NVARCHAR (1000)  NOT NULL,
    [Source]     NVARCHAR (100)   NOT NULL,
    [Confidence] DECIMAL (5, 4)   NOT NULL DEFAULT (0),
    [CreatedAt]  DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt]  DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_ChurchAttributes] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ChurchAttributes_Churches] FOREIGN KEY ([ChurchId]) REFERENCES [dbo].[Churches] ([Id]) ON DELETE CASCADE
);

GO
CREATE INDEX [IX_ChurchAttributes_ChurchId]
    ON [dbo].[ChurchAttributes] ([ChurchId] ASC);
