// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;

namespace Cotopaxi.Cosmos.PackageManagement.Contracts;

internal sealed class PackageDocumentKeyNode
{
    [JsonConverter(typeof(CosmosResourceNameConverter))]
    [JsonRequired]
    [JsonPropertyName("databaseName")]
    public required CosmosResourceName DatabaseName
    {
        get;
        set;
    }

    [JsonConverter(typeof(CosmosResourceNameConverter))]
    [JsonRequired]
    [JsonPropertyName("containerName")]
    public required CosmosResourceName ContainerName
    {
        get;
        set;
    }

    [JsonConverter(typeof(CosmosDocumentIdConverter))]
    [JsonRequired]
    [JsonPropertyName("documentId")]
    public required CosmosDocumentId DocumentId
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
