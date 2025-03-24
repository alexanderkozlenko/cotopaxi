using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cotopaxi.Cosmos.PackageManagement.UnitTests;

[TestClass]
public sealed class CosmosResourceTests
{
    [TestMethod]
    public void TryGetPartitionKeyReturnsTrueWhenSize1()
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

        var result = CosmosResource.TryGetPartitionKey(document, partitionKeyPaths, out var actual);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetPartitionKeyReturnsTrueWhenSize2()
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

        var result = CosmosResource.TryGetPartitionKey(document, partitionKeyPaths, out var actual);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetPartitionKeyReturnsTrueWhenSize3()
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

        var result = CosmosResource.TryGetPartitionKey(document, partitionKeyPaths, out var actual);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetPartitionKeyReturnsTrueWhenNull()
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

        var result = CosmosResource.TryGetPartitionKey(document, partitionKeyPaths, out var actual);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetPartitionKeyReturnsTrueWhenUndefined()
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

        var result = CosmosResource.TryGetPartitionKey(document, partitionKeyPaths, out var actual);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetPartitionKeyReturnsFalse()
    {
        var document = new JsonObject
        {
            ["p1"] = new JsonObject(),
        };

        var partitionKeyPaths = new JsonPointer[]
        {
            new("/p1"),
        };

        var expected = default(PartitionKey);

        var result = CosmosResource.TryGetPartitionKey(document, partitionKeyPaths, out var actual);

        Assert.IsFalse(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetDocumentIdWhenReturnsTrue()
    {
        var document = new JsonObject
        {
            ["id"] = "5f3edc36-17c0-4e11-a6da-a81440214abe",
        };

        var result = CosmosResource.TryGetDocumentId(document, out var documentId);

        Assert.IsTrue(result);
        Assert.AreEqual("5f3edc36-17c0-4e11-a6da-a81440214abe", documentId);
    }

    [TestMethod]
    public void TryGetDocumentIdWhenReturnsFalseWhenUndefined()
    {
        var document = new JsonObject();

        var result = CosmosResource.TryGetDocumentId(document, out var documentId);

        Assert.IsFalse(result);
        Assert.IsNull(documentId);
    }

    [TestMethod]
    public void TryGetDocumentIdWhenReturnsFalseWhenUnsupported()
    {
        var document = new JsonObject
        {
            ["id"] = new JsonObject(),
        };

        var result = CosmosResource.TryGetDocumentId(document, out var documentId);

        Assert.IsFalse(result);
        Assert.IsNull(documentId);
    }

    [TestMethod]
    public void CleanupDocument()
    {
        var document = new JsonObject
        {
            ["id"] = "5f3edc36-17c0-4e11-a6da-a81440214abe",
            ["pk"] = "pk",
            ["_attachments"] = "_attachments",
            ["_etag"] = "_etag",
            ["_rid"] = "_rid",
            ["_self"] = "_self",
            ["_ts"] = "_ts",
        };

        CosmosResource.CleanupDocument(document);

        Assert.AreEqual(2, document.Count);
        Assert.IsTrue(document.ContainsKey("id"));
        Assert.IsTrue(document.ContainsKey("pk"));
    }

    [TestMethod]
    public void FormatDocument()
    {
        var document = new JsonObject
        {
            ["pk"] = "pk",
            ["id"] = "5f3edc36-17c0-4e11-a6da-a81440214abe",
            ["_attachments"] = "_attachments",
            ["_etag"] = "_etag",
            ["_rid"] = "_rid",
            ["_self"] = "_self",
            ["_ts"] = "_ts",
        };

        CosmosResource.FormatDocument(document);

        Assert.AreEqual(2, document.Count);
        Assert.IsTrue(document.ContainsKey("id"));
        Assert.IsTrue(document.ContainsKey("pk"));
        Assert.AreEqual("id", document.GetAt(0).Key);
    }

    [DataTestMethod]
    [DataRow(null, false)]
    [DataRow("1", true)]
    [DataRow("a", true)]
    [DataRow("", false)]
    [DataRow("/", false)]
    [DataRow("\\", false)]
    [DataRow("#", false)]
    [DataRow("?", false)]
    public void IsSupportedResourceId(string value, bool expected)
    {
        var result = CosmosResource.IsSupportedResourceId(value);

        Assert.AreEqual(expected, result);
    }
}
