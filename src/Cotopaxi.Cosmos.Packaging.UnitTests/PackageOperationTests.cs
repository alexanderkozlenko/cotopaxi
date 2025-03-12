using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cotopaxi.Cosmos.Packaging.UnitTests;

[TestClass]
public sealed class PackageOperationTests
{
    [DataTestMethod]
    [DataRow("delete", true, PackageOperationType.Delete)]
    [DataRow("create", true, PackageOperationType.Create)]
    [DataRow("upsert", true, PackageOperationType.Upsert)]
    [DataRow("patch", true, PackageOperationType.Patch)]
    [DataRow("replace", false, default(PackageOperationType))]
    [DataRow("DELETE", true, PackageOperationType.Delete)]
    [DataRow("CREATE", true, PackageOperationType.Create)]
    [DataRow("UPSERT", true, PackageOperationType.Upsert)]
    [DataRow("PATCH", true, PackageOperationType.Patch)]
    [DataRow("REPLACE", false, default(PackageOperationType))]
    public void TryParse(string? source, bool expectedResult, PackageOperationType expectedValue)
    {
        var actualResult = PackageOperation.TryParse(source, out var actualValue);

        Assert.AreEqual(expectedResult, actualResult);
        Assert.AreEqual(expectedValue, actualValue);
    }

    [DataTestMethod]
    [DataRow(PackageOperationType.Delete, "delete")]
    [DataRow(PackageOperationType.Create, "create")]
    [DataRow(PackageOperationType.Upsert, "upsert")]
    [DataRow(PackageOperationType.Patch, "patch")]
    public void Format(PackageOperationType value, string expectedResult)
    {
        var actualResult = PackageOperation.Format(value);

        Assert.AreEqual(expectedResult, actualResult);
    }
}
