// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Text.Json.Serialization;

namespace Cotopaxi.Cosmos.PackageManagement.Contracts;

internal sealed class ProjectDatabaseNode
{
    [JsonConverter(typeof(CosmosResourceNameConverter))]
    [JsonRequired]
    [JsonPropertyName("name")]
    public required CosmosResourceName Name
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
