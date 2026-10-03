namespace Directory.Tests.Integration;

using Directory.Tests.Integration.TestSupport;

[Trait("Category", "Integration")]
public sealed class TestDatabaseContractTests
{
    [Fact]
    public void IsDisposableCatalog_TriageSuffix_ReturnsTrue()
    {
        var triageCatalog = Generated.NewDatabaseName() + TestDatabaseContractConstants.TriageCatalogSuffix;

        Assert.True(TestDatabaseContract.IsDisposableCatalog(triageCatalog));
    }

    [Fact]
    public void IsDisposableCatalog_TestSuffix_ReturnsTrue()
    {
        var testCatalog = Generated.NewDatabaseName() + TestDatabaseContractConstants.TestCatalogSuffix;

        Assert.True(TestDatabaseContract.IsDisposableCatalog(testCatalog));
    }

    [Fact]
    public void IsDisposableCatalog_NeitherSuffix_ReturnsFalse()
    {
        var productionLikeCatalog = Generated.NewDatabaseName();

        Assert.False(TestDatabaseContract.IsDisposableCatalog(productionLikeCatalog));
    }

    [Fact]
    public void IsDisposableCatalog_TriageSuffixBeforeTheEnd_ReturnsFalse()
    {
        var triageMidNameCatalog = TestDatabaseContractConstants.TriageCatalogSuffix + Generated.NewDatabaseName();

        Assert.False(TestDatabaseContract.IsDisposableCatalog(triageMidNameCatalog));
    }

    [Fact]
    public void IsDisposableCatalog_LowercaseTriageSuffix_ReturnsFalse()
    {
        var lowercaseSuffixCatalog = Generated.NewDatabaseName() + TestDatabaseContractConstants.TriageCatalogSuffix.ToLowerInvariant();

        Assert.False(TestDatabaseContract.IsDisposableCatalog(lowercaseSuffixCatalog));
    }

    [Fact]
    public void IsDisposableCatalog_Null_ReturnsFalse()
    {
        Assert.False(TestDatabaseContract.IsDisposableCatalog(null));
    }
}
