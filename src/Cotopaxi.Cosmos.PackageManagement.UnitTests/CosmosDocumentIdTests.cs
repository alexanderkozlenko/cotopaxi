#pragma warning disable CA1806

namespace Cotopaxi.Cosmos.PackageManagement.UnitTests;

[TestClass]
public sealed class CosmosDocumentIdTests
{
    [TestMethod]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow(" ", true)]
    [DataRow("1", true)]
    [DataRow("a", true)]
    [DataRow("/", false)]
    [DataRow("\\", false)]
    [DataRow("#", true)]
    [DataRow("?", true)]
    public void Constructor(string value, bool expected)
    {
        if (expected)
        {
            new CosmosDocumentId(value);
        }
        else
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new CosmosDocumentId(value));
        }
    }

    [TestMethod]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow(" ", true)]
    [DataRow("1", true)]
    [DataRow("a", true)]
    [DataRow("/", false)]
    [DataRow("\\", false)]
    [DataRow("#", true)]
    [DataRow("?", true)]
    public void IsWellFormed(string value, bool expected)
    {
        var result = CosmosDocumentId.IsWellFormed(value);

        Assert.AreEqual(expected, result);
    }
}
