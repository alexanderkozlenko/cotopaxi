// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;

namespace Cotopaxi.Cosmos.PackageManagement.Contracts;

internal sealed class PackageDocumentKeyNode
{
    [JsonRequired]
    [JsonPropertyName("databaseName")]
    public required string DatabaseName
    {
        get;
        set;
    }

    [JsonRequired]
    [JsonPropertyName("containerName")]
    public required string ContainerName
    {
        get;
        set;
    }

    [JsonRequired]
    [JsonPropertyName("documentId")]
    public required string DocumentId
    {
        get;
        set;
    }

    [JsonConverter(typeof(PartitionKeyConverter))]
    [JsonRequired]
    [JsonPropertyName("documentPartitionKey")]
    public required PartitionKey DocumentPartitionKey
    {
        get;
        set;
    }
}
