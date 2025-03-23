#pragma warning disable CA1861

using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cotopaxi.Cosmos.PackageManagement.UnitTests;

[TestClass]
public sealed class JsonPointerTests
{
    [DataTestMethod]
    [DataRow("", new string[] { })]
    [DataRow("/foo", new string[] { "foo" })]
    [DataRow("/foo/0", new string[] { "foo", "0" })]
    [DataRow("/", new string[] { "" })]
    [DataRow("/a~1b", new string[] { "a/b" })]
    [DataRow("/c%d", new string[] { "c%d" })]
    [DataRow("/e^f", new string[] { "e^f" })]
    [DataRow("/g|h", new string[] { "g|h" })]
    [DataRow("/i\\\\j", new string[] { "i\\\\j" })]
    [DataRow("/k\\\"l", new string[] { "k\\\"l" })]
    [DataRow("/ ", new string[] { " " })]
    [DataRow("/m~0n", new string[] { "m~n" })]
    public void RFC6901(string jsonPointerValue, string[] jsonPointerTokens)
    {
        var jsonPointer = new JsonPointer(jsonPointerValue);

        CollectionAssert.AreEqual(jsonPointerTokens, jsonPointer.Tokens.ToArray());
    }
}
