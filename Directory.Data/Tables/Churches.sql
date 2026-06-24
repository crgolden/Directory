CREATE TABLE [dbo].[Churches]
(
    [Id]                   UNIQUEIDENTIFIER NOT NULL,
    [CanonicalName]        NVARCHAR (300)   NOT NULL,
    [Slug]                 NVARCHAR (320)   NOT NULL,
    [Latitude]             FLOAT            NOT NULL,
    [Longitude]            FLOAT            NOT NULL,
    [Street]               NVARCHAR (200)   NULL,
    [City]                 NVARCHAR (100)   NOT NULL,
    [State]                NCHAR (2)        NOT NULL,
    [Zip]                  NVARCHAR (10)    NOT NULL,
    [PhoneNumber]          NVARCHAR (20)    NULL,
    [Website]              NVARCHAR (500)   NULL,
    [EmailAddress]         NVARCHAR (254)   NULL,
    [DenominationId]       UNIQUEIDENTIFIER NULL,
    [WorshipStyle]         INT              NOT NULL DEFAULT (0),
    [PrimaryLanguage]      NVARCHAR (50)    NOT NULL DEFAULT (N'English'),
    [AcceptsLGBTQ]         BIT              NULL,
    [WheelchairAccessible] BIT              NULL,
    [HasNursery]           BIT              NULL,
    [HasYouthProgram]      BIT              NULL,
    [ConfidenceScore]      DECIMAL (5, 4)   NOT NULL DEFAULT (0),
    [LastVerifiedAt]       DATETIME2 (7)    NULL,
    [CreatedAt]            DATETIME2 (7)    NOT NULL,
    [UpdatedAt]            DATETIME2 (7)    NOT NULL,
    [IsActive]             BIT              NOT NULL DEFAULT (1),
    CONSTRAINT [PK_Churches] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_Churches_Slug] UNIQUE ([Slug]),
    CONSTRAINT [FK_Churches_Denominations] FOREIGN KEY ([DenominationId]) REFERENCES [dbo].[Denominations] ([Id])
);

GO
CREATE INDEX [IX_Churches_Location]
    ON [dbo].[Churches] ([Latitude] ASC, [Longitude] ASC);

GO
CREATE INDEX [IX_Churches_State_City]
    ON [dbo].[Churches] ([State] ASC, [City] ASC);

GO
CREATE INDEX [IX_Churches_IsActive]
    ON [dbo].[Churches] ([IsActive] ASC);

GO
CREATE FULLTEXT CATALOG [ChurchesFTCatalog] AS DEFAULT;

GO
CREATE FULLTEXT INDEX ON [dbo].[Churches] ([CanonicalName], [City])
    KEY INDEX [PK_Churches]
    ON [ChurchesFTCatalog];
