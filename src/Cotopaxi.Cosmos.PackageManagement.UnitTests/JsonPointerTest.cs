#pragma warning disable CA1861

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cotopaxi.Cosmos.PackageManagement.UnitTests;

[TestClass]
public sealed class JsonPointerTest
{
    [DataTestMethod]
    [DataRow("", new string[] { })]
    [DataRow("/foo", new string[] { "foo" })]
    [DataRow("/foo/0", new string[] { "foo", "0" })]
    [DataRow("/~0", new string[] { "~" })]
    [DataRow("/~1", new string[] { "/" })]
    [DataRow("/~0~1", new string[] { "~/", })]
    [DataRow("/a/b/c", new string[] { "a", "b", "c" })]
    [DataRow("/a~1b~1c", new string[] { "a/b/c" })]
    [DataRow("/m~0n", new string[] { "m~n" })]
    public void RFC6901(string value, string[] expectedTokens)
    {
        var jsonPointer = new JsonPointer(value);
        var jsonPointerTokens = jsonPointer.Tokens.ToArray();

        CollectionAssert.AreEqual(expectedTokens, jsonPointerTokens);
    }
}
