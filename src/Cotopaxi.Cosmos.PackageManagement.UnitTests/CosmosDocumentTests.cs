using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cotopaxi.Cosmos.PackageManagement.UnitTests;

[TestClass]
public sealed class CosmosDocumentTests
{
    [TestMethod]
    public void TryGetPartitionKeyFromArrayWithSize1()
    {
        var source = new JsonArray
        {
            "v1",
        };

        var expected = new PartitionKeyBuilder()
            .Add("v1")
            .Build();

        var result = CosmosDocument.TryGetPartitionKey(source, out var actual);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetPartitionKeyFromArrayWithSize2()
    {
        var source = new JsonArray
        {
            "v1",
            "v2",
        };

        var expected = new PartitionKeyBuilder()
            .Add("v1")
            .Add("v2")
            .Build();

        var result = CosmosDocument.TryGetPartitionKey(source, out var actual);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetPartitionKeyFromArrayWithSize3()
    {
        var source = new JsonArray
        {
            "v1",
            "v2",
            "v3",
        };

        var expected = new PartitionKeyBuilder()
            .Add("v1")
            .Add("v2")
            .Add("v3")
            .Build();

        var result = CosmosDocument.TryGetPartitionKey(source, out var actual);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetPartitionKeyFromArrayWithNullValue()
    {
        var source = new JsonArray
        {
            "v1",
            null,
        };

        var expected = new PartitionKeyBuilder()
            .Add("v1")
            .AddNullValue()
            .Build();

        var result = CosmosDocument.TryGetPartitionKey(source, out var actual);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetPartitionKeyFromArrayWithUndefinedValue()
    {
        var source = new JsonArray
        {
            "v1",
            new JsonObject(),
        };

        var expected = new PartitionKeyBuilder()
            .Add("v1")
            .AddNoneType()
            .Build();

        var result = CosmosDocument.TryGetPartitionKey(source, out var actual);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetPartitionKeyFromArrayWithUnsupportedValue()
    {
        var source = new JsonArray
        {
            new JsonObject
            {
                ["p"] = "v",
            },
        };

        var expected = default(PartitionKey);

        var result = CosmosDocument.TryGetPartitionKey(source, out var actual);

        Assert.IsFalse(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetPartitionKeyFromObjectWithSize1()
    {
        var document = new JsonObject
        {
            ["p1"] = "v1",
        };

        var partitionKeyPaths = new JsonPointer[]
        {
            new("/p1"),
        };

        var expected = new PartitionKeyBuilder()
            .Add("v1")
            .Build();

        var result = CosmosDocument.TryGetPartitionKey(document, partitionKeyPaths, out var actual);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetPartitionKeyFromObjectWithSize2()
    {
        var document = new JsonObject
        {
            ["p1"] = "v1",
            ["p2"] = "v2",
        };

        var partitionKeyPaths = new JsonPointer[]
        {
            new("/p1"),
            new("/p2"),
        };

        var expected = new PartitionKeyBuilder()
            .Add("v1")
            .Add("v2")
            .Build();

        var result = CosmosDocument.TryGetPartitionKey(document, partitionKeyPaths, out var actual);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetPartitionKeyFromObjectWithSize3()
    {
        var document = new JsonObject
        {
            ["p1"] = "v1",
            ["p2"] = "v2",
            ["p3"] = "v3",
        };

        var partitionKeyPaths = new JsonPointer[]
        {
            new("/p1"),
            new("/p2"),
            new("/p3"),
        };

        var expected = new PartitionKeyBuilder()
            .Add("v1")
            .Add("v2")
            .Add("v3")
            .Build();

        var result = CosmosDocument.TryGetPartitionKey(document, partitionKeyPaths, out var actual);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetPartitionKeyFromObjectWithNullValue()
    {
        var document = new JsonObject
        {
            ["p1"] = "v1",
            ["p2"] = null,
        };

        var partitionKeyPaths = new JsonPointer[]
        {
            new("/p1"),
            new("/p2"),
        };

        var expected = new PartitionKeyBuilder()
            .Add("v1")
            .AddNullValue()
            .Build();

        var result = CosmosDocument.TryGetPartitionKey(document, partitionKeyPaths, out var actual);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetPartitionKeyFromObjectWithUndefinedValue()
    {
        var document = new JsonObject
        {
            ["p1"] = "v1",
        };

        var partitionKeyPaths = new JsonPointer[]
        {
            new("/p1"),
            new("/p2"),
        };

        var expected = new PartitionKeyBuilder()
            .Add("v1")
            .AddNoneType()
            .Build();

        var result = CosmosDocument.TryGetPartitionKey(document, partitionKeyPaths, out var actual);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetPartitionKeyFromObjectWithUnsupportedValue()
    {
        var document = new JsonObject
        {
            ["p1"] = new JsonObject
            {
                ["p"] = "v",
            },
        };

        var partitionKeyPaths = new JsonPointer[]
        {
            new("/p1"),
        };

        var expected = default(PartitionKey);

        var result = CosmosDocument.TryGetPartitionKey(document, partitionKeyPaths, out var actual);

        Assert.IsFalse(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetId()
    {
        var document = new JsonObject
        {
            ["id"] = "5f3edc36-17c0-4e11-a6da-a81440214abe",
        };

        var result = CosmosDocument.TryGetId(document, out var documentId);

        Assert.IsTrue(result);
        Assert.AreEqual("5f3edc36-17c0-4e11-a6da-a81440214abe", documentId);
    }

    [TestMethod]
    public void TryGetIdWithUndefinedValue()
    {
        var document = new JsonObject();

        var result = CosmosDocument.TryGetId(document, out var documentId);

        Assert.IsFalse(result);
        Assert.IsNull(documentId);
    }

    [TestMethod]
    public void TryGetIdWithUnsupportedValue()
    {
        var document = new JsonObject
        {
            ["id"] = new JsonObject(),
        };

        var result = CosmosDocument.TryGetId(document, out var documentId);

        Assert.IsFalse(result);
        Assert.IsNull(documentId);
    }

    [TestMethod]
    public void Prune()
    {
        var document = new JsonObject
        {
            ["id"] = "5f3edc36-17c0-4e11-a6da-a81440214abe",
            ["pk"] = "59fcfd99-4016-4eba-a9cd-4d44cae02282",
            ["_attachments"] = "_attachments",
            ["_etag"] = "_etag",
            ["_rid"] = "_rid",
            ["_self"] = "_self",
            ["_ts"] = "_ts",
        };

        CosmosDocument.Prune(document);

        Assert.AreEqual(2, document.Count);
        Assert.IsTrue(document.ContainsKey("id") && JsonNode.DeepEquals(document["id"], "5f3edc36-17c0-4e11-a6da-a81440214abe"));
        Assert.IsTrue(document.ContainsKey("pk") && JsonNode.DeepEquals(document["pk"], "59fcfd99-4016-4eba-a9cd-4d44cae02282"));
    }

    [TestMethod]
    public void Format()
    {
        var document = new JsonObject
        {
            ["pk"] = "59fcfd99-4016-4eba-a9cd-4d44cae02282",
            ["id"] = "5f3edc36-17c0-4e11-a6da-a81440214abe",
            ["_attachments"] = "_attachments",
            ["_etag"] = "_etag",
            ["_rid"] = "_rid",
            ["_self"] = "_self",
            ["_ts"] = "_ts",
        };

        CosmosDocument.Format(document);

        Assert.AreEqual(2, document.Count);
        Assert.IsTrue(document.ContainsKey("id") && JsonNode.DeepEquals(document["id"], "5f3edc36-17c0-4e11-a6da-a81440214abe"));
        Assert.IsTrue(document.ContainsKey("pk") && JsonNode.DeepEquals(document["pk"], "59fcfd99-4016-4eba-a9cd-4d44cae02282"));
        Assert.AreEqual("id", document.First().Key);
    }
}
