using System.IO.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cotopaxi.Cosmos.Packaging.UnitTests;

[TestClass]
public sealed class PackageModelTests
{
    [TestMethod]
    public async Task OpenSaveOpen()
    {
        using var memoryStream = new MemoryStream();

        using (var package = Package.Open(memoryStream, FileMode.Create, FileAccess.ReadWrite))
        {
            using (var packageModel = await PackageModel.OpenAsync(package, default, default).ConfigureAwait(false))
            {
                var packagePartition = new PackagePartition(
                    new("0c2deaa0-9517-4958-9bb9-2444ca352705"),
                    "adventureworks",
                    "products",
                    PackageOperationType.Upsert);

                var partitionUri = packageModel.CreatePartition(packagePartition);

                Assert.IsNotNull(partitionUri);
                Assert.IsFalse(partitionUri.IsAbsoluteUri);

                await packageModel.SaveAsync(default).ConfigureAwait(false);
            }
        }

        memoryStream.Seek(0, SeekOrigin.Begin);

        using (var package = Package.Open(memoryStream, FileMode.Open, FileAccess.Read))
        {
            using (var packageModel = await PackageModel.OpenAsync(package, default, default).ConfigureAwait(false))
            {
                var packagePartitions = packageModel.GetPartitions();

                Assert.IsNotNull(packagePartitions);
                Assert.AreEqual(1, packagePartitions.Count);

                var (packagePartitionKey, packagePartition) = packagePartitions.First();

                Assert.IsNotNull(packagePartition);
                Assert.IsFalse(packagePartitionKey.IsAbsoluteUri);
                Assert.AreEqual(new("0c2deaa0-9517-4958-9bb9-2444ca352705"), packagePartition.PartitionKey);
                Assert.AreEqual("adventureworks", packagePartition.DatabaseName);
                Assert.AreEqual("products", packagePartition.ContainerName);
                Assert.AreEqual(PackageOperationType.Upsert, packagePartition.OperationType);
            }
        }
    }

    [TestMethod]
    public async Task CreatePartitionDuplicate()
    {
        using var memoryStream = new MemoryStream();

        using (var package = Package.Open(memoryStream, FileMode.Create, FileAccess.ReadWrite))
        {
            using (var packageModel = await PackageModel.OpenAsync(package, default, default).ConfigureAwait(false))
            {
                var packagePartition = new PackagePartition(
                    new("0c2deaa0-9517-4958-9bb9-2444ca352705"),
                    "adventureworks",
                    "products",
                    PackageOperationType.Upsert);

                packageModel.CreatePartition(packagePartition);

                Assert.ThrowsExactly<InvalidOperationException>(() =>
                    packageModel.CreatePartition(packagePartition));
            }
        }
    }
}
