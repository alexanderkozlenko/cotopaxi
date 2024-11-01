// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Text.Json.Serialization;

namespace Cotopaxi.Cosmos.PackageManagement;

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
    [JsonPropertyName("sources")]
    public required string?[] Sources
    {
        get;
        set;
    }
}
