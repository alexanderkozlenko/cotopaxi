// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Text.Json.Serialization;

namespace Cotopaxi.Cosmos.PackageManagement.Contracts;

internal sealed class ProjectNode
{
    [JsonRequired]
    [JsonPropertyName("databases")]
    public required ProjectDatabaseNode?[] Databases
    {
        get;
        set;
    }
}
