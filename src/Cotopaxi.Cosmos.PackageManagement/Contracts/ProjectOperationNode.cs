// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Text.Json.Serialization;

namespace Cotopaxi.Cosmos.PackageManagement.Contracts;

internal sealed class ProjectOperationNode
{
    [JsonRequired]
    [JsonPropertyName("name")]
    public required string Name
    {
        get;
        set;
    }

    [JsonRequired]
    [JsonPropertyName("documents")]
    public required string?[] Documents
    {
        get;
        set;
    }
}
