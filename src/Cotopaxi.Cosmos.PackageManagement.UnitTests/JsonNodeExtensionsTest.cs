using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cotopaxi.Cosmos.PackageManagement.UnitTests;

[TestClass]
public sealed class JsonNodeExtensionsTest
{
    [DataTestMethod]
    [DataRow("", "$")]
    [DataRow("/foo", "$.foo")]
    [DataRow("/foo/0", "$.foo[0]")]
    [DataRow("/", "$.")]
    [DataRow("/a~1b", "$['a/b']")]
    [DataRow("/c%d", "$.c%d")]
    [DataRow("/e^f", "$.e^f")]
    [DataRow("/g|h", "$.g|h")]
    [DataRow("/i\\\\j", "$['i\\\\j']")]
    [DataRow("/k\\\"l", "$['k\\\"l']")]
    [DataRow("/ ", "$[' ']")]
    [DataRow("/m~0n", "$.m~n")]
    public void TryGetValue(string jsonPointerValue, string jsonPath)
    {
        var jsonObject = new JsonObject
        {
            ["foo"] = new JsonArray
            {
                "bar",
                "baz",
            },
            [""] = 0,
            ["a/b"] = 1,
            ["c%d"] = 2,
            ["e^f"] = 3,
            ["g|h"] = 4,
            ["i\\\\j"] = 5,
            ["k\\\"l"] = 6,
            [" "] = 7,
            ["m~n"] = 8,
        };

        var jsonPointer = new JsonPointer(jsonPointerValue);
        var result = jsonObject.TryGetValue(jsonPointer, out var value);

        Assert.IsTrue(result);
        Assert.IsNotNull(value);
        Assert.AreEqual(jsonPath, value.GetPath());
    }
}
