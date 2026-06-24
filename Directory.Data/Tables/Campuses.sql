CREATE TABLE [dbo].[Campuses]
(
    [Id]        UNIQUEIDENTIFIER NOT NULL,
    [ChurchId]  UNIQUEIDENTIFIER NOT NULL,
    [Name]      NVARCHAR (200)   NOT NULL,
    [Street]    NVARCHAR (200)   NULL,
    [City]      NVARCHAR (100)   NOT NULL,
    [State]     NCHAR (2)        NOT NULL,
    [Zip]       NVARCHAR (10)    NOT NULL,
    [Latitude]  FLOAT            NOT NULL,
    [Longitude] FLOAT            NOT NULL,
    [CreatedAt] DATETIME2 (7)    NOT NULL,
    [UpdatedAt] DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Campuses] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Campuses_Churches] FOREIGN KEY ([ChurchId]) REFERENCES [dbo].[Churches] ([Id]) ON DELETE CASCADE
);

GO
CREATE INDEX [IX_Campuses_ChurchId]
    ON [dbo].[Campuses] ([ChurchId] ASC);
