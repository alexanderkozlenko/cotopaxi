// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Text.Json.Serialization;
using Cotopaxi.Cosmos.Packaging;

namespace Cotopaxi.Cosmos.PackageManagement.Contracts;

internal sealed class ProjectOperationNode
{
    [JsonConverter(typeof(PackageOperationTypeConverter))]
    [JsonRequired]
    [JsonPropertyName("name")]
    public required PackageOperationType OperationType
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
