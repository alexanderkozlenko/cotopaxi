﻿#pragma warning disable CA1806

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cotopaxi.Cosmos.PackageManagement.UnitTests;

[TestClass]
public sealed class CosmosDocumentIdTests
{
    [DataTestMethod]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow(" ", true)]
    [DataRow("1", true)]
    [DataRow("a", true)]
    [DataRow("/", false)]
    [DataRow("\\", false)]
    [DataRow("#", true)]
    [DataRow("?", true)]
    public void Constructor(string value, bool expected)
    {
        if (expected)
        {
            new CosmosDocumentId(value);
        }
        else
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new CosmosDocumentId(value));
        }
    }

    [DataTestMethod]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow(" ", true)]
    [DataRow("1", true)]
    [DataRow("a", true)]
    [DataRow("/", false)]
    [DataRow("\\", false)]
    [DataRow("#", true)]
    [DataRow("?", true)]
    public void IsWellFormed(string value, bool expected)
    {
        var result = CosmosDocumentId.IsWellFormed(value);

        Assert.AreEqual(expected, result);
    }
}
