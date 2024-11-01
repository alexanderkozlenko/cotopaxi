// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Text.Json.Serialization;

namespace Cotopaxi.Cosmos.PackageManagement;

internal sealed class ProjectDatabaseNode
{
    [JsonRequired]
    [JsonPropertyName("name")]
    public required string Name
    {
        get;
        set;
    }

    [JsonRequired]
    [JsonPropertyName("containers")]
    public required ProjectContainerNode?[] Containers
    {
        get;
        set;
    }
}
