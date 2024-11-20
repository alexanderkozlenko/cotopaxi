using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cotopaxi.Cosmos.PackageManagement.UnitTests;

[TestClass]
public sealed class PackageOperationComparerTests
{
    [DataTestMethod]
    [DataRow("delete", "delete", +0)]
    [DataRow("delete", "create", -1)]
    [DataRow("delete", "upsert", -1)]
    [DataRow("create", "delete", +1)]
    [DataRow("create", "create", +0)]
    [DataRow("create", "upsert", -1)]
    [DataRow("upsert", "delete", +1)]
    [DataRow("upsert", "create", +1)]
    [DataRow("upsert", "upsert", +0)]
    [DataRow("delete", "DELETE", +0)]
    [DataRow("delete", "CREATE", -1)]
    [DataRow("delete", "UPSERT", -1)]
    [DataRow("create", "DELETE", +1)]
    [DataRow("create", "CREATE", +0)]
    [DataRow("create", "UPSERT", -1)]
    [DataRow("upsert", "DELETE", +1)]
    [DataRow("upsert", "CREATE", +1)]
    [DataRow("upsert", "UPSERT", +0)]
    [DataRow("DELETE", "delete", +0)]
    [DataRow("DELETE", "create", -1)]
    [DataRow("DELETE", "upsert", -1)]
    [DataRow("CREATE", "delete", +1)]
    [DataRow("CREATE", "create", +0)]
    [DataRow("CREATE", "upsert", -1)]
    [DataRow("UPSERT", "delete", +1)]
    [DataRow("UPSERT", "create", +1)]
    [DataRow("UPSERT", "upsert", +0)]
    [DataRow("DELETE", "DELETE", +0)]
    [DataRow("DELETE", "CREATE", -1)]
    [DataRow("DELETE", "UPSERT", -1)]
    [DataRow("CREATE", "DELETE", +1)]
    [DataRow("CREATE", "CREATE", +0)]
    [DataRow("CREATE", "UPSERT", -1)]
    [DataRow("UPSERT", "DELETE", +1)]
    [DataRow("UPSERT", "CREATE", +1)]
    [DataRow("UPSERT", "UPSERT", +0)]
    public void Compare(string? x, string? y, int expected)
    {
        var comparer = PackageOperationComparer.Instance;
        var actual = comparer.Compare(x, y);

        Assert.AreEqual(expected, actual);
    }
}
