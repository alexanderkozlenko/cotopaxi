using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cotopaxi.Cosmos.Packaging.UnitTests;

[TestClass]
public sealed class DatabasePackageTests
{
    [TestMethod]
    public async Task CreatePartition()
    {
        using var memoryStream = new MemoryStream();

        await using (var package = await DatabasePackage.OpenAsync(memoryStream, FileMode.Create, FileAccess.ReadWrite))
        {
            var packagePartition = package.CreatePartition("adventureworks", "products", DatabaseOperationType.Upsert);

            Assert.IsNotNull(packagePartition);
            Assert.IsFalse(packagePartition.Uri.IsAbsoluteUri);
            Assert.AreEqual("adventureworks", packagePartition.DatabaseName);
            Assert.AreEqual("products", packagePartition.ContainerName);
            Assert.AreEqual(DatabaseOperationType.Upsert, packagePartition.OperationType);

            await using var packagePartitionStream = packagePartition.GetStream(FileMode.Create, FileAccess.Write);

            Assert.IsNotNull(packagePartitionStream);

            var documents = new JsonObject[]
            {
                new()
                {
                    ["id"] = "5f3edc36-17c0-4e11-a6da-a81440214abe",
                    ["pk"] = "59fcfd99-4016-4eba-a9cd-4d44cae02282",
                },
            };

            await JsonSerializer.SerializeAsync(packagePartitionStream, documents, JsonSerializerOptions.Default);
        }
    }

    [TestMethod]
    public async Task CreatePartitionWhenOpenReadOnly()
    {
        using var memoryStream = new MemoryStream();

        await using (var package = await DatabasePackage.OpenAsync(memoryStream, FileMode.Create, FileAccess.ReadWrite))
        {
        }

        memoryStream.Seek(0, SeekOrigin.Begin);

        await using (var package = await DatabasePackage.OpenAsync(memoryStream, FileMode.Open, FileAccess.Read))
        {
            Assert.ThrowsExactly<IOException>(
                () => package.CreatePartition("adventureworks", "products", DatabaseOperationType.Upsert));
        }
    }

    [TestMethod]
    public async Task CreatePartitionWhenCreateWriteOnly()
    {
        using var memoryStream = new MemoryStream();

        await using (var package = await DatabasePackage.OpenAsync(memoryStream, FileMode.Create, FileAccess.Write))
        {
            Assert.ThrowsExactly<IOException>(
                () => package.CreatePartition("adventureworks", "products", DatabaseOperationType.Upsert));
        }
    }

    [TestMethod]
    public async Task GetPartitionsWhenCount0()
    {
        using var memoryStream = new MemoryStream();

        await using (var package = await DatabasePackage.OpenAsync(memoryStream, FileMode.Create, FileAccess.ReadWrite))
        {
        }

        memoryStream.Seek(0, SeekOrigin.Begin);

        await using (var package = await DatabasePackage.OpenAsync(memoryStream, FileMode.Open, FileAccess.Read))
        {
            var packagePartitions = package.GetPartitions();

            Assert.IsNotNull(packagePartitions);
            Assert.AreEqual(0, packagePartitions.Length);
        }
    }

    [TestMethod]
    public async Task GetPartitionsWhenCount1()
    {
        using var memoryStream = new MemoryStream();

        await using (var package = await DatabasePackage.OpenAsync(memoryStream, FileMode.Create, FileAccess.ReadWrite))
        {
            var packagePartition = package.CreatePartition("adventureworks", "products", DatabaseOperationType.Upsert);

            Assert.IsNotNull(packagePartition);
            Assert.IsFalse(packagePartition.Uri.IsAbsoluteUri);
            Assert.AreEqual("adventureworks", packagePartition.DatabaseName);
            Assert.AreEqual("products", packagePartition.ContainerName);
            Assert.AreEqual(DatabaseOperationType.Upsert, packagePartition.OperationType);

            await using var packagePartitionStream = packagePartition.GetStream(FileMode.Create, FileAccess.Write);

            Assert.IsNotNull(packagePartitionStream);

            var documents = new JsonObject[]
            {
                new()
                {
                    ["id"] = "5f3edc36-17c0-4e11-a6da-a81440214abe",
                    ["pk"] = "59fcfd99-4016-4eba-a9cd-4d44cae02282",
                },
            };

            await JsonSerializer.SerializeAsync(packagePartitionStream, documents, JsonSerializerOptions.Default);
        }

        memoryStream.Seek(0, SeekOrigin.Begin);

        await using (var package = await DatabasePackage.OpenAsync(memoryStream, FileMode.Open, FileAccess.Read))
        {
            var packagePartitions = package.GetPartitions();

            Assert.IsNotNull(packagePartitions);
            Assert.AreEqual(1, packagePartitions.Length);

            var packagePartition = packagePartitions.First();

            Assert.IsNotNull(packagePartition);
            Assert.IsFalse(packagePartition.Uri.IsAbsoluteUri);
            Assert.AreEqual("adventureworks", packagePartition.DatabaseName);
            Assert.AreEqual("products", packagePartition.ContainerName);
            Assert.AreEqual(DatabaseOperationType.Upsert, packagePartition.OperationType);

            await using var packagePartitionStream = packagePartition.GetStream(FileMode.Open, FileAccess.Read);

            Assert.IsNotNull(packagePartitionStream);

            var documents = await JsonSerializer.DeserializeAsync<JsonObject[]>(packagePartitionStream, JsonSerializerOptions.Default);

            Assert.IsNotNull(documents);
            Assert.AreEqual(1, documents.Length);

            var document = documents[0];

            Assert.IsNotNull(document);
            Assert.IsTrue(document.ContainsKey("id") && JsonNode.DeepEquals(document["id"], "5f3edc36-17c0-4e11-a6da-a81440214abe"));
            Assert.IsTrue(document.ContainsKey("pk") && JsonNode.DeepEquals(document["pk"], "59fcfd99-4016-4eba-a9cd-4d44cae02282"));
        }
    }

    [TestMethod]
    public async Task GetPartitionsWhenOpenWriteOnly()
    {
        using var memoryStream = new MemoryStream();

        await using (var package = await DatabasePackage.OpenAsync(memoryStream, FileMode.Create, FileAccess.ReadWrite))
        {
        }

        memoryStream.Seek(0, SeekOrigin.Begin);

        await using (var package = await DatabasePackage.OpenAsync(memoryStream, FileMode.Open, FileAccess.Write))
        {
            Assert.ThrowsExactly<IOException>(
                () => package.GetPartitions());
        }
    }
}
