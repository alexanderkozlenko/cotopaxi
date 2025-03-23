using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cotopaxi.Cosmos.PackageManagement.UnitTests;

[TestClass]
public sealed class UuidTests
{
    [TestMethod]
    [DataRow("a", "a", true)]
    [DataRow("a", "A", false)]
    [DataRow("a", "b", false)]
    [DataRow("a", "B", false)]
    [DataRow("A", "a", false)]
    [DataRow("A", "A", true)]
    [DataRow("A", "b", false)]
    [DataRow("A", "B", false)]
    public void CreateVersion8(string source1, string source2, bool expected)
    {
        var uuid1 = Uuid.CreateVersion8(source1);
        var uuid2 = Uuid.CreateVersion8(source2);

        var actual = uuid1 == uuid2;

        Assert.AreEqual(expected, actual);
    }
}
