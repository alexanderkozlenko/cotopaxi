﻿using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cotopaxi.Cosmos.PackageManagement.UnitTests;

[TestClass]
public sealed class CosmosDocumentTests
{
    [TestMethod]
    public void TryGetPartitionKeyReturnsTrue1()
    {
        var document = new JsonObject
        {
            ["category"] = "bikes",
        };

        var partitionKeyPaths = new JsonPointer[]
        {
            new("/category"),
        };

        var expected = new PartitionKeyBuilder()
            .Add("bikes")
            .Build();

        var result = CosmosDocument.TryGetPartitionKey(document, partitionKeyPaths, out var actual);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetPartitionKeyReturnsTrue2()
    {
        var document = new JsonObject
        {
            ["category"] = "bikes",
            ["subcategory1"] = "hybrid",
        };

        var partitionKeyPaths = new JsonPointer[]
        {
            new("/category"),
            new("/subcategory1"),
        };

        var expected = new PartitionKeyBuilder()
            .Add("bikes")
            .Add("hybrid")
            .Build();

        var result = CosmosDocument.TryGetPartitionKey(document, partitionKeyPaths, out var actual);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetPartitionKeyReturnsTrue3()
    {
        var document = new JsonObject
        {
            ["category"] = "bikes",
            ["subcategory1"] = "hybrid",
            ["subcategory2"] = "gravel",
        };

        var partitionKeyPaths = new JsonPointer[]
        {
            new("/category"),
            new("/subcategory1"),
            new("/subcategory2"),
        };

        var expected = new PartitionKeyBuilder()
            .Add("bikes")
            .Add("hybrid")
            .Add("gravel")
            .Build();

        var result = CosmosDocument.TryGetPartitionKey(document, partitionKeyPaths, out var actual);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetPartitionKeyReturnsTrue4()
    {
        var document = new JsonObject
        {
            ["category"] = "bikes",
        };

        var partitionKeyPaths = new JsonPointer[]
        {
            new("/category"),
            new("/subcategory1"),
            new("/subcategory2"),
        };

        var expected = new PartitionKeyBuilder()
            .Add("bikes")
            .AddNoneType()
            .AddNoneType()
            .Build();

        var result = CosmosDocument.TryGetPartitionKey(document, partitionKeyPaths, out var actual);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetPartitionKeyReturnsFalse()
    {
        var document = new JsonObject
        {
            ["category"] = new JsonObject(),
        };

        var partitionKeyPaths = new JsonPointer[]
        {
            new("/category"),
        };

        var expected = default(PartitionKey);

        var result = CosmosDocument.TryGetPartitionKey(document, partitionKeyPaths, out var actual);

        Assert.IsFalse(result);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryGetUniqueIDWhenReturnsTrue()
    {
        var document = new JsonObject
        {
            ["id"] = "5f3edc36-17c0-4e11-a6da-a81440214abe",
        };

        var result = CosmosDocument.TryGetUniqueID(document, out var documentID);

        Assert.IsTrue(result);
        Assert.AreEqual("5f3edc36-17c0-4e11-a6da-a81440214abe", documentID);
    }

    [TestMethod]
    public void TryGetUniqueIDWhenReturnsFalse1()
    {
        var document = new JsonObject
        {
        };

        var result = CosmosDocument.TryGetUniqueID(document, out var documentID);

        Assert.IsFalse(result);
        Assert.IsNull(documentID);
    }

    [TestMethod]
    public void TryGetUniqueIDWhenReturnsFalse2()
    {
        var document = new JsonObject
        {
            ["id"] = new JsonObject(),
        };

        var result = CosmosDocument.TryGetUniqueID(document, out var documentID);

        Assert.IsFalse(result);
        Assert.IsNull(documentID);
    }
}
