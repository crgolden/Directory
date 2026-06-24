/*
Post-deployment seed: canonical denomination reference data.

Idempotent — only inserts denominations whose Name is not already present, so it is safe to run on
every publish. Churches reference these by [DenominationId]; the Functions ChurchWriter resolves an
incoming denomination name to one of these rows (unknown names are left null, never inserted ad hoc).
*/
MERGE INTO [dbo].[Denominations] AS target
USING (VALUES
    (N'Roman Catholic'),
    (N'Eastern Orthodox'),
    (N'Greek Orthodox'),
    (N'Coptic Orthodox'),
    (N'Baptist'),
    (N'Southern Baptist'),
    (N'Methodist'),
    (N'United Methodist'),
    (N'Lutheran'),
    (N'Presbyterian'),
    (N'Anglican'),
    (N'Episcopal'),
    (N'Pentecostal'),
    (N'Assemblies of God'),
    (N'Non-denominational'),
    (N'Evangelical'),
    (N'Reformed'),
    (N'Christian Reformed'),
    (N'Congregational'),
    (N'Adventist'),
    (N'Seventh-day Adventist'),
    (N'Latter-day Saints'),
    (N'Jehovah''s Witnesses'),
    (N'Quaker'),
    (N'Mennonite'),
    (N'Amish'),
    (N'Brethren'),
    (N'Nazarene'),
    (N'Church of Christ'),
    (N'Disciples of Christ'),
    (N'Wesleyan'),
    (N'Foursquare'),
    (N'Calvary Chapel'),
    (N'Vineyard'),
    (N'Unitarian Universalist'),
    (N'Salvation Army'),
    (N'Apostolic'),
    (N'Holiness'),
    (N'Charismatic'),
    (N'Messianic Jewish'),
    (N'Orthodox')
) AS source ([Name])
    ON target.[Name] = source.[Name]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Id], [Name], [CreatedAt], [UpdatedAt])
    VALUES (NEWID(), source.[Name], SYSUTCDATETIME(), SYSUTCDATETIME());
