using System.IO.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cotopaxi.Cosmos.PackageManagement.UnitTests;

[TestClass]
public sealed class PackageModelTests
{
    [TestMethod]
    public async Task Universal()
    {
        using var memoryStream = new MemoryStream();

        using (var package = Package.Open(memoryStream, FileMode.Create, FileAccess.ReadWrite))
        {
            using (var packageModel = await PackageModel.OpenAsync(package, default, default).ConfigureAwait(false))
            {
                var partitionUri = packageModel.CreatePartition(
                    "0c2deaa0-9517-4958-9bb9-2444ca352705",
                    "adventureworks",
                    "products",
                    "upsert");

                Assert.IsNotNull(partitionUri);
                Assert.IsFalse(partitionUri.IsAbsoluteUri);

                await packageModel.SaveAsync().ConfigureAwait(false);
            }
        }

        memoryStream.Seek(0, SeekOrigin.Begin);

        using (var package = Package.Open(memoryStream, FileMode.Open, FileAccess.Read))
        {
            using (var packageModel = await PackageModel.OpenAsync(package, default, default).ConfigureAwait(false))
            {
                var packagePartitions = packageModel.GetPartitions();

                Assert.IsNotNull(packagePartitions);
                Assert.AreEqual(1, packagePartitions.Length);

                var packagePartition = packagePartitions[0];

                Assert.IsNotNull(packagePartition);
                Assert.IsFalse(packagePartition.PartitionUri.IsAbsoluteUri);
                Assert.AreEqual("0c2deaa0-9517-4958-9bb9-2444ca352705", packagePartition.PartitionName);
                Assert.AreEqual("adventureworks", packagePartition.DatabaseName);
                Assert.AreEqual("products", packagePartition.ContainerName);
                Assert.AreEqual("upsert", packagePartition.OperationName);
            }
        }
    }
}
