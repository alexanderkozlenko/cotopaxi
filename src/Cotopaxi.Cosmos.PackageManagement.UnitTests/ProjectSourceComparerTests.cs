using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cotopaxi.Cosmos.PackageManagement.UnitTests;

[TestClass]
public sealed class ProjectSourceComparerTests
{
    [DataTestMethod]
    [DataRow(
        "adventureworks/products/bikes.json", "adventureworks", "products", "upsert",
        "adventureworks/products/bikes.json", "adventureworks", "products", "upsert",
        true)]
    [DataRow(
        "adventureworks/products/bikes.json", "adventureworks", "products", "upsert",
        "AdventureWorks/Products/Bikes.json", "adventureworks", "products", "upsert",
        true)]
    [DataRow(
        "adventureworks/products/bikes.json", "adventureworks", "products", "upsert",
        "adventureworks/products/bikes.json", "AdventureWorks", "products", "upsert",
        false)]
    [DataRow(
        "adventureworks/products/bikes.json", "adventureworks", "products", "upsert",
        "adventureworks/products/bikes.json", "adventureworks", "Products", "upsert",
        false)]
    [DataRow(
        "adventureworks/products/bikes.json", "adventureworks", "products", "upsert",
        "adventureworks/products/bikes.json", "adventureworks", "products", "UPSERT",
        true)]
    public void Equals(
        string filePath1, string database1, string container1, string operation1,
        string filePath2, string database2, string container2, string operation2,
        bool expected)
    {
        var projectSource1 = new ProjectSource(filePath1, database1, container1, operation1);
        var projectSource2 = new ProjectSource(filePath2, database2, container2, operation2);

        var comparer = ProjectSourceComparer.Instance;
        var actual = comparer.Equals(projectSource1, projectSource2);

        Assert.AreEqual(expected, actual);
    }
}
