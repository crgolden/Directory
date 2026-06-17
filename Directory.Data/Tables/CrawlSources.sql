CREATE TABLE [dbo].[CrawlSources]
(
    [Id]            UNIQUEIDENTIFIER NOT NULL,
    [ChurchId]      UNIQUEIDENTIFIER NULL,
    [Url]           NVARCHAR (2048)  NOT NULL,
    [LastCrawledAt] DATETIME2 (7)    NULL,
    [LastStatus]    INT              NOT NULL DEFAULT (0),
    [CreatedAt]     DATETIME2 (7)    NOT NULL,
    [UpdatedAt]     DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_CrawlSources] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_CrawlSources_Directory] FOREIGN KEY ([ChurchId]) REFERENCES [dbo].[Directory] ([Id])
);

GO
CREATE INDEX [IX_CrawlSources_ChurchId]
    ON [dbo].[CrawlSources] ([ChurchId] ASC);
