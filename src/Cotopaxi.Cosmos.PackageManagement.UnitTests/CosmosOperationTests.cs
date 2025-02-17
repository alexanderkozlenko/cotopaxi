using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cotopaxi.Cosmos.PackageManagement.UnitTests;

[TestClass]
public sealed class CosmosOperationTests
{
    [DataTestMethod]
    [DataRow("delete", true, CosmosOperationType.Delete)]
    [DataRow("create", true, CosmosOperationType.Create)]
    [DataRow("upsert", true, CosmosOperationType.Upsert)]
    [DataRow("patch", true, CosmosOperationType.Patch)]
    [DataRow("replace", false, default(CosmosOperationType))]
    [DataRow("DELETE", true, CosmosOperationType.Delete)]
    [DataRow("CREATE", true, CosmosOperationType.Create)]
    [DataRow("UPSERT", true, CosmosOperationType.Upsert)]
    [DataRow("PATCH", true, CosmosOperationType.Patch)]
    [DataRow("REPLACE", false, default(CosmosOperationType))]
    public void TryParse(string? source, bool expectedResult, CosmosOperationType expectedValue)
    {
        var actualResult = CosmosOperation.TryParse(source, out var actualValue);

        Assert.AreEqual(expectedResult, actualResult);
        Assert.AreEqual(expectedValue, actualValue);
    }

    [DataTestMethod]
    [DataRow(CosmosOperationType.Delete, "delete")]
    [DataRow(CosmosOperationType.Create, "create")]
    [DataRow(CosmosOperationType.Upsert, "upsert")]
    [DataRow(CosmosOperationType.Patch, "patch")]
    public void TryParse(CosmosOperationType value, string expectedResult)
    {
        var actualResult = CosmosOperation.Format(value);

        Assert.AreEqual(expectedResult, actualResult);
    }
}
