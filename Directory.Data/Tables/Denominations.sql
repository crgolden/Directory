CREATE TABLE [dbo].[Denominations]
(
    [Id]        UNIQUEIDENTIFIER NOT NULL,
    [Name]      NVARCHAR (200)   NOT NULL,
    [CreatedAt] DATETIME2 (7)    NOT NULL,
    [UpdatedAt] DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Denominations] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_Denominations_Name] UNIQUE ([Name])
);
