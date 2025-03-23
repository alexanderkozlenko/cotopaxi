using Cotopaxi.Cosmos.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cotopaxi.Cosmos.PackageManagement.UnitTests;

[TestClass]
public sealed class ProjectSourceComparerTests
{
    [DataTestMethod]
    [DataRow(
        "adventureworks/products/bikes.json", "adventureworks", "products", PackageOperationType.Upsert,
        "adventureworks/products/bikes.json", "adventureworks", "products", PackageOperationType.Upsert,
        true)]
    [DataRow(
        "adventureworks/products/bikes.json", "adventureworks", "products", PackageOperationType.Upsert,
        "AdventureWorks/Products/Bikes.json", "adventureworks", "products", PackageOperationType.Upsert,
        true)]
    [DataRow(
        "adventureworks/products/bikes.json", "adventureworks", "products", PackageOperationType.Upsert,
        "adventureworks/products/bikes.json", "AdventureWorks", "products", PackageOperationType.Upsert,
        false)]
    [DataRow(
        "adventureworks/products/bikes.json", "adventureworks", "products", PackageOperationType.Upsert,
        "adventureworks/products/bikes.json", "adventureworks", "Products", PackageOperationType.Upsert,
        false)]
    public void Equals(
        string filePath1, string database1, string container1, PackageOperationType operation1Type,
        string filePath2, string database2, string container2, PackageOperationType operation2Type,
        bool expected)
    {
        var projectSource1 = new ProjectSource(filePath1, database1, container1, operation1Type);
        var projectSource2 = new ProjectSource(filePath2, database2, container2, operation2Type);

        var comparer = ProjectSourceComparer.Instance;
        var actual = comparer.Equals(projectSource1, projectSource2);

        Assert.AreEqual(expected, actual);
    }
}
