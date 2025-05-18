using System.IO.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cotopaxi.Cosmos.Packaging.UnitTests;

[TestClass]
public sealed class PackageModelTests
{
    [TestMethod]
    public async Task CreateSaveOpen()
    {
        using var memoryStream = new MemoryStream();

        using (var package = Package.Open(memoryStream, FileMode.Create, FileAccess.ReadWrite))
        {
            using (var packageModel = await PackageModel.OpenAsync(package, default, default).ConfigureAwait(false))
            {
                var packagePartition = new PackagePartition("adventureworks", "products", PackageOperationType.Upsert);
                var packagePartitionUri = packageModel.CreatePartition(packagePartition);

                Assert.IsNotNull(packagePartitionUri);
                Assert.IsFalse(packagePartitionUri.IsAbsoluteUri);

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
                Assert.AreEqual("adventureworks", packagePartition.DatabaseName);
                Assert.AreEqual("products", packagePartition.ContainerName);
                Assert.AreEqual(PackageOperationType.Upsert, packagePartition.OperationType);
            }
        }
    }
}
