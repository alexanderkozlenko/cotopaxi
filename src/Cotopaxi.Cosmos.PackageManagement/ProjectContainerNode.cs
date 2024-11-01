// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Text.Json.Serialization;

namespace Cotopaxi.Cosmos.PackageManagement;

internal sealed class ProjectContainerNode
{
    [JsonRequired]
    [JsonPropertyName("name")]
    public required string Name
    {
        get;
        set;
    }

    [JsonRequired]
    [JsonPropertyName("operations")]
    public required ProjectOperationNode?[] Operations
    {
        get;
        set;
    }
}
