CREATE TABLE [dbo].[ServiceSchedules]
(
    [Id]          UNIQUEIDENTIFIER NOT NULL,
    [ChurchId]    UNIQUEIDENTIFIER NOT NULL,
    [CampusId]    UNIQUEIDENTIFIER NULL,
    [DayOfWeek]   TINYINT          NOT NULL,
    [StartTime]   TIME (0)         NOT NULL,
    [Description] NVARCHAR (200)   NULL,
    [CreatedAt]   DATETIME2 (7)    NOT NULL,
    [UpdatedAt]   DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_ServiceSchedules] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ServiceSchedules_Directory] FOREIGN KEY ([ChurchId]) REFERENCES [dbo].[Directory] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ServiceSchedules_Campuses] FOREIGN KEY ([CampusId]) REFERENCES [dbo].[Campuses] ([Id])
);

GO
CREATE INDEX [IX_ServiceSchedules_ChurchId]
    ON [dbo].[ServiceSchedules] ([ChurchId] ASC);
