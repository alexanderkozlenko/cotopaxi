using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cotopaxi.Cosmos.PackageManagement.UnitTests;

[TestClass]
public sealed class CosmosOperationTests
{
    [DataTestMethod]
    [DataRow("delete", true)]
    [DataRow("create", true)]
    [DataRow("upsert", true)]
    [DataRow("patch", true)]
    [DataRow("replace", false)]
    [DataRow("DELETE", true)]
    [DataRow("CREATE", true)]
    [DataRow("UPSERT", true)]
    [DataRow("PATCH", true)]
    [DataRow("REPLACE", false)]
    public void IsSupported(string? value, bool expected)
    {
        var actual = CosmosOperation.IsSupported(value);

        Assert.AreEqual(expected, actual);
    }
}
