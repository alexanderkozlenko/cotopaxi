#pragma warning disable CA1806

namespace Cotopaxi.Cosmos.PackageManagement.UnitTests;

[TestClass]
public sealed class CosmosResourceNameTests
{
    [TestMethod]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow(" ", true)]
    [DataRow("1", true)]
    [DataRow("a", true)]
    [DataRow("/", true)]
    [DataRow("\\", true)]
    [DataRow("#", true)]
    [DataRow("?", true)]
    public void Constructor(string value, bool expected)
    {
        if (expected)
        {
            new CosmosResourceName(value);
        }
        else
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new CosmosResourceName(value));
        }
    }
}
